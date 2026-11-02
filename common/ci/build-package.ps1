param(
    [string]$ProjectDir = ".",
    [Parameter(Mandatory=$true)]
    [string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [Parameter(Mandatory=$true)]
    [string]$Version,
    [Parameter(Mandatory=$true)]
    [Hashtable]$Keys

)

[IO.File]::WriteAllBytes("$PSScriptRoot/../51Degrees.snk", [Convert]::FromBase64String($Keys.StrongNameKeyBase64))

./dotnet/build-package-nuget.ps1 -RepoName $RepoName -ProjectDir $ProjectDir -Name $Name -Configuration $Configuration -Version $Version -SolutionName "FiftyOne.Common.sln" -SearchPattern "^Project\(.*csproj" `
    -CodeSigningKeyVaultUrl $Keys.CodeSigningKeyVaultUrl `
    -CodeSigningKeyVaultClientId $Keys.CodeSigningKeyVaultClientId `
    -CodeSigningKeyVaultTenantId $Keys.CodeSigningKeyVaultTenantId `
    -CodeSigningKeyVaultClientSecret $Keys.CodeSigningKeyVaultClientSecret `
    -CodeSigningKeyVaultCertificateName $Keys.CodeSigningKeyVaultCertificateName


exit $LASTEXITCODE
