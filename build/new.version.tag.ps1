#!/usr/bin/env pwsh

<#
.SYNOPSIS
交互式 Git SemVer 版本标签升级脚本 vX.Y.Z
.DESCRIPTION
交互式选择 major/minor/patch，自动读取最高v标签，校验本地&远程，创建注释标签，仅推送标签
#>

#region 1. 获取所有合法 vX.Y.Z 标签
Write-Host "`n=== Git 版本标签升级工具 ===" -ForegroundColor Cyan
Write-Host "[1/6] 获取本地语义化版本标签..."

$gitTags = git tag --list
if ($LASTEXITCODE -ne 0)
{
    Write-Error "执行 git tag --list 失败，请确认当前目录为git仓库"
    exit 1
}

$semverPattern = '^v(?<maj>\d+)\.(?<min>\d+)\.(?<pat>\d+)$'
$versionList = @()
foreach ($t in $gitTags)
{
    if ($t -match $semverPattern)
    {
        $versionList += [PSCustomObject]@{
            TagName = $t
            Major = [int]$matches.maj
            Minor = [int]$matches.min
            Patch = [int]$matches.pat
        }
    }
}

# 无基准版本直接退出
if ($versionList.Count -eq 0)
{
    Write-Error "仓库内未找到任何 vX.Y.Z 格式基准标签，无法升级版本"
    exit 1
}

# 获取最高版本
$latestVer = $versionList | Sort-Object Major,Minor,Patch -Descending | Select-Object -First 1
Write-Host "`n当前最高版本标签: $( $latestVer.TagName )`n" -ForegroundColor Green

#region 2. 交互式菜单选择
Write-Host "请选择版本升级类型："
Write-Host "  1) major  - 主版本升级 ($( $latestVer.TagName ) → v$( $latestVer.Major + 1 ).0.0)"
Write-Host "  2) minor  - 次版本升级 ($( $latestVer.TagName ) → v$( $latestVer.Major ).$( $latestVer.Minor + 1 ).0)"
Write-Host "  3) patch  - 补丁升级   ($( $latestVer.TagName ) → v$( $latestVer.Major ).$( $latestVer.Minor ).$( $latestVer.Patch + 1 ))"
Write-Host "  0) 退出，不执行任何操作`n"

do
{
    $choice = Read-Host "请输入选项数字(0‑3)"
    switch ($choice)
    {
        "1" {
            $bump = "major"; break
        }
        "2" {
            $bump = "minor"; break
        }
        "3" {
            $bump = "patch"; break
        }
        "0" {
            Write-Host "已退出，无操作。"; exit 0
        }
        default {
            Write-Warning "无效输入，请输入 0‑3 之间数字`n"
        }
    }
} while (-not $bump)

#region 3. 计算新版本号
$newMaj = $latestVer.Major
$newMin = $latestVer.Minor
$newPat = $latestVer.Patch

switch ($bump)
{
    "major" {
        $newMaj += 1
        $newMin = 0
        $newPat = 0
    }
    "minor" {
        $newMin += 1
        $newPat = 0
    }
    "patch" {
        $newPat += 1
    }
}
$newTag = "v$newMaj.$newMin.$newPat"
Write-Host "`n将要生成新版本标签: $newTag" -ForegroundColor Yellow

# 二次确认
$confirm = Read-Host "确认继续? (y / n)"
if ($confirm -notmatch '^[Yy]$')
{
    Write-Host "已取消操作"
    exit 0
}

#region 4. 检查本地标签是否已存在
Write-Host "`n[2/6] 校验本地标签是否存在..."
$localExists = git tag --list $newTag
if ($localExists)
{
    Write-Error "本地已存在标签 $newTag，禁止覆盖，终止操作"
    exit 1
}

#region 5. 检查远程origin标签是否已存在
Write-Host "`n[3/6] 校验远程origin标签是否存在..."
git fetch origin --tags
if ($LASTEXITCODE -ne 0)
{
    Write-Warning "git fetch origin --tags 警告，继续校验"
}
$remoteExists = git ls-remote --tags origin $newTag
if ($remoteExists)
{
    Write-Error "远程 origin 已存在标签 $newTag，禁止覆盖，终止操作"
    exit 1
}

#region 6. 创建带注释(annotated)Git标签
Write-Host "`n[4/6] 创建带注释标签 $newTag ..."
$tagMessage = "Release $newTag - Auto bump $bump version"
git tag -a $newTag -m $tagMessage
if ($LASTEXITCODE -ne 0)
{
    Write-Error "创建标签 $newTag 失败"
    exit 1
}
Write-Host "标签创建成功" -ForegroundColor Green

#region 7. 仅推送标签到 origin，不推送分支
Write-Host "`n[5/6] 推送新标签 $newTag 至 origin..."
git push origin $newTag
if ($LASTEXITCODE -ne 0)
{
    Write-Error "推送标签 $newTag 到 origin 失败，请手动排查网络/权限"
    exit 1
}

Write-Host "`n✅ 全部完成！新版本标签 $newTag 已创建并推送到 origin`n" -ForegroundColor Green