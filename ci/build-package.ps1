param (
    [Parameter(Mandatory)][string]$RepoName,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][hashtable]$Keys,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

. "$PSScriptRoot/packages.ps1"

$RepoPath = [IO.Path]::Combine($pwd, $RepoName)
$PackagesFolder = New-Item -ItemType Directory -Force "$pwd/package"

# The strong name key, so the assemblies are fully signed.
[IO.File]::WriteAllBytes("$RepoPath/51Degrees.snk", [Convert]::FromBase64String($Keys.StrongNameKeyBase64))

# The native libraries every platform built, which the engine projects pack
# from <product>/<os>/<arch>/ as runtimes.
foreach ($built in Get-ChildItem -Path "$pwd/package-files/*" -Recurse -Directory -Include "linux", "windows", "macos") {
    foreach ($engine in $NativeEngines) {
        $product = Split-Path $engine.Project -Parent
        Copy-Item -Path $built -Destination "$RepoPath/$product" -Recurse -Force
    }
}

# Only the families the release plan names are packed, each at its next
# version, which is every family that changed since its last tag and every
# family depending on one of them. Every family's version is passed so a
# package depends on the other families at the versions being published, or
# at the last ones published.
$versions = Get-FamilyVersions -RepoName $RepoName
$properties = Get-VersionProperties -Versions $versions
$packArgs = @(
    "--output=$PackagesFolder"
    "--configuration=$Configuration"
    "-p:FiftyOneReferences=Project"
    "-p:BuiltOnCI=true"
    "-p:ContinuousIntegrationBuild=true"
) + $properties

Push-Location $RepoPath
try {
    foreach ($family in $Families) {
        $v = $versions[$family.Name]
        if (-not $v.Release) {
            Write-Host "$($family.Name) stays at $($v.Last), so it is not packed"
            continue
        }
        Write-Host "::group::Packing $($family.Name) $($v.Next)"
        # Each project is packed on its own, as the separate repositories did,
        # so it builds as Any CPU. Packed through a solution, the engines take
        # the platform the solution maps them to, which is x64, and an Arm64
        # or x86 project cannot reference an x64 assembly. The native
        # libraries are added per platform from package-files.
        foreach ($project in Get-PackableProjects -Solutions $family.Solutions) {
            dotnet pack $project @packArgs
        }
        Write-Host "::endgroup::"
    }

    # The nuspec packages. Only on Windows, where the .NET Framework half
    # builds, with msbuild, which the shared step puts on the path.
    if ($NuspecPackages) {
        Pop-Location
        try { ./environments/setup-msbuild.ps1 } finally { Push-Location $RepoPath }
    }
    foreach ($package in $NuspecPackages) {
        $v = $versions[$package.Family]
        if (-not $v.Release) { continue }
        Write-Host "::group::Packing $($package.Nuspec) $($v.Next)"
        dotnet build $package.Core -c $Configuration -p:FiftyOneReferences=Project -p:BuiltOnCI=true @properties
        msbuild $package.Framework -t:build -restore -verbosity:minimal "-p:Configuration=$Configuration" "-p:Platform=Any CPU" "-p:FiftyOneReferences=Project" "-p:BuiltOnCI=true" @properties
        $env:Version = $v.Next
        nuget pack $package.Nuspec -NonInteractive -OutputDirectory $PackagesFolder -Properties config=$Configuration -version $v.Next
        Write-Host "::endgroup::"
    }
} finally {
    Pop-Location
}

$packages = @(Get-ChildItem "$PackagesFolder/*.nupkg")
if (-not $packages) {
    # The workflow uploads the folder as an artifact, which must not be empty.
    Write-Host "No family is released, so there is nothing to publish."
    Set-Content "$PackagesFolder/dummy.txt" "No packages to publish"
    return
}

# Signed once, after every package is packed. A package signed twice fails
# its own integrity check and NuGet drops it during validation.
Write-Host "Installing NuGetKeyVaultSignTool"
dotnet tool install -g NuGetKeyVaultSignTool
# Each package is signed by its full path. Given a wildcard the tool signs
# whatever it matches, which on the Windows runner was nothing, and still
# succeeds, so an unsigned package reached the verify step below.
foreach ($package in $packages) {
    Write-Host "Signing $($package.Name)"
    NuGetKeyVaultSignTool sign $package.FullName `
        --file-digest sha256 `
        --timestamp-digest sha256 `
        --timestamp-rfc3161 http://rfc3161timestamp.globalsign.com/advanced `
        --azure-key-vault-url $Keys.CodeSigningKeyVaultUrl `
        --azure-key-vault-client-id $Keys.CodeSigningKeyVaultClientId `
        --azure-key-vault-tenant-id $Keys.CodeSigningKeyVaultTenantId `
        --azure-key-vault-client-secret $Keys.CodeSigningKeyVaultClientSecret `
        --azure-key-vault-certificate $Keys.CodeSigningKeyVaultCertificateName
}
foreach ($package in $packages) {
    dotnet nuget verify $package.FullName
}
