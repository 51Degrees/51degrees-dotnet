# Shared by the scripts in this folder, which dot-source it. Paths are
# relative to the repository root.

# Each package family keeps the version stream its own repository had, and is
# published from here at the next version of that stream. A family's release
# is the tag '<family>/<version>', for example 'pipeline/4.5.129'.
#
#   Name       the family, which is also the tag prefix
#   Property   the MSBuild property in the root Directory.Build.props that
#              holds the last published version, and which Package mode and
#              the examples restore against
#   Paths      the paths a change in which means the family has changed since
#              its last tag, so a new version is published
#   Solutions  what dotnet builds and packs for the family, in dependency order
#   Framework  the .NET Framework solutions, which need msbuild on Windows
#   Platform   whether the solutions take a Platform (the on-premise engines).
#              The others build as Any CPU, which is all their solutions define.
#   NotOn      the architectures the family is not built for. IP Intelligence
#              ships no x86 library, as its own repository never did.
$Families = @(
    @{ Name = 'common';           Property = 'FiftyOneCommonVersion';          Paths = @('common');                 Solutions = @('common/FiftyOne.Common.sln') }
    @{ Name = 'caching';          Property = 'FiftyOneCachingVersion';         Paths = @('caching');                Solutions = @('caching/FiftyOne.Caching.sln') }
    @{ Name = 'pipeline';         Property = 'FiftyOnePipelineVersion';        Paths = @('pipeline');               Solutions = @('pipeline/FiftyOne.Pipeline.sln', 'pipeline/FiftyOne.Pipeline.Elements.sln', 'pipeline/FiftyOne.CloudRequestEngine.sln'); Framework = @('pipeline/FiftyOne.Pipeline.Web.sln') }
    @{ Name = 'device-detection'; Property = 'FiftyOneDeviceDetectionVersion'; Paths = @('device-detection', 'cxx'); Solutions = @('device-detection/FiftyOne.DeviceDetection.Core.slnf'); Framework = @('device-detection/FiftyOne.DeviceDetection.sln'); Platform = $true }
    @{ Name = 'ip-intelligence';  Property = 'FiftyOneIpIntelligenceVersion';  Paths = @('ip-intelligence', 'cxx');  Solutions = @('ip-intelligence/FiftyOne.IpIntelligence.sln'); Platform = $true; NotOn = @('x86') }
    @{ Name = 'location';         Property = 'FiftyOneGeoLocationVersion';     Paths = @('location');               Solutions = @('location/FiftyOne.GeoLocation.sln') }
)

# A change to a root build file is a change to every family.
$RootPaths = @('Directory.Build.targets')

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

# Returns, for every family, the last published version, the next version and
# whether anything in the family changed since the last tag. Deterministic for
# a commit, so every job of a run computes the same answer.
#
# A family with no tag yet is published at the version after the one the root
# Directory.Build.props records, which is the last version its own repository
# published. After that the tag is the record.
function Get-FamilyVersions {
    param ([Parameter(Mandatory)][string]$RepoName)
    $props = Get-Content "$RepoName/Directory.Build.props" -Raw
    $tags = @(git -C $RepoName tag --list)
    $versions = [ordered]@{}
    foreach ($family in $Families) {
        $released = @($tags |
            Where-Object { $_ -match "^$($family.Name)/(\d+\.\d+\.\d+)$" } |
            ForEach-Object { [version]$Matches[1] } |
            Sort-Object)
        if ($released) {
            $last = $released[-1]
            $tag = "$($family.Name)/$last"
            $changes = @(git -C $RepoName log --oneline "$tag..HEAD" -- @($family.Paths + $RootPaths))
            $changed = $changes.Count -gt 0
        } else {
            if ($props -notmatch "<$($family.Property)>(\d+\.\d+\.\d+)</$($family.Property)>") {
                throw "Directory.Build.props has no $($family.Property)"
            }
            $last = [version]$Matches[1]
            $changed = $true
        }
        $next = $changed ? [version]"$($last.Major).$($last.Minor).$($last.Build + 1)" : $last
        $versions[$family.Name] = @{ Last = "$last"; Next = "$next"; Changed = $changed; Property = $family.Property }
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
