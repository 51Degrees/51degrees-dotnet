param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$BuildMethod = "dotnet"
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

. "$PSScriptRoot/packages.ps1"

# Every folder is built against the projects next to it, so a change that
# crosses packages is built and tested together. Package mode is what a
# checkout and the examples use, and what install-package.ps1 tests.
$common = @("-p:FiftyOneReferences=Project", "-p:BuiltOnCI=true", "-c", $Configuration)

Push-Location $RepoName
try {
    foreach ($family in $Families) {
        if (-not (Test-BuiltFor $family $Arch)) {
            Write-Host "$($family.Name) is not built for $Arch"
            continue
        }
        $platform = $family.Platform ? @("-p:Platform=$Arch") : @()
        foreach ($solution in $family.Solutions) {
            Write-Host "::group::Building $solution"
            dotnet build $solution @common @platform
            Write-Host "::endgroup::"
        }
        # The .NET Framework projects, on Windows with msbuild only.
        if ($BuildMethod -eq "msbuild" -and $family.Framework) {
            foreach ($solution in $family.Framework) {
                Write-Host "::group::Building $solution with msbuild"
                msbuild $solution -t:build -restore -verbosity:minimal "-p:Configuration=$Configuration" "-p:Platform=$(Get-SolutionPlatform $family $Arch)" "-p:FiftyOneReferences=Project" "-p:BuiltOnCI=true"
                Write-Host "::endgroup::"
            }
        }
    }
    foreach ($examples in $ExampleSolutions) {
        if (-not (Test-BuiltFor $examples $Arch)) {
            Write-Host "$($examples.Path) is not built for $Arch"
            continue
        }
        $platform = $examples.Platform ? @("-p:Platform=$Arch") : @()
        $solution = ($BuildMethod -eq "msbuild") ? $examples.Framework : $examples.Path
        Write-Host "::group::Building $solution"
        if ($BuildMethod -eq "msbuild") {
            msbuild $solution -t:build -restore -verbosity:minimal "-p:Configuration=$Configuration" "-p:Platform=$(Get-SolutionPlatform $examples $Arch)" "-p:FiftyOneReferences=Project" "-p:BuiltOnCI=true"
        } else {
            dotnet build $solution @common @platform
        }
        Write-Host "::endgroup::"
    }
} finally {
    Pop-Location
}
