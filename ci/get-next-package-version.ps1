param (
    [Parameter(Mandatory)][string]$RepoName,
    [Parameter(Mandatory)][string]$VariableName
)
$ErrorActionPreference = "Stop"

. "$PSScriptRoot/packages.ps1"

# Each family has its own version, see packages.ps1. The shared workflow
# wants one version for the run, and gets the Pipeline API's, which every
# other family builds on. publish-package.ps1 tags each family itself, so the
# shared tag step is skipped there.
$versions = Get-FamilyVersions -RepoName $RepoName
foreach ($name in $versions.Keys) {
    $v = $versions[$name]
    Write-Host ("{0,-18} last {1,-10} next {2,-10} {3}" -f $name, $v.Last, $v.Next, ($v.Changed ? 'changed' : 'unchanged'))
}

Set-Variable -Scope Global -Name $VariableName -Value $versions['pipeline'].Next
