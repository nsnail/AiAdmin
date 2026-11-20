#!/usr/bin/env pwsh

# NOTE: keep this file pure ASCII. Windows PowerShell 5.1 reads BOM-less scripts as the ANSI code page,
# so non-ASCII (e.g. Chinese) comments get mangled, swallow neighbouring lines and make the cleaned
# content empty, which used to blank out every changed file. This script itself strips BOMs, so a BOM
# cannot be used as the workaround.
#
# Normalizes changed/added/untracked text files in Git to: no BOM, LF line endings, no trailing
# whitespace, no trailing newline at EOF. A file is only rewritten when its content really changes.
#
# Safety guards:
#   1. If the cleaned content is empty while the original is not, the file is skipped with a warning
#   2. The result is written to a temp file next to the original, verified, then moved over it
#   3. A failure on one file only warns and continues; the exit code is non-zero if anything failed

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

# Binary and asset files are not text-cleaned
$binaryExtensions = @(
    '.png', '.jpg', '.jpeg', '.gif', '.ico', '.webp', '.bmp', '.svg', '.woff', '.woff2', '.ttf', '.eot', '.otf', '.zip', '.gz', '.7z', '.rar'
    ,'.pdf', '.mp3', '.mp4', '.mov', '.dll', '.exe', '.pdb', '.so', '.dylib', '.nupkg', '.snk', '.pfx'
)

# Directories that must never be cleaned
$excludedDirectories = @('/refs/', '/.git/', '/node_modules/')

# core.quotepath=false keeps non-ASCII paths from being escaped into quoted octal text
$trackedFiles = @(git -c core.quotepath=false -C $repoRoot diff --name-only --diff-filter=ACMR HEAD)
$untrackedFiles = @(git -c core.quotepath=false -C $repoRoot ls-files --others --exclude-standard)
$changedFiles = @($trackedFiles + $untrackedFiles) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$modified = 0
$failed = 0

foreach ($relative in $changedFiles)
{
    $normalized = '/' + ($relative -replace '\\', '/')
    if ($excludedDirectories | Where-Object { $normalized.Contains($_) })
    {
        continue
    }

    if ($binaryExtensions -contains [System.IO.Path]::GetExtension($relative).ToLowerInvariant())
    {
        continue
    }

    $path = Join-Path $repoRoot $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf))
    {
        continue
    }

    try
    {
        $bytes = [System.IO.File]::ReadAllBytes($path)
        if ($bytes.Length -eq 0 -or $bytes -contains 0)
        {
            continue
        }

        # Strip the UTF-8 BOM, then decode as UTF-8
        $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        $offset = if ($hasBom)
        {
            3
        }
        else
        {
            0
        }
        $original = $utf8NoBom.GetString($bytes, $offset, $bytes.Length - $offset)

        # CRLF and lone CR become LF, trailing spaces/tabs per line are removed, trailing newlines at EOF are removed
        $cleaned = $original -replace "`r`n", "`n" -replace "`r", "`n"
        $cleaned = [regex]::Replace($cleaned, '[ \t]+(?=\n|$)', '')
        $cleaned = $cleaned.TrimEnd("`n")

        if ($cleaned -ceq $original -and -not $hasBom)
        {
            continue
        }

        # Original has content but the cleaned result is empty: treat as abnormal and do not write back
        if ($cleaned.Length -eq 0 -and $original.Trim().Length -gt 0)
        {
            Write-Warning "skipped (cleaned content is empty): $relative"
            $failed++
            continue
        }

        # Write a temp file and verify its bytes before replacing the original
        $expected = $utf8NoBom.GetBytes($cleaned)
        $temp = "$path.dotclean.tmp"
        [System.IO.File]::WriteAllBytes($temp, $expected)
        $written = [System.IO.File]::ReadAllBytes($temp)
        if ($written.Length -eq 0 -or $written.Length -ne $expected.Length)
        {
            Remove-Item -LiteralPath $temp -Force
            Write-Warning "skipped (temp file verification failed): $relative"
            $failed++
            continue
        }

        Move-Item -LiteralPath $temp -Destination $path -Force
        $modified++
        Write-Host "cleaned: $relative"
    }
    catch
    {
        Write-Warning "failed: $relative - $( $_.Exception.Message )"
        $failed++
        if (Test-Path -LiteralPath "$path.dotclean.tmp")
        {
            Remove-Item -LiteralPath "$path.dotclean.tmp" -Force -ErrorAction SilentlyContinue
        }
    }
}

Write-Host "dot clean finished, $modified file(s) modified, $failed file(s) skipped or failed."
if ($failed -gt 0)
{
    exit 1
}