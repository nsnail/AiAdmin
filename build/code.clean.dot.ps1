#!/usr/bin/env pwsh

$repoRoot = Split-Path -Parent $PSScriptRoot
dot rbom -w -e refs -e .git -e node_modules "$repoRoot"
dot trim -w -e refs -e .git -e node_modules "$repoRoot"
dot tolf -w -e refs -e .git -e node_modules "$repoRoot"