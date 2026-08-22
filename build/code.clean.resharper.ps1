#!/usr/bin/env pwsh

$repoRoot = Split-Path -Parent $PSScriptRoot
$files = $( git -C $repoRoot status --porcelain | Where-Object { $_ -match "^\s*[MA]" } | ForEach-Object { $_.Substring(3).Trim() } ) -join ";"
jb cleanupcode --no-build --include="$files" --dotnetcore="C:\Program Files\dotnet\dotnet.exe" --dotnetcoresdk=10.0.302 "$repoRoot\AiAdmin.slnx"