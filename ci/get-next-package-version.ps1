param (
    [Parameter(Mandatory)][string]$RepoName,
    [Parameter(Mandatory)][string]$VariableName
)
$ErrorActionPreference = "Stop"

. "$PSScriptRoot/packages.ps1"

# Each family has its own version, see packages.ps1. The shared workflow
# wants one version for the run, and gets the Pipeline API's, which every
# other family builds on. package-update-required.ps1 decides whether there is
# anything to publish, and publish-package.ps1 tags each family itself, so the
# shared tag step is skipped there.
#
# The plan is printed in the order the families build, each after the
# families it depends on.
$versions = Get-FamilyVersions -RepoName $RepoName
$format = "{0,-18} {1,-8} {2,-8} {3,-9} {4}"
Write-Host ($format -f 'Family', 'Last', 'Next', 'Released', 'Why')
foreach ($name in $versions.Keys) {
    $v = $versions[$name]
    Write-Host ($format -f $name, $v.Last, $v.Next, ($v.Release ? 'yes' : 'no'), $v.Reason)
}

Set-Variable -Scope Global -Name $VariableName -Value $versions['pipeline'].Next
