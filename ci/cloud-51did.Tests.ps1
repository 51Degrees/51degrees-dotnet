# Tests for the script of the step in .github/workflows/cloud-51did.yml that
# runs the live cloud 51Did tests, run with Pester 5 or later.
#
# The step's script is read from the workflow and run in a child PowerShell
# with dotnet replaced by a function that records each call and the keys it
# sees, so no build or test runs and no real key is needed. A child process is
# used because the script ends with exit, which would end the test run itself.

BeforeAll {
    $workflow = Join-Path $PSScriptRoot '..' '.github' 'workflows' 'cloud-51did.yml'
    $lines = @(Get-Content -LiteralPath $workflow)
    $start = $lines | Select-String -Pattern '^(\s*)run: \|\s*$' |
        Select-Object -First 1
    $indent = $start.Matches[0].Groups[1].Value.Length
    $body = foreach ($line in $lines[$start.LineNumber..($lines.Count - 1)]) {
        if ($line.Trim() -and $line -notmatch "^\s{$($indent + 1),}") { break }
        if ($line.Length -gt $indent + 2) { $line.Substring($indent + 2) } else { '' }
    }

    $script:Root = Join-Path ([IO.Path]::GetTempPath()) "cloud-51did-$([Guid]::NewGuid())"
    New-Item -ItemType Directory -Path $Root | Out-Null
    $script:Calls = Join-Path $Root 'calls.txt'
    $script:Step = Join-Path $Root 'step.ps1'
    # Each call is recorded with the value of every _51DEGREES_ name the
    # tests would see, so a test can check that no key was copied.
    $callsLiteral = "'" + ($Calls -replace "'", "''") + "'"
    Set-Content -LiteralPath $Step -Value (@(
        'function dotnet {'
        '  $keys = @(Get-ChildItem env: | Where-Object Name -like "_51DEGREES_*" |'
        '    Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" })'
        "  Add-Content -LiteralPath $callsLiteral -Value ((`$args -join ' ') + ' | ' + (`$keys -join ' '))"
        '  $global:LASTEXITCODE = 0'
        '}'
    ) + $body)

    function Invoke-Step {
        $output = & pwsh -NoProfile -NonInteractive -File $script:Step 2>&1
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

Describe 'the Cloud 51Did step' {

    BeforeEach {
        $script:Saved = @(Get-ChildItem env: |
            Where-Object { $_.Name -like '_51DEGREES_*' })
        $script:Saved | ForEach-Object { Remove-Item "Env:$($_.Name)" }
        Remove-Item -LiteralPath $script:Calls -ErrorAction SilentlyContinue
        $env:_51DEGREES_RESOURCE_KEY_BASIC = 'resource-basic'
        $env:_51DEGREES_RESOURCE_KEY_BESPOKE = 'resource-bespoke'
        $env:_51DEGREES_RESOURCE_KEY_51DID = 'resource-51did'
        $env:_51DEGREES_LICENSE_KEY_51DID = 'license-51did'
    }

    AfterEach {
        Get-ChildItem env: | Where-Object { $_.Name -like '_51DEGREES_*' } |
            ForEach-Object { Remove-Item "Env:$($_.Name)" }
        $script:Saved | ForEach-Object { Set-Item "Env:$($_.Name)" $_.Value }
    }

    It 'runs each test project once' {
        $result = Invoke-Step

        $result.ExitCode | Should -Be 0
        $tests = @($result.Calls | Where-Object { $_ -like 'test *' })
        $tests.Count | Should -Be 2
        $tests | Where-Object { $_ -like '*FiftyOne.Did.Tests.csproj*' } |
            Should -HaveCount 1
        $tests | Where-Object { $_ -like '*FiftyOne.Did.Cloud.Tests.csproj*' } |
            Should -HaveCount 1
    }

    It 'gives the tests each key under its own name only' {
        $result = Invoke-Step

        $expected = '_51DEGREES_LICENSE_KEY_51DID=license-51did ' +
            '_51DEGREES_RESOURCE_KEY_51DID=resource-51did ' +
            '_51DEGREES_RESOURCE_KEY_BASIC=resource-basic ' +
            '_51DEGREES_RESOURCE_KEY_BESPOKE=resource-bespoke'
        $tests = @($result.Calls | Where-Object { $_ -like 'test *' })
        foreach ($call in $tests) {
            ($call -split ' \| ', 2)[1] | Should -Be $expected
        }
    }

    It 'fails naming the 51Did resource key when its license key has a value and it is empty' {
        $env:_51DEGREES_RESOURCE_KEY_51DID = ''

        $result = Invoke-Step

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match '_51DEGREES_RESOURCE_KEY_51DID'
        $result.Calls | Should -BeNullOrEmpty
    }

    It 'fails when no resource key reached the job' {
        $env:_51DEGREES_RESOURCE_KEY_BASIC = ''
        $env:_51DEGREES_RESOURCE_KEY_BESPOKE = ''
        $env:_51DEGREES_RESOURCE_KEY_51DID = ''
        $env:_51DEGREES_LICENSE_KEY_51DID = ''

        $result = Invoke-Step

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match '_51DEGREES_RESOURCE_KEY_BESPOKE'
        $result.Calls | Should -BeNullOrEmpty
    }

    # Without 51Did keys the rows for them skip, and the other keys still run.
    It 'runs the tests when both 51Did keys are empty' {
        $env:_51DEGREES_RESOURCE_KEY_51DID = ''
        $env:_51DEGREES_LICENSE_KEY_51DID = ''

        $result = Invoke-Step

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Not -Match '::error::'
        @($result.Calls | Where-Object { $_ -like 'test *' }).Count | Should -Be 2
    }
}
