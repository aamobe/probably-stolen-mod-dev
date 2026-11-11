$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
& "$PSScriptRoot/Build-ApiReference.ps1" -Check
& "$PSScriptRoot/Check-ApiCards.ps1"
$files = @('README.md','AGENTS.md','总方案.md','docs/接口速查.md','docs/接口开发卡.md','docs/接口符号索引.md','docs/来源与适配.md') | ForEach-Object { Get-Item -LiteralPath (Join-Path $root $_) }
$files += Get-ChildItem -LiteralPath "$root/docs/官方接口" -Filter '*.md'
$count = 0; $errors = [Collections.Generic.List[string]]::new()
function Slug([string]$s) { return (($s.ToLowerInvariant() -replace '[^\p{L}\p{N}\p{M}\s_-]', '') -replace '\s','-') }
foreach ($file in $files) {
    $text = [IO.File]::ReadAllText($file.FullName)
    # Code examples can contain literal Markdown, not navigational links.
    $text = [regex]::Replace($text,'(?ms)^```.*?^```[^\r\n]*','')
    foreach ($m in [regex]::Matches($text,'\]\((?:<([^>]+)>|([^\)]+))\)')) {
        $link = if ($m.Groups[1].Success) { $m.Groups[1].Value } else { $m.Groups[2].Value }
        if ($link -match '^(https?://|mailto:)') { continue }
        $bits = $link -split '#',2
        $path = if ($bits[0]) { Join-Path $file.DirectoryName ([Uri]::UnescapeDataString($bits[0])) } else { $file.FullName }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $errors.Add("$($file.Name): 文件不存在 $link"); continue }
        if ($bits.Count -gt 1 -and $bits[1]) {
            $id = [Uri]::UnescapeDataString($bits[1]); $target = [IO.File]::ReadAllText($path)
            $ids = @([regex]::Matches($target,'<a id="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
            $ids += @([regex]::Matches($target,'(?m)^#{1,6} ([^\r\n]+)') | ForEach-Object { Slug $_.Groups[1].Value })
            if ($id -notin $ids) { $errors.Add("$($file.Name): 锚点不存在 $link") }
        }
        $count++
    }
}
foreach ($doc in @('AGENTS.md','总方案.md')) {
    $text = [IO.File]::ReadAllText((Join-Path $root $doc))
    if ($text -match '警告数不超过|附录 H“工程模板”里的|先用测试驱动把游戏开到') { $errors.Add("$doc 残留已淘汰规则") }
}
if ($errors.Count) { throw ($errors -join "`n") }
"通过：$count 个本地链接及其目标锚点；生成一致性和已知规则残留检查通过。"
'范围：不请求外网，不校验官方快照内部原始链接，不验证 C# 编译、游戏接口签名或运行行为。'
