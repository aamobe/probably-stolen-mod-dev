param([switch]$Check)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$source = Join-Path $root 'docs/官方资料/ModdingGuide.zh-CN.md'
$target = Join-Path $root 'docs/官方接口'
$meta = Get-Content -LiteralPath "$root/docs/官方资料/source.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$sha = $meta.commit
if ($sha -notmatch '^[a-f0-9]{40}$') { throw '来源提交必须是完整 SHA' }
foreach ($entry in $meta.files) {
    $actual = (Get-FileHash -LiteralPath (Join-Path "$root/docs/官方资料" $entry.path) -Algorithm SHA256).Hash
    if ($actual -cne $entry.sha256) { throw "原始快照已改变，先审阅来源：$($entry.path)" }
}
$raw = [IO.File]::ReadAllText($source)
$hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
function Slug([string]$s) {
    $s = $s.ToLowerInvariant() -replace '[^\p{L}\p{N}\p{M}\s_-]', ''
    return $s -replace '\s', '-'
}
$parts = [regex]::Matches($raw, '(?ms)^## ([^\r\n]+)\r?\n(.*?)(?=^## |\z)')
$sections = @(); $anchors = @{}; $counts = @{}; $n = 0
foreach ($part in $parts) {
    $title = $part.Groups[1].Value
    if ($title -eq '目录') { continue }
    $n++; $file = ('{0:D2}-' -f $n) + ($title -replace '[\\/:*?"<>|]', '') + '.md'
    $body = $part.Value
    $body = [regex]::Replace($body, '(?m)^(#{2,6}) ([^\r\n]+)', {
        param($m)
        $id = Slug $m.Groups[2].Value
        $base = $id
        if ($counts.ContainsKey($base)) { $counts[$base]++; $id += '-' + $counts[$base] } else { $counts[$base] = 0 }
        $anchors[$id] = $file
        '<a id="' + $id + '"></a>' + "`n" + $m.Value
    })
    $sections += [pscustomobject]@{Title=$title; File=$file; Body=$body; Original=$part.Value}
}
$outputs = @{}
$index = @('# 官方接口章节目录', '', "来源提交：$sha。原始指南 SHA256：$hash。", '', '由 scripts/Build-ApiReference.ps1 生成。保留官方正文和示例，新增稳定锚点并修正链接；请修改原始快照后重新生成，不手工编辑章节。接口属于文档示例，未核对本机程序集签名。', '')
$symbols = @{}
foreach ($section in $sections) {
    $body = [regex]::Replace($section.Body, '\]\(([^)]+)\)', {
        param($m)
        $link = $m.Groups[1].Value.Trim('<','>')
        if ($link -match '^https?://') { return $m.Value }
        if ($link.StartsWith('#')) {
            $id = [Uri]::UnescapeDataString($link.Substring(1))
            if (-not $anchors.ContainsKey($id)) { throw "未知官方锚点：$link" }
            return '](<' + $anchors[$id] + '#' + $id + '>)'
        }
        $path = ($link -split '#')[0]
        if (Test-Path -LiteralPath (Join-Path "$root/docs/官方资料" $path)) { return '](<../官方资料/' + $link + '>)' }
        return '](https://github.com/dragonpan2/probably-stolen-modding/blob/' + $sha + '/' + $link + ')'
    })
    $outputs[$section.File] = $body
    $index += '- [' + $section.Title + '](https://github.com/dragonpan2/probably-stolen-modding/blob/' + $sha + '/ModdingGuide.zh-CN.md#' + (Slug $section.Title) + ')'
    foreach ($m in [regex]::Matches($section.Original, '\b(?:ModHelper|ModHook|ModReflect|MachineHelper|CustomUIManager|KeyBindHelper|AppraisalHelper|AppraisalRankHelper)\.[A-Za-z_][A-Za-z0-9_]*')) {
        $symbol = $m.Value
        if (-not $symbols.ContainsKey($symbol)) { $symbols[$symbol] = [Collections.Generic.List[string]]::new() }
        if (-not $symbols[$symbol].Contains($section.File)) { $symbols[$symbol].Add($section.File) }
    }
}
$outputs['目录.md'] = $index -join "`n"
$rows = @('# 官方接口符号索引', '', '从官方正文和示例中提取的明确成员引用；不是程序集导出的完整 API，也不推断参数、返回类型或省略前缀的方法。按名称查到章节后阅读参数示例与限制。', '', '| 成员 | 出现章节 |', '| --- | --- |')
foreach ($symbol in ($symbols.Keys | Sort-Object)) {
    $links = @($symbols[$symbol] | ForEach-Object { $title = $_ -replace '^\d+-|\.md$',''; '[' + $title + '](https://github.com/dragonpan2/probably-stolen-modding/blob/' + $sha + '/ModdingGuide.zh-CN.md#' + (Slug $title) + ')' })
    $rows += '| `' + $symbol + '` | ' + ($links -join '、') + ' |'
}
$symbolText = $rows -join "`n"
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($file in $outputs.Keys) {
    $path = Join-Path $target $file
    if ($Check) { if (-not (Test-Path -LiteralPath $path) -or [IO.File]::ReadAllText($path) -cne $outputs[$file]) { throw "生成内容过期：$file" } }
    else { [IO.File]::WriteAllText($path, $outputs[$file], [Text.UTF8Encoding]::new($false)) }
}
$symbolPath = Join-Path $root 'docs/接口符号索引.md'
if ($Check) { if (-not (Test-Path -LiteralPath $symbolPath) -or [IO.File]::ReadAllText($symbolPath) -cne $symbolText) { throw '符号索引过期' } }
else { [IO.File]::WriteAllText($symbolPath, $symbolText, [Text.UTF8Encoding]::new($false)) }
$unexpected = Get-ChildItem -LiteralPath $target -File | Where-Object { -not $outputs.ContainsKey($_.Name) }
if ($unexpected) { throw "存在旧章节，请人工处理：$($unexpected.Name -join ', ')" }
"通过：$($sections.Count) 个官方章节，$($symbols.Count) 个明确成员引用；生成模式 Check=$Check。"
