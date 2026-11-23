param (
    [Parameter(Mandatory)][string]$RepoName,
    [Parameter(Mandatory)][string]$VariableName
)
$ErrorActionPreference = "Stop"

. "$PSScriptRoot/packages.ps1"

# Each package has its own version, see packages.ps1. The shared workflow
# wants one version for the run, and gets the Pipeline API core package's,
# which most other packages build on. package-update-required.ps1 decides
# whether there is anything to publish, and publish-package.ps1 tags each
# package itself, so the shared tag step is skipped there.
#
# The plan is printed in the order of packages.json, each package after the
# packages it depends on.
$versions = Get-PackageVersions -RepoName $RepoName
$format = "{0,-48} {1,-8} {2,-8} {3,-9} {4}"
Write-Host ($format -f 'Package', 'Last', 'Next', 'Released', 'Why')
foreach ($name in $versions.Keys) {
    $v = $versions[$name]
    Write-Host ($format -f $name, $v.Last, $v.Next, ($v.Release ? 'yes' : 'no'), $v.Reason)
}

$released = @($versions.Values | Where-Object Release)
Write-Host "This publish releases $($released.Count) of $($versions.Count) packages."
Set-Variable -Scope Global -Name $VariableName -Value $versions['FiftyOne.Pipeline.Core'].Next
