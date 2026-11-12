# Shared by the scripts in this folder, which dot-source it. Paths are
# relative to the repository root.

# Which families a publish releases, and at which version, is worked out by
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

# Each package family keeps the version stream its own repository had, and is
# published from here at the next version of that stream. A family's release
# is the tag '<family>/<version>', for example 'pipeline/4.5.129'.
#
# packages.json lists the families in the order they build, each after the
# families it depends on. Name, Path, ExtraPaths, DependsOn, Publish and
# Ignore are the fields common-ci's release plan reads. Path is the family's
# folder. Ignore holds the globs of files whose change releases nothing, on top
# of the documentation, test and example folders the plan always leaves out.
# It names the performance test projects, which are never packed.
# ExtraPaths holds the root Directory.Build.targets, which every package
# builds with, and the cxx submodule the on-premise engines compile. The root
# Directory.Build.props is left out, because the publish records the released
# versions there, which would otherwise count as a change to every family.
# DependsOn names the families whose packages the family's packages reference
# through FiftyOneReference items. The other fields are this repository's:
#
#   Property   the MSBuild property in the root Directory.Build.props that
#              holds the last published version, and which Package mode and
#              the examples restore against
#   Solutions  what dotnet builds and packs for the family, in dependency order
#   Framework  the .NET Framework solutions, which need msbuild on Windows
#   Platform   whether the solutions take a Platform (the on-premise engines).
#              The others build as Any CPU, which is all their solutions define.
#   NotOn      the architectures the family is not built for. IP Intelligence
#              ships no x86 library, as its own repository never did.
$Families = @(Get-Content -Raw "$PSScriptRoot/packages.json" | ConvertFrom-Json)

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

# The one package built from a nuspec rather than a project, because it
# bundles the .NET and .NET Framework assemblies together.
$NuspecPackages = @(
    @{ Nuspec = 'pipeline/FiftyOne.Pipeline.Web.nuspec'; Family = 'pipeline'; Core = 'pipeline/Web/FiftyOne.Pipeline.Web.csproj'; Framework = 'pipeline/FiftyOne.Pipeline.Web.sln' }
)

# Returns, for every family, the last published version, the version this
# publish gives it, whether it is released and why. The released families are
# common-ci's release plan, which is every family that changed since its last
# tag and every family depending on one of them, so that the dependent's
# packages require the new version. The others keep their last version, which
# the released packages then require. Deterministic for a commit, so every job
# of a run computes the same answer.
function Get-FamilyVersions {
    param ([Parameter(Mandatory)][string]$RepoName)
    if (-not (Get-Command Get-ReleasePlan -ErrorAction SilentlyContinue)) {
        throw "common-ci's steps/release-plan.ps1 was not found, so the " +
            "release cannot be planned. Set COMMON_CI to a checkout of common-ci."
    }
    $plan = @(Get-ReleasePlan $RepoName $Families)
    $versions = [ordered]@{}
    foreach ($family in $Families) {
        $release = $plan | Where-Object Name -eq $family.Name
        if ($release) {
            $last = $release.Previous
            $next = $release.Version
            $reason = $release.Reason
        } else {
            $last = @(Get-ReleasedVersions $RepoName $family.Name)[-1]
            if ($null -eq $last) {
                throw "$($family.Name) has no '$($family.Name)/<version>' tag."
            }
            $next = $last
            $reason = 'unchanged, as is every family it depends on'
        }
        $versions[$family.Name] = @{
            Last = "$last"
            Next = "$next"
            Release = $null -ne $release
            Reason = $reason
            Property = $family.Property
        }
    }
    return $versions
}

# The MSBuild properties that set every family's version for a build, so a
# project packs with its own version and depends on the others at theirs.
function Get-VersionProperties {
    param ([Parameter(Mandatory)]$Versions)
    return @($Versions.Values | ForEach-Object { "-p:$($_.Property)=$($_.Next)" })
}

# Whether a family or example solution is built for an architecture.
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
