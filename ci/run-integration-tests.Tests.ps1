# Tests for which example tests ci/run-integration-tests.ps1 runs at each
# depth, run with Pester 5 or later.
#
# Each test makes a folder holding empty files named as two example test
# assemblies, puts a stand-in for dotnet first on the path that writes each
# command line it is given to a file, runs the script and reads that file.

BeforeAll {
    $script:Script = Join-Path $PSScriptRoot 'run-integration-tests.ps1'
    $script:Root = Join-Path ([IO.Path]::GetTempPath()) "run-integration-tests-$([Guid]::NewGuid())"
    $script:Repo = 'repo'
    $script:Bin = Join-Path $Root $Repo 'examples' 'bin' 'Release' 'net8.0'
    $script:Tools = Join-Path $Root 'tools'
    $script:Calls = Join-Path $Root 'calls.txt'
    New-Item -ItemType Directory -Path $Bin, $Tools | Out-Null

    $script:IpExamples = 'FiftyOne.IpIntelligence.Example.Tests.OnPremise.dll'
    $script:DdExamples = 'FiftyOne.DeviceDetection.Example.Tests.OnPremise.dll'
    foreach ($name in $IpExamples, $DdExamples) {
        New-Item -ItemType File -Path (Join-Path $Bin $name) | Out-Null
    }

    if ($IsWindows) {
        # A PowerShell script rather than a batch file, because cmd would read
        # the '|' in a test filter as a pipe.
        Set-Content -LiteralPath (Join-Path $Tools 'dotnet.ps1') -Value (@(
            'Add-Content -LiteralPath $env:DOTNET_CALLS -Value ($args -join " ")',
            'exit 0') -join "`r`n")
    } else {
        $dotnet = Join-Path $Tools 'dotnet'
        Set-Content -LiteralPath $dotnet -Value (@(
            '#!/bin/sh',
            'echo "$*" >> "$DOTNET_CALLS"',
            'exit 0') -join "`n")
        chmod +x $dotnet
    }

    $script:SavedPath = $env:PATH
    $script:SavedTestFilter = $env:_51DEGREES_TEST_FILTER
    $env:PATH = $Tools + [IO.Path]::PathSeparator + $env:PATH
    $env:DOTNET_CALLS = $Calls
    Remove-Item Env:_51DEGREES_TEST_FILTER -ErrorAction SilentlyContinue

    # The command lines dotnet was given for the assembly named, none when
    # the script did not run it.
    function Get-Calls([string]$Depth, [string]$Assembly) {
        Remove-Item -LiteralPath $Calls -ErrorAction SilentlyContinue
        Push-Location $Root
        try {
            & $Script -RepoName $Repo -Depth $Depth -SeleniumContract $false *> $null
        } finally {
            Pop-Location
        }
        if (Test-Path $Calls) {
            @(Get-Content $Calls | Where-Object { $_ -like "*$Assembly*" })
        }
    }
}

AfterAll {
    $env:PATH = $SavedPath
    $env:_51DEGREES_TEST_FILTER = $SavedTestFilter
    Remove-Item Env:DOTNET_CALLS -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force -LiteralPath $Root -ErrorAction SilentlyContinue
}

Describe 'run-integration-tests.ps1' {
    It 'runs only the IP Intelligence examples with no settings and the unit tests at standard depth' {
        $calls = @(Get-Calls 'standard' $IpExamples)

        $calls.Count | Should -Be 1
        $calls[0] | Should -Match ([regex]::Escape(
            '--filter TestCategory=WithoutSettings|TestCategory=Unit'))
    }

    It 'runs every IP Intelligence example test at deep depth' {
        $calls = @(Get-Calls 'deep' $IpExamples)

        $calls.Count | Should -Be 1
        $calls[0] | Should -Not -Match '--filter'
    }

    It 'runs every IP Intelligence example test on the branch and in a release' {
        $calls = @(Get-Calls '' $IpExamples)

        $calls.Count | Should -Be 1
        $calls[0] | Should -Not -Match '--filter'
    }

    It 'runs every other example test at standard depth' {
        $calls = @(Get-Calls 'standard' $DdExamples)

        $calls.Count | Should -Be 1
        $calls[0] | Should -Not -Match '--filter'
    }
}
