# Tests for the lines ci/run-test-lanes.ps1 prints before it runs anything,
# run with Pester 5 or later.
#
# An assembly's output is printed when it finishes, so a job killed at its
# time limit says nothing about the assembly it was running. These tests hold
# the script to naming every assembly it is about to run, and each exclusive
# one again as it starts.
#
# The test makes a folder holding empty files named as two test assemblies,
# one that runs on its own and one that runs in the pool, puts a stand-in for
# dotnet first on the path that passes and writes nothing, and reads what the
# script printed.

BeforeAll {
    $script:Script = Join-Path $PSScriptRoot 'run-test-lanes.ps1'
    $script:Root = Join-Path ([IO.Path]::GetTempPath()) "run-test-lanes-$([Guid]::NewGuid())"
    $script:Repo = 'repo'
    $script:Bin = Join-Path $Root $Repo 'bin' 'Release' 'net8.0'
    $script:Tools = Join-Path $Root 'tools'
    New-Item -ItemType Directory -Path $Bin, $Tools | Out-Null

    $script:Exclusive = 'FiftyOne.Caching.Tests.dll'
    $script:Pooled = 'FiftyOne.Pipeline.Core.Tests.dll'
    foreach ($name in $Exclusive, $Pooled) {
        New-Item -ItemType File -Path (Join-Path $Bin $name) | Out-Null
    }

    if ($IsWindows) {
        Set-Content -LiteralPath (Join-Path $Tools 'dotnet.ps1') -Value 'exit 0'
    } else {
        $dotnet = Join-Path $Tools 'dotnet'
        Set-Content -LiteralPath $dotnet -Value (@('#!/bin/sh', 'exit 0') -join "`n")
        chmod +x $dotnet
    }

    $script:SavedPath = $env:PATH
    $script:SavedTestFilter = $env:_51DEGREES_TEST_FILTER
    $env:PATH = $Tools + [IO.Path]::PathSeparator + $env:PATH
    Remove-Item Env:_51DEGREES_TEST_FILTER -ErrorAction SilentlyContinue

    # Everything the script printed, as one string.
    function Get-Output {
        Push-Location $Root
        try {
            (& $Script -RepoName $Repo -Name 'Release_x64' -Configuration 'Release' `
                -Filter '^FiftyOne\..*Tests\.dll$' -OutputFolder 'unit' *>&1 |
                Out-String)
        } finally {
            Pop-Location
        }
    }
}

AfterAll {
    $env:PATH = $SavedPath
    $env:_51DEGREES_TEST_FILTER = $SavedTestFilter
    Remove-Item -Recurse -Force -LiteralPath $Root -ErrorAction SilentlyContinue
}

Describe 'run-test-lanes.ps1' {
    BeforeAll {
        $script:Output = Get-Output
    }

    It 'names every assembly it is about to run, with its framework' {
        $Output | Should -Match ([regex]::Escape('exclusive: FiftyOne.Caching.Tests net8.0'))
        $Output | Should -Match ([regex]::Escape('pool: FiftyOne.Pipeline.Core.Tests net8.0'))
    }

    It 'names each exclusive assembly as it starts it, with the time' {
        $Output | Should -Match 'Starting FiftyOne\.Caching\.Tests net8\.0 at \d\d:\d\d:\d\d UTC'
    }
}
