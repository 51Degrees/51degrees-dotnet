param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Version
)
$ErrorActionPreference = "Stop"

. "$PSScriptRoot/packages.ps1"

# The shared publish runs this in place of its own check, which looks for a
# tag of the run's one version. That version is the Pipeline API core
# package's, whose tag already exists when it is not released, so the shared
# check would publish nothing even with another package to release. Exits 0
# when the plan releases a package and 1 when it releases none, as the shared
# check does. $Version is not used.
$versions = Get-PackageVersions -RepoName $RepoName
$released = @($versions.Keys | Where-Object { $versions[$_].Release })
if ($released.Count -eq 0) {
    Write-Output "No package is released, so there is nothing to publish"
    exit 1
}
Write-Output "This publish releases $($released -join ', ')"
exit 0
