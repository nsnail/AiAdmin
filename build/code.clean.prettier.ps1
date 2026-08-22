#!/usr/bin/env pwsh

$repoRoot = Split-Path -Parent $PSScriptRoot
cnpm --prefix "$repoRoot\src\AiAdmin.Web" run prettier