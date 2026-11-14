# Shared by the scripts in this folder, which dot-source it. Paths are
# relative to the repository root.

# Which packages a publish releases, and at which version, is worked out by
# common-ci's steps/release-plan.ps1 from packages.json. common-ci runs these
# scripts from its own root, with this repository checked out inside it. A
# checkout of common-ci beside this repository is found too, or one elsewhere
# can be named with COMMON_CI. Only the publish asks for the plan, so a build
# or a test runs without it.
$releasePlan = @(
    $env:COMMON_CI,
    (Join-Path $PSScriptRoot '..' '..'),
    (Join-Path $PSScriptRoot '..' '..' 'common-ci')) |
    Where-Object { $_ } |
    ForEach-Object { Join-Path $_ 'steps' 'release-plan.ps1' } |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1
if ($releasePlan) {
    . $releasePlan
}
Remove-Variable releasePlan

# Each NuGet package has its own version, continuing from the version it last
# published, and each release of it is the tag '<package>/<version>', for
# example 'FiftyOne.Pipeline.Core/4.5.131'. A publish releases a package when
# a file that counts under its folders changed since its last tag, together
# with every package that depends on one it releases, and only the patch
# number moves.
#
# packages.json lists the packages, each after the packages it depends on.
# Name, Path, ExtraPaths, DependsOn and Publish are the fields common-ci's
# release plan reads. Name is the NuGet package id and Path the folder of the
# project the package is built from. ExtraPaths holds what the package builds
# with outside that folder: the root Directory.Build.targets, its family's
# Directory.Build.props, global.json and Images, and for some packages a folder
# they compile, such as the cxx submodule of the on-premise engines. The root
# Directory.Build.props is left out, because the publish records the released
# versions there, which would otherwise count as a change to every package.
# DependsOn names the packages its projects reference. The other fields are
# this repository's:
#
#   Family    the folder of families.json the package is built in
#   Project   the project dotnet packs
#   Nuspec    for a package packed from a nuspec, the nuspec, with Projects
#             the projects whose assemblies it holds and Solution the .NET
#             Framework solution msbuild builds them with
#   Property  the MSBuild property in the root Directory.Build.props holding
#             the package's last published version, which its own projects
#             and every project referencing it read
$Packages = @(Get-Content -Raw "$PSScriptRoot/packages.json" | ConvertFrom-Json)

# families.json lists the folders that came from separate repositories, in
# the order they build, with what dotnet builds for each:
#
#   Solutions  what dotnet builds for the folder, in dependency order
#   Framework  the .NET Framework solutions, which need msbuild on Windows
#   Platform   whether the solutions take a Platform (the on-premise engines).
#              The others build as Any CPU, which is all their solutions define.
#   NotOn      the architectures the folder is not built for. IP Intelligence
#              ships no x86 library, as its own repository never did.
$Families = @(Get-Content -Raw "$PSScriptRoot/families.json" | ConvertFrom-Json)

# The example solutions, built and tested but never packed.
$ExampleSolutions = @(
    @{ Path = 'examples/device-detection/FiftyOne.DeviceDetection.Examples.Core.slnf'; Framework = 'examples/device-detection/FiftyOne.DeviceDetection.Examples.sln'; Platform = $true }
    @{ Path = 'examples/ip-intelligence/FiftyOne.IpIntelligence.Examples.Core.slnf';   Framework = 'examples/ip-intelligence/FiftyOne.IpIntelligence.Examples.sln';   Platform = $true; NotOn = @('x86') }
)

# The engines with a native library, built once per platform before packing.
$NativeEngines = @(
    @{ Project = 'device-detection/Hash.Engine.OnPremise'; Library = 'FiftyOne.DeviceDetection.Hash.Engine.OnPremise.Native.dll' }
    @{ Project = 'ip-intelligence/Engine.OnPremise';        Library = 'FiftyOne.IpIntelligence.Engine.OnPremise.Native.dll' }
)

# Until each package had its own version, every package of a family was
# published at the family's version and tagged '<family>/<version>' once. A
# package with no tag of its own continues from its family's last tag: this
# tags it '<package>/<version>' at the family tag's commit, in this checkout
# only. Only a package whose project was there at that commit was published
# then, so a package added since gets no tag and starts from the FirstVersion
# packages.json gives it.
function Add-FamilyStartTags {
    param (
        [Parameter(Mandatory)][string]$RepoPath,
        [Parameter(Mandatory)]$Packages
    )
    # A missing file is an answer here, not a failure.
    $PSNativeCommandUseErrorActionPreference = $false
    foreach ($package in $Packages) {
        if (@(Get-ReleasedVersions $RepoPath $package.Name).Count -gt 0) {
            continue
        }
        $family = @(Get-ReleasedVersions $RepoPath $package.Family)[-1]
        if ($null -eq $family) {
            continue
        }
        $familyTag = "$($package.Family)/$family"
        $project = $package.Project ?? $package.Nuspec
        & git -C $RepoPath cat-file -e "${familyTag}:$project" 2>$null
        if ($LASTEXITCODE -ne 0) {
            continue
        }
        & git -C $RepoPath tag "$($package.Name)/$family" "$familyTag^{commit}"
        if ($LASTEXITCODE -ne 0) {
            throw "Could not tag $($package.Name) at $familyTag."
        }
    }
    $global:LASTEXITCODE = 0
}

# Returns, for every package in the order of packages.json, the last published
# version, the version this publish gives it, whether it is released and why,
# with its Property and its packages.json entry. The released packages are
# common-ci's release plan, which is every package that changed since its last
# tag and every package depending on one of them, so that the dependant
# requires the new version. The others keep their last version, which the
# released packages then require. Deterministic for a commit, so every job of
# a run computes the same answer.
function Get-PackageVersions {
    param ([Parameter(Mandatory)][string]$RepoName)
    if (-not (Get-Command Get-ReleasePlan -ErrorAction SilentlyContinue)) {
        throw "common-ci's steps/release-plan.ps1 was not found, so the " +
            "release cannot be planned. Set COMMON_CI to a checkout of common-ci."
    }
    Add-FamilyStartTags -RepoPath $RepoName -Packages $Packages
    $plan = @(Get-ReleasePlan $RepoName $Packages)
    $versions = [ordered]@{}
    foreach ($package in $Packages) {
        $release = $plan | Where-Object Name -eq $package.Name
        if ($release) {
            $last = $release.Previous
            $next = $release.Version
            $reason = $release.Reason
        } else {
            $last = @(Get-ReleasedVersions $RepoName $package.Name)[-1]
            if ($null -eq $last) {
                throw "$($package.Name) has no '$($package.Name)/<version>' tag."
            }
            $next = $last
            $reason = 'unchanged, as is every package it depends on'
        }
        $versions[$package.Name] = @{
            Last = "$last"
            Next = "$next"
            Release = $null -ne $release
            Reason = $reason
            Property = $package.Property
            Package = $package
        }
    }
    return $versions
}

# The MSBuild properties that set every package's version for a build, so a
# project packs with its own version and depends on the others at theirs.
function Get-VersionProperties {
    param ([Parameter(Mandatory)]$Versions)
    return @($Versions.Values | ForEach-Object { "-p:$($_.Property)=$($_.Next)" })
}

# Whether a family's or the examples' solutions are built for an architecture.
function Test-BuiltFor {
    param ([Parameter(Mandatory)]$Item, [Parameter(Mandatory)][string]$Arch)
    return -not ($Item.NotOn -and ($Item.NotOn -contains $Arch))
}

# The msbuild platform of a solution: the job's architecture for the engine
# solutions, Any CPU for the rest.
function Get-SolutionPlatform {
    param ([Parameter(Mandatory)]$Item, [Parameter(Mandatory)][string]$Arch)
    return $Item.Platform ? $Arch : "Any CPU"
}

# The package projects of the given solutions and solution filters, as paths
# relative to the repository root. Tests, examples, tools and performance
# tests are left out, as they are never packed.
function Get-PackableProjects {
    param ([Parameter(Mandatory)][string[]]$Solutions)
    $projects = foreach ($solution in $Solutions) {
        $folder = Split-Path $solution -Parent
        if ($solution -like '*.slnf') {
            $filter = Get-Content $solution -Raw | ConvertFrom-Json
            $base = Join-Path $folder (Split-Path $filter.solution.path -Parent)
            $filter.solution.projects | ForEach-Object { Join-Path $base $_ }
        } else {
            Select-String -Path $solution -Pattern '^Project\("[^"]+"\)\s*=\s*"[^"]+",\s*"([^"]+\.csproj)"' |
                ForEach-Object { Join-Path $folder $_.Matches[0].Groups[1].Value }
        }
    }
    $projects |
        ForEach-Object { $_.Replace('\', '/') } |
        Where-Object { "/$_" -notmatch '/(Tests|Examples|examples|Tools|performance-tests)/' -and $_ -notmatch '\.Tests?\.csproj$' } |
        Sort-Object -Unique
}
