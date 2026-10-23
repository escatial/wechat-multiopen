param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$projectPath = Join-Path $PSScriptRoot "..\src\WechatMultiopen\WechatMultiopen.csproj"
$outputPath = Join-Path $PSScriptRoot "..\artifacts\$Configuration"

dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outputPath

$exePath = Join-Path $outputPath "wechat-multiopen.exe"
if (-not (Test-Path -LiteralPath $exePath)) { throw "发布结果不存在：$exePath" }
$pdbPath = Join-Path $outputPath "wechat-multiopen.pdb"
if (Test-Path -LiteralPath $pdbPath) { Remove-Item -LiteralPath $pdbPath -Force }
Get-FileHash -Algorithm SHA256 -LiteralPath $exePath |
    ForEach-Object { "$($_.Hash)  $($_.Path)" } |
    Set-Content -LiteralPath (Join-Path $outputPath "wechat-multiopen.exe.sha256") -Encoding ascii
Write-Host "发布完成：$exePath"
