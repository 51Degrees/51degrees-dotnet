param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$BuildMethod = "dotnet",
    [string]$Version
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

. "$PSScriptRoot/packages.ps1"

# Puts the packages about to be published where a restore finds them, and
# rebuilds the examples in Package mode against them, at the versions being
# published. A family that did not change restores from nuget.org at its last
# version, as a customer would. run-integration-tests.ps1 then tests those
# builds. When no family is released there are no packages, and every family
# restores from nuget.org, so the examples are still built and tested as a
# customer gets them.
# The publish job installs too, before it pushes, and needs nothing built.
if ($env:GITHUB_JOB -eq "Package") {
    Write-Host "Nothing to install before publishing"
    return
}
$packages = @(Get-ChildItem "$pwd/package/*.nupkg" -ErrorAction SilentlyContinue)
if ($packages) {
    dotnet nuget add source "$pwd/package" --name fiftyone-local
} else {
    Write-Host "No packages were built, so the examples use the published ones"
}

$versions = Get-FamilyVersions -RepoName $RepoName
$properties = Get-VersionProperties -Versions $versions
Push-Location $RepoName
try {
    foreach ($examples in $ExampleSolutions) {
        if (-not (Test-BuiltFor $examples $Arch)) {
            Write-Host "$($examples.Path) is not built for $Arch"
            continue
        }
        $platform = $examples.Platform ? @("-p:Platform=$Arch") : @()
        $solution = ($BuildMethod -eq "msbuild") ? $examples.Framework : $examples.Path
        Write-Host "::group::Building $solution against the packages"
        if ($BuildMethod -eq "msbuild") {
            msbuild $solution -t:build -restore -verbosity:minimal "-p:Configuration=$Configuration" "-p:Platform=$(Get-SolutionPlatform $examples $Arch)" "-p:FiftyOneReferences=Package" "-p:BuiltOnCI=true" @properties
        } else {
            dotnet build $solution -c $Configuration -p:FiftyOneReferences=Package -p:BuiltOnCI=true @properties @platform
        }
        Write-Host "::endgroup::"
    }
} finally {
    Pop-Location
}
