# Tests for how the package build and the publish read their secrets, run
# with Pester 5 or later.
#
# Each script runs in a child PowerShell from an empty folder, with the
# signing and publishing names removed from the environment, so it stops at
# the check for the secret it needs before it builds or pushes anything.

BeforeAll {
    $script:BuildPackage = Join-Path $PSScriptRoot 'build-package.ps1'
    $script:PublishPackage = Join-Path $PSScriptRoot 'publish-package.ps1'
    $script:SigningNames = @(
        '_51DEGREES_SIGN_STRONG_NAME_KEY_BASE64',
        '_51DEGREES_SIGN_KEY_VAULT_URL',
        '_51DEGREES_SIGN_KEY_VAULT_CLIENT_ID',
        '_51DEGREES_SIGN_KEY_VAULT_TENANT_ID',
        '_51DEGREES_SIGN_KEY_VAULT_CLIENT_SECRET',
        '_51DEGREES_SIGN_KEY_VAULT_CERTIFICATE_NAME')
    $script:PublishName = '_51DEGREES_PUBLISH_NUGET_API_KEY'

    function Invoke-Script([string]$Path, [string[]]$Arguments) {
        $folder = Join-Path ([IO.Path]::GetTempPath()) "signing-$([Guid]::NewGuid())"
        New-Item -ItemType Directory -Path (Join-Path $folder 'package') | Out-Null
        New-Item -ItemType File -Path (Join-Path $folder 'package' 'a.nupkg') | Out-Null
        Push-Location $folder
        try {
            $output = & pwsh -NoProfile -NonInteractive -File $Path @Arguments 2>&1
            [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output | Out-String) }
        } finally {
            Pop-Location
            Remove-Item -Recurse -Force -LiteralPath $folder -ErrorAction SilentlyContinue
        }
    }
}

Describe 'the secrets the package build and the publish read' {
    BeforeEach {
        $script:Saved = @(Get-ChildItem env: | Where-Object {
            $_.Name -like '_51DEGREES_SIGN_*' -or $_.Name -like '_51DEGREES_PUBLISH_*' })
        $script:Saved | ForEach-Object { Remove-Item "Env:$($_.Name)" }
    }

    AfterEach {
        Get-ChildItem env: | Where-Object {
            $_.Name -like '_51DEGREES_SIGN_*' -or $_.Name -like '_51DEGREES_PUBLISH_*' } |
            ForEach-Object { Remove-Item "Env:$($_.Name)" }
        $script:Saved | ForEach-Object { Set-Item "Env:$($_.Name)" $_.Value }
    }

    It 'stops the package build naming the strong name key when no signing secret is set' {
        $result = Invoke-Script $BuildPackage @('-RepoName', 'repo', '-Version', '4.5.0')

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match '_51DEGREES_SIGN_STRONG_NAME_KEY_BASE64 is not set'
    }

    It 'stops the package build naming the one signing secret that is not set' {
        foreach ($name in $SigningNames) { Set-Item "Env:$name" 'value' }
        Remove-Item Env:_51DEGREES_SIGN_KEY_VAULT_CERTIFICATE_NAME

        $result = Invoke-Script $BuildPackage @('-RepoName', 'repo', '-Version', '4.5.0')

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match '_51DEGREES_SIGN_KEY_VAULT_CERTIFICATE_NAME is not set'
        $result.Output | Should -Not -Match '_51DEGREES_SIGN_KEY_VAULT_URL is not set'
    }

    It 'stops the publish naming the NuGet key before pushing anything' {
        $result = Invoke-Script $PublishPackage @('-RepoName', 'repo', '-OrgName', 'org')

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match "$PublishName is not set"
    }
}

# Only the job that builds the packages has the private key, so every other
# build has to work without it.
Describe 'the strong name key' {
    BeforeDiscovery {
        $root = Join-Path $PSScriptRoot '..'
        $script:PropsFiles = @(git -C $root ls-files '*Directory.Build.props' |
            ForEach-Object { Join-Path $root $_ } |
            Where-Object { Select-String -LiteralPath $_ -Pattern '51Degrees\.snk' -Quiet } |
            ForEach-Object { @{ Path = $_; Name = (Resolve-Path -Relative $_) } })
    }

    It 'is found in at least one project settings file' {
        $PropsFiles.Count | Should -BeGreaterThan 0
    }

    It 'signs with the private key in <Name> only when the key file is there' -ForEach $PropsFiles {
        [xml]$xml = Get-Content -Raw -LiteralPath $Path
        $settings = @($xml.SelectNodes('//AssemblyOriginatorKeyFile | //PublicSign'))

        $settings.Count | Should -BeGreaterThan 0
        foreach ($setting in $settings) {
            $setting.Condition | Should -Match "Exists\('\`$\(FiftyOneRoot\)51Degrees\.snk'\)"
        }
    }
}
