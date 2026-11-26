# Tests for the script of the step in
# .github/workflows/cloud-human-confidence.yml that runs the Human
# Confidence tests against the staging cloud, run with Pester 5 or later.
#
# The step's script is read from the workflow and run in a child PowerShell
# with dotnet replaced by a function that records each call and the names it
# sees, and that copies the results files a test puts in a folder into the
# results folder the step names, so no build or test runs and no real key is
# needed. A child process is used because the script ends with exit, which
# would end the test run itself.

BeforeAll {
    $workflow = Join-Path $PSScriptRoot '..' '.github' 'workflows' 'cloud-human-confidence.yml'
    $lines = @(Get-Content -LiteralPath $workflow)
    $start = $lines | Select-String -Pattern '^(\s*)run: \|\s*$' |
        Select-Object -First 1
    $indent = $start.Matches[0].Groups[1].Value.Length
    $body = foreach ($line in $lines[$start.LineNumber..($lines.Count - 1)]) {
        if ($line.Trim() -and $line -notmatch "^\s{$($indent + 1),}") { break }
        if ($line.Length -gt $indent + 2) { $line.Substring($indent + 2) } else { '' }
    }

    $script:Root = Join-Path ([IO.Path]::GetTempPath()) "cloud-human-confidence-$([Guid]::NewGuid())"
    $script:Reports = Join-Path $Root 'reports'
    New-Item -ItemType Directory -Path $Root, $Reports | Out-Null
    $script:Calls = Join-Path $Root 'calls.txt'
    $script:Step = Join-Path $Root 'step.ps1'
    $callsLiteral = "'" + ($Calls -replace "'", "''") + "'"
    $reportsLiteral = "'" + ($Reports -replace "'", "''") + "'"
    Set-Content -LiteralPath $Step -Value (@(
        'function dotnet {'
        '  $names = @("CLOUD_STAGING_ENDPOINT", "RESOURCE_KEY_BESPOKE_STAGING",'
        '    "_51DEGREES_RESOURCE_KEY_BASIC", "_51DEGREES_CLOUD_ENDPOINT",'
        '    "_51DEGREES_RESOURCE_KEY_BESPOKE") | Where-Object {'
        '    [Environment]::GetEnvironmentVariable($_) } |'
        '    ForEach-Object { "$_=$([Environment]::GetEnvironmentVariable($_))" }'
        "  Add-Content -LiteralPath $callsLiteral -Value ((`$args -join ' ') + ' | ' + (`$names -join ' '))"
        '  if ($args[0] -eq "test") {'
        '    $results = $args[[Array]::IndexOf($args, "--results-directory") + 1]'
        '    New-Item -ItemType Directory -Force -Path $results | Out-Null'
        "    Copy-Item -Path (Join-Path $reportsLiteral '*') -Destination `$results"
        '  }'
        '  $global:LASTEXITCODE = 0'
        '}'
    ) + $body)

    # The test project the step reads its target frameworks from, at the
    # path the step names.
    $script:Project = Join-Path $Root 'pipeline' 'Tests' 'HumanConfidence.Examples.Tests' 'FiftyOne.HumanConfidence.Examples.Tests.csproj'
    New-Item -ItemType Directory -Path (Split-Path $Project) | Out-Null
    function Set-Project([string]$Frameworks) {
        Set-Content -LiteralPath $script:Project -Value (
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>' +
            "<TargetFrameworks>$Frameworks</TargetFrameworks>" +
            '</PropertyGroup><PropertyGroup><IsPackable>false</IsPackable>' +
            '</PropertyGroup></Project>')
    }

    # A results file for the framework given, named as dotnet test names it
    # with the step's LogFilePrefix, with a result for each outcome given.
    function New-Report([string]$Framework, [string[]]$Outcomes) {
        $name = "human-confidence_$($Framework)_20261123000000.trx"
        $passed = @($Outcomes | Where-Object { $_ -eq 'Passed' }).Count
        $results = for ($i = 0; $i -lt $Outcomes.Count; $i++) {
            "<UnitTestResult testName=`"Test$i`" outcome=`"$($Outcomes[$i])`" />"
        }
        Set-Content -LiteralPath (Join-Path $script:Reports $name) -Value (
            '<?xml version="1.0" encoding="utf-8"?>' +
            '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">' +
            "<Results>$($results -join '')</Results>" +
            '<ResultSummary outcome="Completed">' +
            "<Counters total=`"$($Outcomes.Count)`" passed=`"$passed`" />" +
            '</ResultSummary></TestRun>')
    }

    function Invoke-Step {
        Push-Location $script:Root
        try {
            $output = & pwsh -NoProfile -NonInteractive -File $script:Step 2>&1
        } finally {
            Pop-Location
        }
        [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Output   = ($output | Out-String)
            Calls    = @(if (Test-Path -LiteralPath $script:Calls) {
                Get-Content -LiteralPath $script:Calls })
        }
    }
}

AfterAll {
    Remove-Item -LiteralPath $script:Root -Recurse -Force -ErrorAction SilentlyContinue
}

Describe 'the Cloud Human Confidence step' {

    BeforeEach {
        $script:Saved = @(Get-ChildItem env: | Where-Object {
            $_.Name -like '_51DEGREES_*' -or
            $_.Name -in 'CLOUD_STAGING_ENDPOINT', 'RESOURCE_KEY_BESPOKE_STAGING' })
        $script:Saved | ForEach-Object { Remove-Item "Env:$($_.Name)" }
        Remove-Item -LiteralPath $script:Calls -ErrorAction SilentlyContinue
        Remove-Item -Path (Join-Path $script:Reports '*') -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath (Join-Path $script:Root 'test-results') -Recurse -Force -ErrorAction SilentlyContinue
        Set-Project 'net8.0;net10.0'
        $env:CLOUD_STAGING_ENDPOINT = 'https://staging.example.test/api/v4/'
        $env:RESOURCE_KEY_BESPOKE_STAGING = 'resource-bespoke-staging'
        $env:_51DEGREES_RESOURCE_KEY_BASIC = 'resource-basic'
    }

    AfterEach {
        Get-ChildItem env: | Where-Object {
            $_.Name -like '_51DEGREES_*' -or
            $_.Name -in 'CLOUD_STAGING_ENDPOINT', 'RESOURCE_KEY_BESPOKE_STAGING' } |
            ForEach-Object { Remove-Item "Env:$($_.Name)" }
        $script:Saved | ForEach-Object { Set-Item "Env:$($_.Name)" $_.Value }
    }

    It 'runs only the staging category, with the three names and no other' {
        New-Report 'net8.0' 'Passed', 'Passed'
        New-Report 'net10.0' 'Passed', 'Passed'

        $result = Invoke-Step

        $result.ExitCode | Should -Be 0
        $tests = @($result.Calls | Where-Object { $_ -like 'test *' })
        $tests.Count | Should -Be 1
        $tests[0] | Should -Match ([regex]::Escape(
            '--filter TestCategory=CloudStaging'))
        ($tests[0] -split ' \| ', 2)[1] | Should -Be (
            'CLOUD_STAGING_ENDPOINT=https://staging.example.test/api/v4/ ' +
            'RESOURCE_KEY_BESPOKE_STAGING=resource-bespoke-staging ' +
            '_51DEGREES_RESOURCE_KEY_BASIC=resource-basic')
    }

    It 'fails naming <Name> when it is empty, and runs nothing' -ForEach @(
        @{ Name = 'CLOUD_STAGING_ENDPOINT' }
        @{ Name = 'RESOURCE_KEY_BESPOKE_STAGING' }
        @{ Name = '_51DEGREES_RESOURCE_KEY_BASIC' }
    ) {
        Remove-Item "Env:$Name"

        $result = Invoke-Step

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match "::error::$Name is empty"
        $result.Calls | Should -BeNullOrEmpty
    }

    It 'passes when every test of each framework passed' {
        New-Report 'net8.0' 'Passed', 'Passed'
        New-Report 'net10.0' 'Passed', 'Passed'

        $result = Invoke-Step

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Not -Match '::error::'
    }

    It 'fails when a test was skipped' {
        New-Report 'net8.0' 'Passed', 'Passed'
        New-Report 'net10.0' 'Passed', 'NotExecuted'

        $result = Invoke-Step

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match '::error::1 of 2 tests in human-confidence_net10\.0_\d+\.trx did not pass'
        $result.Output | Should -Match 'Test1: NotExecuted'
    }

    It 'fails when no test ran' {
        New-Report 'net8.0' @()
        New-Report 'net10.0' 'Passed', 'Passed'

        $result = Invoke-Step

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match '::error::No test ran in human-confidence_net8\.0_\d+\.trx'
    }

    It 'fails when a framework ran fewer than the two staging tests' {
        New-Report 'net8.0' 'Passed', 'Passed'
        New-Report 'net10.0' 'Passed'

        $result = Invoke-Step

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match '::error::human-confidence_net10\.0_\d+\.trx holds 1 tests, where the CloudStaging category has 2'
    }

    It 'fails when only one framework wrote results' {
        New-Report 'net8.0' 'Passed', 'Passed'

        $result = Invoke-Step

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match '::error::The tests wrote 0 results files for net10\.0, where one is expected'
        $result.Output | Should -Not -Match 'for net8\.0'
    }

    It 'fails when the tests wrote no results' {
        $result = Invoke-Step

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match '::error::The tests wrote 0 results files for net8\.0'
        $result.Output | Should -Match '::error::The tests wrote 0 results files for net10\.0'
    }

    It 'fails before building when the project names no target frameworks' {
        Set-Project ''

        $result = Invoke-Step

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match '::error::pipeline/Tests/HumanConfidence\.Examples\.Tests/FiftyOne\.HumanConfidence\.Examples\.Tests\.csproj names no target frameworks'
        $result.Calls | Should -BeNullOrEmpty
    }
}
