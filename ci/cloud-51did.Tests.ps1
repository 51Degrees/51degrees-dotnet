# Tests for the script of the step in .github/workflows/cloud-51did.yml that
# runs the live cloud 51Did tests, run with Pester 5 or later.
#
# The step's script is read from the workflow and run in a child PowerShell
# with dotnet replaced by a function that records each call, so no build or
# test runs and no key is needed. A child process is used because the script
# ends with exit, which would end the test run itself.

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
    Set-Content -LiteralPath $Step -Value (@(
        'function dotnet {'
        '  Add-Content -LiteralPath $env:CLOUD_51DID_CALLS -Value ($args -join " ")'
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
        $env:CLOUD_51DID_CALLS = $script:Calls
        $env:_51DEGREES_RESOURCE_KEY_BASIC = 'resource-basic'
        $env:_51DEGREES_RESOURCE_KEY_BESPOKE = 'resource-bespoke'
        $env:_51DEGREES_RESOURCE_KEY_51DID = 'resource-51did'
        $env:_51DEGREES_LICENSE_KEY_51DID = 'license-51did'
    }

    AfterEach {
        Get-ChildItem env: | Where-Object { $_.Name -like '_51DEGREES_*' } |
            ForEach-Object { Remove-Item "Env:$($_.Name)" }
        Remove-Item Env:CLOUD_51DID_CALLS -ErrorAction SilentlyContinue
        $script:Saved | ForEach-Object { Set-Item "Env:$($_.Name)" $_.Value }
    }

    It 'runs the tests when every license key has its resource key' {
        $result = Invoke-Step

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'License key for _51DEGREES_RESOURCE_KEY_51DID: _51DEGREES_LICENSE_KEY_51DID'
        $result.Calls | Where-Object { $_ -like 'test *' } | Should -Not -BeNullOrEmpty
    }

    # With the resource key missing, the pass that redeems with the license
    # key would be left out and the job would pass on the other keys alone.
    It 'fails naming _51DEGREES_RESOURCE_KEY_51DID when the license key has a value and it is empty' {
        $env:_51DEGREES_RESOURCE_KEY_51DID = ''

        $result = Invoke-Step

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match '_51DEGREES_RESOURCE_KEY_51DID'
        $result.Calls | Should -BeNullOrEmpty
    }
}
