param (
    [Parameter(Mandatory)][string]$RepoName
)
$ErrorActionPreference = "Stop"

. "$PSScriptRoot/packages.ps1"

# Updates the third party packages one solution at a time. Passing a solution
# rather than the repository root keeps dotnet-outdated away from the projects
# in submodules, and the FiftyOne packages are referenced through
# FiftyOneReference items, which it does not see, so the family versions in
# the root Directory.Build.props stay as publish-package.ps1 records them.
$solutions = @($Families | ForEach-Object { $_.Solutions + $_.Framework }) + ($ExampleSolutions | ForEach-Object { $_.Framework })
foreach ($solution in $solutions | Where-Object { $_ } | Sort-Object -Unique) {
    ./dotnet/outdated.ps1 -RepoName:$RepoName -Target $solution
}
