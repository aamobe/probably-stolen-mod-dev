param([string]$GuidePath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
if (!$GuidePath) { $GuidePath = Join-Path $root 'docs/官方资料/ModdingGuide.zh-CN.md' }
$rules = Get-Content -LiteralPath "$root/docs/接口卡依赖.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$text = [IO.File]::ReadAllText($GuidePath)
$sections = @{}
foreach ($m in [regex]::Matches($text,'(?ms)^## ([^\r\n]+)\r?\n(.*?)(?=^## |\z)')) {
    $sections[$m.Groups[1].Value] = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($m.Value.Replace("`r`n","`n"))))
}
$pending = @()
$cards = [IO.File]::ReadAllText((Join-Path $root $rules.document))
foreach ($dep in $rules.dependencies) {
    foreach ($card in $dep.cards) { if ($cards -notmatch ('(?m)^## ' + [regex]::Escape($card) + '\r?$')) { throw "开发卡标题不存在：$card" } }
    if (!$sections.ContainsKey($dep.section) -or $sections[$dep.section] -cne $dep.reviewed_sha256) {
        $pending += "官方章节「$($dep.section)」已改变或移除，请复核 $($rules.document)：$($dep.cards -join '、')"
    }
}
if ($pending.Count) { throw ($pending -join "`n") }
"通过：$($rules.dependencies.Count) 个官方章节与开发卡复核基线一致。更新基线必须先人工审阅，不由生成器自动接受。"
