param (
    [Parameter(Mandatory)][string]$RepoName
)
$ErrorActionPreference = "Stop"

. "$PSScriptRoot/packages.ps1"

# Updates the third party packages one solution at a time. Passing a solution
# rather than the repository root keeps dotnet-outdated away from the projects
# in submodules.
#
# The packages built here are left out by name. In the default Package mode
# the root Directory.Build.targets turns each FiftyOneReference item into a
# PackageReference, which dotnet-outdated finds but cannot change, because the
# item is in an imported file. Their versions are the family versions in the
# root Directory.Build.props, which publish-package.ps1 records.
$OwnPackagePrefix = 'FiftyOne.'
$solutions = @($Families | ForEach-Object { $_.Solutions + $_.Framework }) + ($ExampleSolutions | ForEach-Object { $_.Framework })
foreach ($solution in $solutions | Where-Object { $_ } | Sort-Object -Unique) {
    ./dotnet/outdated.ps1 -RepoName:$RepoName -Target $solution -ExtraArgs '--exclude', $OwnPackagePrefix
}
