<#
  Ask-Agy.ps1 —— AGY 咨询入口（薄封装，真正实现是 ask-agy.mjs）
  =====================================================================
  为什么是封装而不是原生实现：
    本沙箱下 PowerShell / curl 走 Windows schannel，禁止访问证书库，
    HTTPS 一律失败（SEC_E_NO_CREDENTIALS）。Node 自带 OpenSSL，直连与走本机
    代理均实测 200 —— 所以调用器必须是 Node。
  详见《写法\AI工具配置与调用纪律.md》§三之二。

  用法：
    powershell -NoProfile -ExecutionPolicy Bypass -File .\写法\_tools\Ask-Agy.ps1 `
        -PromptFile .\写法\_agy\prompt_01.md -OutFile .\写法\_agy\reply_01.md
    其余开关（-Model / -RetryHigh / -ViaProxy）原样透传给 ask-agy.mjs。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PromptFile,
    [Parameter(Mandatory = $true)][string]$OutFile,
    [string]$Model = '',
    [switch]$RetryHigh,
    [switch]$ViaProxy
)

$ErrorActionPreference = 'Stop'
$mjs = Join-Path $PSScriptRoot 'ask-agy.mjs'
if (-not (Test-Path $mjs)) { throw "找不到调用器：$mjs" }

$argv = @($mjs, '--prompt', $PromptFile, '--out', $OutFile)
if ($Model) { $argv += @('--model', $Model) }
if ($RetryHigh) { $argv += '--retry-high' }
if ($ViaProxy) { $argv += '--via-proxy' }

& node @argv
exit $LASTEXITCODE