#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$folder = Join-Path $root 'docs/官方资料'
$meta = Get-Content -LiteralPath "$folder/source.json" -Raw -Encoding UTF8 | ConvertFrom-Json
if ($meta.commit -notmatch '^[a-f0-9]{40}$') { throw '来源提交必须是完整 SHA' }
foreach ($entry in $meta.files) {
    if ($entry.path -notmatch '^[A-Za-z0-9.-]+\.md$') { throw '来源文件名无效' }
    $dest = Join-Path $folder $entry.path
    if ((Test-Path -LiteralPath $dest) -and (Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash -ceq $entry.sha256) { continue }
    $temp = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
    try {
        Invoke-WebRequest -Uri "https://raw.githubusercontent.com/dragonpan2/probably-stolen-modding/$($meta.commit)/$($entry.path)" -OutFile $temp -TimeoutSec 30
        if ((Get-FileHash -LiteralPath $temp -Algorithm SHA256).Hash -cne $entry.sha256) { throw "下载哈希不匹配：$($entry.path)" }
        Copy-Item -LiteralPath $temp -Destination $dest
    } finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp } }
}
& "$PSScriptRoot/Build-ApiReference.ps1"
& "$PSScriptRoot/Check-ApiCards.ps1"
'官方资料已下载并核对到本地忽略目录；官方全文不随仓库提交。'
