param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Version
)
$ErrorActionPreference = "Stop"

. "$PSScriptRoot/packages.ps1"

# The shared publish runs this in place of its own check, because each family
# has its own version, see packages.ps1. Every family about to be published goes
# through the shared steps/check-version-bump.ps1, run from the common-ci root,
# which refuses a major or minor move unless ALLOW_VERSION_BUMP is 'true'.
#
# A tagged family is compared with its own tags. A family with no tag yet is
# compared with the highest listed version of its package on NuGet, because
# the version Directory.Build.props records is a file anyone can change.

# The package whose NuGet history stands for each family.
$FamilyPackages = @{
    'common'           = 'FiftyOne.Common'
    'caching'          = 'FiftyOne.Caching'
    'pipeline'         = 'FiftyOne.Pipeline.Core'
    'device-detection' = 'FiftyOne.DeviceDetection'
    'ip-intelligence'  = 'FiftyOne.IpIntelligence'
    'location'         = 'FiftyOne.GeoLocation'
}

# The highest listed, stable version of a package on NuGet, or $null when it
# has none. Unlisted versions are left out, because NuGet no longer offers them.
function Get-ListedNuGetVersion {
    param ([Parameter(Mandatory)][string]$PackageId)
    $index = "https://api.nuget.org/v3/registration5-gz-semver2/$($PackageId.ToLowerInvariant())/index.json"
    try {
        $registration = Invoke-RestMethod -Uri $index
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 404) { return $null }
        throw
    }
    $highest = $null
    foreach ($page in $registration.items) {
        $items = $page.items ?? (Invoke-RestMethod -Uri $page.'@id').items
        foreach ($item in $items) {
            $entry = $item.catalogEntry
            if ($entry.listed -eq $false -or $entry.version -notmatch '^\d+\.\d+\.\d+$') {
                continue
            }
            if ($null -eq $highest -or [version]$entry.version -gt $highest) {
                $highest = [version]$entry.version
            }
        }
    }
    return $highest
}

Write-Host "The publish worked out $Version, the pipeline family's. Each family is checked on its own."

$refused = @()
$versions = Get-FamilyVersions -RepoName $RepoName
foreach ($name in $versions.Keys) {
    $family = $versions[$name]
    if (-not $family.Changed) {
        Write-Host "$name is unchanged since its last tag, so it is not published."
        continue
    }
    $global:LASTEXITCODE = 0
    if (@(git -C $RepoName tag --list "$name/*").Count -gt 0) {
        ./steps/check-version-bump.ps1 -Version $family.Next -RepoName $RepoName -TagPrefix $name
    } else {
        $package = $FamilyPackages[$name]
        if (-not $package) {
            throw "No NuGet package stands for the $name family, so its version cannot be checked."
        }
        $released = Get-ListedNuGetVersion -PackageId $package
        if ($null -eq $released) {
            Write-Host "::error title=First release refused::$name has no tag here and $package has no listed version on NuGet, so nothing has decided where its versions start."
            $refused += $name
            continue
        }
        ./steps/check-version-bump.ps1 -Version $family.Next -ReleasedVersion "$released"
    }
    if ($LASTEXITCODE -ne 0) {
        $refused += $name
    }
}

if ($refused) {
    Write-Host "Nothing is published, because these families were refused: $($refused -join ', ')."
    exit 1
}
exit 0
