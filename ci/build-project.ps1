param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$BuildMethod = "dotnet"
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

# Every warning is an error in the builds 51Degrees runs. Each CI script
# that builds sets this until the shared workflows set it in every job.
$env:_51DEGREES_WARNINGS_AS_ERRORS = "true"

. "$PSScriptRoot/packages.ps1"

# The token of a public key is the last eight bytes of the SHA-1 hash of the
# key, in reverse order.
function Get-PublicKeyToken([string]$Path) {
    $hash = [Security.Cryptography.SHA1]::HashData([IO.File]::ReadAllBytes($Path))
    -join ($hash[-1..-8] | ForEach-Object { $_.ToString('x2') })
}

# Registers the key in the public key file to skip strong name verification
# for every assembly signed with it, in the 64-bit and 32-bit registry, as
# sn.exe -Vr does. Only on a CI runner, where the job runs as an
# administrator and the machine is discarded after the job.
function Register-TestSigningKey([string]$Path) {
    $token = Get-PublicKeyToken $Path
    if ($env:GITHUB_ACTIONS -ne 'true') {
        Write-Host "The assemblies are public signed. To compile the pages here, run sn.exe -Vr *,$token as an administrator first."
        return
    }
    foreach ($view in 'Registry64', 'Registry32') {
        $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey('LocalMachine', $view)
        try {
            $root.CreateSubKey("SOFTWARE\Microsoft\StrongName\Verification\*,$token").Dispose()
        } finally {
            $root.Dispose()
        }
    }
    Write-Host "Registered the public key token $token to skip strong name verification."
}

# A public signed assembly has the strong name signed flag in its CLI header
# but no signature. When the .NET Framework checks a strong name, as it does
# for an assembly ASP.NET has shadow copied, it honors a skip entry only for
# an assembly without that flag. Set-DelaySigned clears the flag in each
# assembly in a folder whose public key is the one in the public key file,
# which leaves the assembly delay signed and covered by the skip entry.
$StrongNameSignedFlag = [int][Reflection.PortableExecutable.CorFlags]::StrongNameSigned
# Where the Flags field starts in the CLI header (IMAGE_COR20_HEADER).
$CorHeaderFlagsOffsetBytes = 16
function Set-DelaySigned([string]$Bin, [string]$PublicKeyPath) {
    $key = [IO.File]::ReadAllBytes($PublicKeyPath)
    foreach ($dll in Get-ChildItem -Path $Bin -Filter *.dll -File) {
        $bytes = [IO.File]::ReadAllBytes($dll.FullName)
        $pe = [Reflection.PortableExecutable.PEReader]::new([IO.MemoryStream]::new($bytes))
        try {
            $header = $pe.PEHeaders.CorHeader
            if (-not $header -or -not ($header.Flags -band $StrongNameSignedFlag)) {
                continue
            }
            $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            if (-not $metadata.IsAssembly) {
                continue
            }
            $assemblyKey = $metadata.GetBlobBytes($metadata.GetAssemblyDefinition().PublicKey)
            if ([Convert]::ToHexString($assemblyKey) -ne [Convert]::ToHexString($key)) {
                continue
            }
            $offset = $pe.PEHeaders.CorHeaderStartOffset + $CorHeaderFlagsOffsetBytes
        } finally {
            $pe.Dispose()
        }
        $flags = [int]$header.Flags -band -bnot $StrongNameSignedFlag
        [BitConverter]::GetBytes($flags).CopyTo($bytes, $offset)
        [IO.File]::WriteAllBytes($dll.FullName, $bytes)
        Write-Host "Marked $($dll.Name) in $Bin as delay signed."
    }
}

# Every folder is built against the projects next to it, so a change that
# crosses packages is built and tested together. Package mode is what a
# checkout and the examples use, and what install-package.ps1 tests.
$common = @("-p:_51DEGREES_REFERENCES=Project", "-p:_51DEGREES_BUILT_ON_CI=true", "-c", $Configuration)

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
                msbuild $solution -t:build -restore -verbosity:minimal "-p:Configuration=$Configuration" "-p:Platform=$(Get-SolutionPlatform $family $Arch)" "-p:_51DEGREES_REFERENCES=Project" "-p:_51DEGREES_BUILT_ON_CI=true"
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
            msbuild $solution -t:build -restore -verbosity:minimal "-p:Configuration=$Configuration" "-p:Platform=$(Get-SolutionPlatform $examples $Arch)" "-p:_51DEGREES_REFERENCES=Project" "-p:_51DEGREES_BUILT_ON_CI=true"
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
    # Only the job that builds the packages has the private key, so here
    # every assembly is public signed. ASP.NET shadow copies a site's
    # assemblies, so the .NET Framework checks their strong name signatures,
    # which a public signed assembly does not have. The copies in each site's
    # bin folder are therefore marked as delay signed and their key is
    # registered to skip verification. That covers this compile and the
    # sites test-framework-sites.ps1 hosts later in the same job.
    # A full path, because .NET reads a relative one from the process's
    # current directory, which the PowerShell location does not move.
    $publicKey = Convert-Path (Join-Path $RepoName "51Degrees.publickey")
    $publicSigned = -not (Test-Path (Join-Path $RepoName "51Degrees.snk"))
    if ($publicSigned) {
        Register-TestSigningKey $publicKey
    }
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
        if ($publicSigned) {
            Set-DelaySigned (Join-Path $path "bin") $publicKey
        }
        $number++
        $target = Join-Path ([IO.Path]::GetTempPath()) "compiled-site-$number"
        Write-Host "::group::Compiling the pages of $site"
        & $compiler -v / -p (Resolve-Path $path).Path -f -errorstack $target
        Write-Host "::endgroup::"
    }
    Write-Host "Compiled the pages of $number .NET Framework sites"
}
