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

# The pages of a .NET Framework site are compiled by ASP.NET when they are
# first asked for, which no build does, so a fault in a page would show only
# to whoever opened it. Each site msbuild has built is compiled here as
# ASP.NET would, into a folder that is not kept. The x86 job is left out,
# because the compiler has only been shown to load the x64 builds.
if ($BuildMethod -eq "msbuild" -and $Arch -eq "x64") {
    $compiler = Join-Path $env:windir "Microsoft.NET/Framework64/v4.0.30319/aspnet_compiler.exe"
    $sites = @(git -C $RepoName ls-files |
        Where-Object { $_ -match '(^|/)Global\.asax$' } |
        ForEach-Object { Split-Path $_ -Parent })
    $number = 0
    foreach ($site in $sites) {
        $path = Join-Path $RepoName $site
        if (-not (Test-Path (Join-Path $path "bin"))) {
            Write-Host "$site was not built, so its pages are not compiled"
            continue
        }
        $number++
        $target = Join-Path ([IO.Path]::GetTempPath()) "compiled-site-$number"
        Write-Host "::group::Compiling the pages of $site"
        & $compiler -v / -p (Resolve-Path $path).Path -f -errorstack $target
        Write-Host "::endgroup::"
    }
    Write-Host "Compiled the pages of $number .NET Framework sites"
}
