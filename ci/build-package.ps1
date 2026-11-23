param (
    [Parameter(Mandatory)][string]$RepoName,
    [Parameter(Mandatory)][string]$Version,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

# The job that signs the packages has the signing secrets under these names,
# and nothing is packed unless every one is set.
foreach ($name in 'STRONG_NAME_KEY_BASE64',
    'CODE_SIGNING_KEY_VAULT_URL', 'CODE_SIGNING_KEY_VAULT_CLIENT_ID',
    'CODE_SIGNING_KEY_VAULT_TENANT_ID',
    'CODE_SIGNING_KEY_VAULT_CLIENT_SECRET',
    'CODE_SIGNING_KEY_VAULT_CERTIFICATE_NAME') {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
        throw "$name is not set, so the packages cannot be signed."
    }
}

. "$PSScriptRoot/packages.ps1"

$RepoPath = [IO.Path]::Combine($pwd, $RepoName)
$PackagesFolder = New-Item -ItemType Directory -Force "$pwd/package"

# The strong name key, so the assemblies are fully signed.
[IO.File]::WriteAllBytes("$RepoPath/51Degrees.snk", [Convert]::FromBase64String($env:STRONG_NAME_KEY_BASE64))

# The native libraries every platform built, which the engine projects pack
# from <product>/<os>/<arch>/ as runtimes.
foreach ($built in Get-ChildItem -Path "$pwd/package-files/*" -Recurse -Directory -Include "linux", "windows", "macos") {
    foreach ($engine in $NativeEngines) {
        $product = Split-Path $engine.Project -Parent
        Copy-Item -Path $built -Destination "$RepoPath/$product" -Recurse -Force
    }
}

# Only the packages the release plan names are packed, each at its next
# version, which is every package that changed since its last tag and every
# package depending on one of them. Every package's version is passed so a
# package depends on the others at the versions being published, or at the
# last ones published.
$versions = Get-PackageVersions -RepoName $RepoName
$properties = Get-VersionProperties -Versions $versions
$packArgs = @(
    "--output=$PackagesFolder"
    "--configuration=$Configuration"
    "-p:_51DEGREES_REFERENCES=Project"
    "-p:_51DEGREES_BUILT_ON_CI=true"
    "-p:ContinuousIntegrationBuild=true"
) + $properties

Push-Location $RepoPath
try {
    # Each project is packed on its own, as the separate repositories did,
    # so it builds as Any CPU. Packed through a solution, the engines take the
    # platform the solution maps them to, which is x64, and an Arm64 or x86
    # project cannot reference an x64 assembly. The native libraries are
    # added per platform from package-files.
    foreach ($name in $versions.Keys) {
        $v = $versions[$name]
        if ($v.Package.Nuspec) {
            continue
        }
        if (-not $v.Release) {
            Write-Host "$name stays at $($v.Last), so it is not packed"
            continue
        }
        Write-Host "::group::Packing $name $($v.Next)"
        dotnet pack $v.Package.Project @packArgs
        Write-Host "::endgroup::"
    }

    # The nuspec packages. Only on Windows, where the .NET Framework half
    # builds, with msbuild, which the shared step puts on the path. The
    # nuspec names each FiftyOne dependency's version by its property.
    $nuspecs = @($versions.Values | Where-Object { $_.Package.Nuspec })
    if (@($nuspecs | Where-Object Release).Count -gt 0) {
        Pop-Location
        try { ./environments/setup-msbuild.ps1 } finally { Push-Location $RepoPath }
    }
    $nuspecProperties = @("config=$Configuration") +
        @($versions.Values | ForEach-Object { "$($_.Property)=$($_.Next)" })
    foreach ($v in $nuspecs) {
        $package = $v.Package
        if (-not $v.Release) {
            Write-Host "$($package.Name) stays at $($v.Last), so it is not packed"
            continue
        }
        Write-Host "::group::Packing $($package.Nuspec) $($v.Next)"
        dotnet build $package.Projects[0] -c $Configuration -p:_51DEGREES_REFERENCES=Project -p:_51DEGREES_BUILT_ON_CI=true @properties
        msbuild $package.Solution -t:build -restore -verbosity:minimal "-p:Configuration=$Configuration" "-p:Platform=Any CPU" "-p:_51DEGREES_REFERENCES=Project" "-p:_51DEGREES_BUILT_ON_CI=true" @properties
        nuget pack $package.Nuspec -NonInteractive -OutputDirectory $PackagesFolder -Properties ($nuspecProperties -join ';') -version $v.Next
        Write-Host "::endgroup::"
    }
} finally {
    Pop-Location
}

$packages = @(Get-ChildItem "$PackagesFolder/*.nupkg")
if (-not $packages) {
    # The workflow uploads the folder as an artifact, which must not be empty.
    Write-Host "No package is released, so there is nothing to publish."
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
        --azure-key-vault-url $env:CODE_SIGNING_KEY_VAULT_URL `
        --azure-key-vault-client-id $env:CODE_SIGNING_KEY_VAULT_CLIENT_ID `
        --azure-key-vault-tenant-id $env:CODE_SIGNING_KEY_VAULT_TENANT_ID `
        --azure-key-vault-client-secret $env:CODE_SIGNING_KEY_VAULT_CLIENT_SECRET `
        --azure-key-vault-certificate $env:CODE_SIGNING_KEY_VAULT_CERTIFICATE_NAME
}
foreach ($package in $packages) {
    dotnet nuget verify $package.FullName
}
