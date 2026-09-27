# Peacock Willow Blade - full mirror sync: workspace -> tModLoader ModSources
#
# Purpose : mirror the mod source folder into Terraria's ModSources, deleting stale files on the target side.
# Lesson  : see the handover overview, section 6.1 - leftover old files make the in-game build fail with
#           many errors, so this must be a DELETE mirror, not a plain copy.
# Usage   : powershell -NoProfile -ExecutionPolicy Bypass -File .\工具\Sync-ToModSources.ps1
#
# IMPORTANT: this file is deliberately ASCII-only with NO non-ASCII literals and NO character escapes.
#            Windows PowerShell 5.1 decodes BOM-less scripts as ANSI/GBK; any CJK literal written here would
#            be decoded into a different byte sequence, which can swallow quotes and corrupt the whole parse.
#            Folders whose names contain CJK are located with wildcards instead of literal names.
#            Keep it that way: do not paste CJK text into this script.
#
# NOTE: the destination lives outside the workspace and requires a sandbox mode that permits writes there.

$ErrorActionPreference = 'Stop'

function Get-RelFiles([string]$Root) {
    $resolved = (Resolve-Path -LiteralPath $Root).Path
    $exclude = @('obj', 'bin', '.vs')
    Get-ChildItem -LiteralPath $resolved -Recurse -File -Force | Where-Object {
        $rel = $_.FullName.Substring($resolved.Length).TrimStart([char]92)
        $exclude -notcontains $rel.Split([char]92)[0]
    }
}

# ---- Locate the mod source folder inside the workspace ----------------------
# The workspace is organised as: <workspace>\<namespace folder>\<mod folder>\, and the mod folder is the one
# holding build.txt. Search recursively so intermediate folders with CJK names never have to be spelled out.
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$Source = Get-ChildItem -LiteralPath $workspaceRoot -Recurse -File -Force -Filter 'build.txt' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' } |
    Select-Object -First 1 -ExpandProperty DirectoryName

if (-not $Source) {
    throw "No mod source folder (a build.txt outside obj/bin) found under: $workspaceRoot"
}

# ---- Locate the target ModSources mod folder -------------------------------
$modSourcesRoot = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\ModSources'
if (-not (Test-Path -LiteralPath $modSourcesRoot)) { throw "ModSources root not found: $modSourcesRoot" }

$Dest = Get-ChildItem -LiteralPath $modSourcesRoot -Directory -Force |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'build.txt') } |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $Dest) { throw "No mod folder (a ModSources subdirectory containing build.txt) found under: $modSourcesRoot" }

Write-Host "Source : $Source"
Write-Host "Dest   : $Dest"

$srcFiles = @(Get-RelFiles $Source)

# ---- 1. Copy every source file (overwrite) --------------------------------
$copied = 0
$failed = @()
foreach ($f in $srcFiles) {
    $rel = $f.FullName.Substring($Source.Length).TrimStart([char]92)
    $target = Join-Path $Dest $rel
    $targetDir = Split-Path -Parent $target
    try {
        if (-not (Test-Path -LiteralPath $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
        Copy-Item -LiteralPath $f.FullName -Destination $target -Force -ErrorAction Stop
        $copied++
    } catch {
        $failed += "$rel :: $($_.Exception.Message)"
    }
}

# ---- 2. Delete target-side files absent from the source (mirror semantics) --
$srcRelSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($f in $srcFiles) { [void]$srcRelSet.Add($f.FullName.Substring($Source.Length).TrimStart([char]92)) }

$removed = 0
$rmFailed = @()
foreach ($f in @(Get-RelFiles $Dest)) {
    $rel = $f.FullName.Substring($Dest.Length).TrimStart([char]92)
    if (-not $srcRelSet.Contains($rel)) {
        try {
            Remove-Item -LiteralPath $f.FullName -Force -ErrorAction Stop
            $removed++
        } catch {
            $rmFailed += "$rel :: $($_.Exception.Message)"
        }
    }
}

# ---- 3. Prune directories left empty by the mirror -------------------------
Get-ChildItem -LiteralPath $Dest -Recurse -Directory -Force |
    Where-Object { @(Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Force).Count -eq 0 } |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        try { Remove-Item -LiteralPath $_.FullName -Force -Recurse -ErrorAction Stop } catch { }
    }

# ---- 4. Verify by counting, never by claiming success ----------------------
$srcCount = @(Get-RelFiles $Source).Count
$dstCount = @(Get-RelFiles $Dest).Count

Write-Host ""
Write-Host "Copied : $copied / $($srcFiles.Count)"
Write-Host "Removed: $removed"
if ($failed.Count -gt 0) {
    Write-Host "Copy failures: $($failed.Count)"
    $failed | ForEach-Object { Write-Host "  - $_" }
}
if ($rmFailed.Count -gt 0) {
    Write-Host "Remove failures: $($rmFailed.Count)"
    $rmFailed | ForEach-Object { Write-Host "  - $_" }
}
Write-Host "File count: source=$srcCount target=$dstCount"

$ok = ($failed.Count -eq 0) -and ($rmFailed.Count -eq 0) -and ($srcCount -eq $dstCount)
if ($ok) {
    Write-Host "Result: PASS"
} else {
    Write-Host "Result: FAIL"
    exit 1
}
