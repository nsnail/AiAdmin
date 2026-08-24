#!/usr/bin/env pwsh

$repoRoot = Split-Path -Parent $PSScriptRoot
$webRoot = Join-Path $repoRoot "src\AiAdmin.Web"
$webPrefix = "src/AiAdmin.Web/"
$prettierExtensions = '\.(js|cjs|mjs|ts|json|jsonc|tsx|css|less|scss|vue|html|htm|md|mdx|yaml|yml)$'

$changedFiles = @(
    git -C $repoRoot diff --name-only --diff-filter=ACMR HEAD -- src/AiAdmin.Web
    git -C $repoRoot ls-files --others --exclude-standard -- src/AiAdmin.Web
) | Where-Object {
    $_.StartsWith($webPrefix, [StringComparison]::OrdinalIgnoreCase) -and $_ -match $prettierExtensions
} | Sort-Object -Unique

if ($changedFiles.Count -eq 0) {
    Write-Host "No changed frontend files require Prettier cleanup."
    exit 0
}

$relativeFiles = $changedFiles | ForEach-Object { $_.Substring($webPrefix.Length) }
Push-Location $webRoot
try {
    cnpm run prettier:changed -- @relativeFiles
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}