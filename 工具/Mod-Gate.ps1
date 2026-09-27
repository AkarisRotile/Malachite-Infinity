#Requires -Version 5.1
<#
================================================================================
  Mod-Gate.ps1 —— 可成长的孔雀翎 · 可执行开发门禁
================================================================================
  用途：把"编译自检 + 结构审计"脚本化。本脚本只读源码并调用 dotnet build，
  不写工作区之外；打包 .tmod 到用户 Mods 目录被拒（TML001 / MSB3073）属预期
  （由作者同步 ModSources 后在游戏内构建），脚本会识别并计为"预期打包越界"，
  不当作编译失败。

  用法（在工作区根目录 E:\孔雀翎 下执行）：
    powershell -NoProfile -ExecutionPolicy Bypass -File .\工具\Mod-Gate.ps1 -Mode Gate
    （脚本为 UTF-8 BOM，Windows PowerShell 5.1 与 PowerShell 7 均可直接运行；
      本文档其余处统一缩写为 pwsh -NoProfile -File .\工具\Mod-Gate.ps1）
    -Mode Gate     编译 + 审计（默认）
    -Mode Compile  仅编译门禁
    -Mode Audit    仅结构审计

  退出码：0 = 通过；1 = 硬性失败（C# 编译错误或红线违规），禁止继续。

  审计红线：
    FAIL-1  error CS* 编译错误（其余 MSB/TML 错误需人工判读）
            预期不算失败：TML001（写用户 Mods 目录被拒）、TML003（游戏正开着，tML 拒绝直接打包）
    FAIL-2  弹幕（Projectiles\）出现 TextureAssets.MagicPixel —— 用自建 1x1 贴图/AdditiveLayer.Pixel；
            UI/HUD 条状绘制属允许范式，仅 WARN 提示人工确认坐标系
    FAIL-3  出现 using CalamityMod（编译期灾厄依赖回潮；软引用只允许 Core\CalamityCompat.cs 等）
    FAIL-4  在 Core\MalachitePalette.cs 与 UI\MalachiteUI.cs（已知债）之外散落数值色值
    WARN-*  代码中 CalamityMod 字符串软引用位置、Projectiles\ 署名头缺失、打包越界、警告清单等
================================================================================
#>
param(
    [ValidateSet('Gate', 'Compile', 'Audit')]
    [string]$Mode = 'Gate'
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

# ---------- 路径 ----------
$root    = Split-Path -Parent $PSScriptRoot      # E:\孔雀翎
$projDir = Join-Path $root 'mod本体\可成长的孔雀翎'
$proj    = Join-Path $projDir '可成长的孔雀翎.csproj'
$dll     = Join-Path $projDir 'bin\Debug\net8.0\可成长的孔雀翎.dll'
$paletteRel = 'Core\MalachitePalette.cs'
$uiRel      = 'UI\MalachiteUI.cs'
# 白名单：CalamityCompat=反射兼容层唯一授权；MalachiteData=Cache TryFind 软引用中枢（交接文档定义）；
# PeacockModifier / MalachitePlayer = 旧灾厄武器服务与 Draedon 探测等运行时软引用（无编译依赖）
$softRefAllow = @('Core\CalamityCompat.cs', 'Core\MalachiteData.cs', 'Items\PeacockModifier.cs', 'Players\MalachitePlayer.cs')

# ---------- 结果统计 ----------
$script:nPass = 0; $script:nFail = 0; $script:nWarn = 0; $script:nInfo = 0
function Write-Pass { param([string]$m) $script:nPass++; Write-Output "[PASS ] $m" }
function Write-Fail { param([string]$m) $script:nFail++; Write-Output "[FAIL ] $m" }
function Write-Warn { param([string]$m) $script:nWarn++; Write-Output "[WARN ] $m" }
function Write-Info { param([string]$m) $script:nInfo++; Write-Output "[INFO ] $m" }

function Get-RelPath { param($f) $f.FullName.Substring($projDir.Length).TrimStart('\') }

# ---------- 阶段 1：编译门禁 ----------
function Invoke-CompileGate {
    Write-Output '== 编译门禁：dotnet build -c Debug =='
    $start = Get-Date
    $lines = @(& dotnet build -c Debug --no-restore $proj 2>&1)
    $code  = $LASTEXITCODE
    $text  = $lines -join "`n"

    if ($code -ne 0 -and $text -match 'MSB4236|assets file.*not found') {
        Write-Warn '还原资产缺失（首次/缓存清空），改用完整 restore 重试……'
        $lines = @(& dotnet build -c Debug $proj 2>&1)
        $code  = $LASTEXITCODE
        $text  = $lines -join "`n"
    }

    # 逐行分类错误：捕获 error 后的代号（CS / TML / MSB...）
    $csLines = @(); $packLines = @(); $otherErr = @()
    foreach ($m in [regex]::Matches($text, '(?m)^.*?\berror\s+([A-Za-z0-9]+)\b.*$')) {
        $codeTag = $m.Groups[1].Value
        $line    = $m.Value.Trim()
        if ($codeTag -like 'CS*') {
            $csLines += $line
        } elseif ($codeTag -eq 'TML001' -or $codeTag -eq 'TML003') {
            $packLines += $line
        } elseif ($codeTag -eq 'MSB3073' -and ($text -match 'TML00[13]|Access to the path|tModLoader\.dll -server')) {
            $packLines += $line
        } else {
            $otherErr += $line
        }
    }

    if ($csLines.Count -eq 0) {
        Write-Pass 'C# 编译 0 错误（error CS = 0）'
    } else {
        Write-Fail "C# 编译错误 $($csLines.Count) 处："
        $csLines | Select-Object -First 12 | ForEach-Object { Write-Output "        $_" }
    }

    if ($packLines.Count -gt 0) {
        Write-Warn "TML001/TML003/MSB3073 打包越界 $($packLines.Count) 条：写用户 Mods 目录被拒或游戏正开着——属预期，由作者游戏内构建（不计失败）"
    }

    if ($otherErr.Count -eq 0) {
        Write-Pass '无其它 MSBuild/工具错误'
    } else {
        Write-Fail "其它错误 $($otherErr.Count) 处（需人工判读）："
        $otherErr | Select-Object -First 8 | ForEach-Object { Write-Output "        $_" }
    }

    # 产物检查（增量构建跳过编译属正常，仅提示不判失败）
    if (Test-Path $dll) {
        $dllTime = (Get-Item $dll).LastWriteTime
        if ($dllTime -ge $start.AddSeconds(-5)) {
            Write-Info "产物本次已刷新：bin\Debug\net8.0\可成长的孔雀翎.dll（$($dllTime.ToString('HH:mm:ss'))）"
        } else {
            Write-Info "产物为最近一次成功编译（$($dllTime.ToString('yyyy-MM-dd HH:mm:ss'))）；本次无源码变更触发增量跳过时属正常"
        }
    } else {
        Write-Fail "找不到产物 $dll"
    }

    # 警告清单（按类型聚合）
    $warnKinds = @{}
    foreach ($w in @([regex]::Matches($text, '(?m)^.*\bwarning\b.*$') | ForEach-Object { $_.Value.Trim() })) {
        $kind = 'other'
        if ($w -match 'CS0618') { $kind = 'CS0618 Label/TooltipAttribute 过时（已知存量，建议迁移 LabelKey/TooltipKey）' }
        elseif ($w -match 'ChangeMagicNumberToID') { $kind = 'tModCodeAssist 魔法数字建议（低优先）' }
        $warnKinds[$kind] = 1 + $warnKinds[$kind]
    }
    $warnTotal = ($warnKinds.Values | Measure-Object -Sum).Sum
    if ($warnTotal -gt 0) {
        Write-Warn "编译警告共 $warnTotal 条：$((($warnKinds.GetEnumerator() | ForEach-Object { "$($_.Value)×$($_.Key)" }) -join '；'))"
    } else {
        Write-Info '编译警告 0 条'
    }
}

# ---------- 阶段 2：结构审计 ----------
function Invoke-Audit {
    Write-Output '== 结构审计：红线扫描 =='
    $csFiles = @(Get-ChildItem -Path $projDir -Recurse -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })

    if ($csFiles.Count -eq 0) { Write-Fail '未找到任何 .cs 源文件（路径错误？）'; return }

    # FAIL-2：MagicPixel —— Projectiles\ 内硬禁；UI/HUD 仅提示人工确认
    $magicProj = @(); $magicOther = @()
    foreach ($f in $csFiles) {
        $rel = Get-RelPath $f
        if ((Get-Content $f.FullName -Raw -Encoding UTF8) -match 'TextureAssets\.MagicPixel') {
            if ($rel -like 'Projectiles\*') { $magicProj += $rel } else { $magicOther += $rel }
        }
    }
    if ($magicProj.Count -eq 0) {
        Write-Pass '弹幕文件无 TextureAssets.MagicPixel（特效贴图纪律 OK）'
    } else {
        Write-Fail "弹幕文件发现 TextureAssets.MagicPixel（$($magicProj.Count) 处）：$($magicProj -join '、') —— 必须改用自建 1x1 贴图/AdditiveLayer.Pixel"
    }
    if ($magicOther.Count -gt 0) {
        Write-Warn "非弹幕文件使用 MagicPixel（UI/HUD 条状绘制属允许范式）：$($magicOther -join '、') —— 请人工确认绘制坐标系为 UI 像素空间而非世界坐标"
    }

    # FAIL-3：using CalamityMod 编译期回潮
    $usingCal = @()
    foreach ($f in $csFiles) {
        $rel = Get-RelPath $f
        if ((Get-Content $f.FullName -Raw -Encoding UTF8) -match '(?m)^\s*using\s+CalamityMod(\s|\.|;)') { $usingCal += $rel }
    }
    if ($usingCal.Count -eq 0) { Write-Pass '无 using CalamityMod（灾厄编译期依赖 = 0）' }
    else { Write-Fail "发现 using CalamityMod（$($usingCal.Count) 处）：$($usingCal -join '、')" }

    # FAIL-4：数值色值散落（允许：Palette 本体；已知债：MalachiteUI.cs）
    $colorFiles = @()
    foreach ($f in $csFiles) {
        $rel = Get-RelPath $f
        if ($rel -eq $paletteRel -or $rel -eq $uiRel) { continue }
        $count = ([regex]::Matches((Get-Content $f.FullName -Raw -Encoding UTF8), 'new Color\(\s*\d+')).Count
        if ($count -gt 0) { $colorFiles += "$rel ($count)" }
    }
    if ($colorFiles.Count -eq 0) {
        Write-Pass '数值色值仅在 MalachitePalette（UI\MalachiteUI.cs 已知债除外）'
    } else {
        Write-Fail "数值色值散落：$($colorFiles -join '；')（应改用 MalachitePalette 常量）"
    }

    # WARN：代码中 CalamityMod 字符串软引用（跳过整行注释——合规署名头多为注释）
    $soft = @()
    foreach ($f in $csFiles) {
        $rel = Get-RelPath $f
        if ($rel -in $softRefAllow) { continue }
        $hit = 0
        foreach ($ln in (Get-Content $f.FullName -Encoding UTF8)) {
            $t = $ln.TrimStart()
            if ($t.StartsWith('//')) { continue }
            if ($t -match 'CalamityMod') { $hit++ }
        }
        if ($hit -gt 0) { $soft += "$rel ($hit)" }
    }
    if ($soft.Count -gt 0) {
        Write-Warn "代码行出现 CalamityMod 字符串/软引用（请逐一确认仅做运行时探测，不在白名单 $($softRefAllow -join '/') 内）：$($soft -join '、')"
    } else {
        Write-Pass 'CalamityMod 代码行引用已收敛到白名单文件'
    }

    # WARN：Projectiles\ 参考署名头覆盖（该目录为参考密集区；其余目录仅报总数）
    $noHeaderProj = @(); $withHeader = 0
    foreach ($f in $csFiles) {
        $rel = Get-RelPath $f
        $head = (Get-Content $f.FullName -Encoding UTF8 -TotalCount 20) -join "`n"
        if ($head -match '署名|自主实现|参考|Azafure|hocha113|CalamityModPublic|CalamityOverhaul') {
            $withHeader++
        } elseif ($rel -like 'Projectiles\*') {
            $noHeaderProj += $rel
        }
    }
    Write-Info "参考署名头：$withHeader/$($csFiles.Count) 个 .cs 头部有署名/自主声明"
    if ($noHeaderProj.Count -gt 0) {
        Write-Warn "Projectiles\ 以下文件头 20 行内无署名/自主声明（新参考文件必须补）：$($noHeaderProj -join '、')"
    }
}

# ---------- 主流程 ----------
Write-Output "==== 孔雀翎 Mod-Gate · Mode=$Mode · $(Get-Date -Format 'yyyy-MM-dd HH:mm') ===="
if ($Mode -in @('Gate', 'Compile')) { Invoke-CompileGate }
if ($Mode -in @('Gate', 'Audit'))   { Invoke-Audit }

Write-Output '----------------------------------------'
Write-Output "结果：PASS=$nPass  FAIL=$nFail  WARN=$nWarn  INFO=$nInfo"
if ($nFail -gt 0) {
    Write-Output '结论：FAIL —— 存在硬性失败，禁止宣称编译通过/继续下一步。'
    exit 1
}
Write-Output '结论：PASS —— 可进入下一步（记录/交接/同步作者测）。'
exit 0
