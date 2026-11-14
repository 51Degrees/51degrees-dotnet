# Tests for ci/start-chrome-once.ps1, run with Pester 5 or later.
#
# Each test names a stand-in for Chrome in CHROME_BIN, which the script reads
# before it looks anywhere else, and reads what the script prints.

BeforeAll {
    $script:Script = Join-Path $PSScriptRoot 'start-chrome-once.ps1'
    $script:Root = Join-Path ([IO.Path]::GetTempPath()) "start-chrome-once-$([Guid]::NewGuid())"
    New-Item -ItemType Directory -Path $Root | Out-Null
    $script:SavedChrome = $env:CHROME_BIN

    # A program that does what its body says, whatever arguments it is given.
    function New-Program {
        param($Name, $WindowsBody, $ShellBody)
        if ($IsWindows) {
            $path = Join-Path $Root "$Name.cmd"
            Set-Content -LiteralPath $path -Value "@echo off`r`n$WindowsBody"
        } else {
            $path = Join-Path $Root $Name
            Set-Content -LiteralPath $path -Value "#!/bin/sh`n$ShellBody"
            chmod +x $path
        }
        return $path
    }

    # Everything the script prints, as one string.
    function Get-Printed {
        param($Chrome, $LimitSeconds = 30)
        $env:CHROME_BIN = $Chrome
        return (& $Script -LimitSeconds $LimitSeconds *>&1 | Out-String)
    }
}

AfterAll {
    $env:CHROME_BIN = $SavedChrome
    Remove-Item -Recurse -Force -LiteralPath $Root -ErrorAction SilentlyContinue
}

Describe 'start-chrome-once' {
    It 'says how long a Chrome that starts took' {
        $chrome = New-Program 'starts' 'exit /b 0' 'exit 0'

        $printed = Get-Printed $chrome

        $printed | Should -Match ([regex]::Escape("Started Chrome $chrome once before the browser tests in "))
        $printed | Should -Not -Match ([regex]::Escape("Chrome $chrome exited"))
    }

    It 'warns of a Chrome that exits with an error, with its code' {
        $chrome = New-Program 'fails' 'exit /b 3' 'exit 3'

        $printed = Get-Printed $chrome

        $printed | Should -Match ([regex]::Escape("::warning title=Chrome did not start::Chrome $chrome exited with code 3 after "))
        $printed | Should -Not -Match ([regex]::Escape("Started Chrome $chrome"))
    }

    It 'stops a Chrome that does not finish within the limit and warns' {
        $chrome = New-Program 'stalls' 'ping -n 60 127.0.0.1 >nul' 'sleep 60'

        $watch = [Diagnostics.Stopwatch]::StartNew()
        $printed = Get-Printed $chrome -LimitSeconds 2

        $printed | Should -Match ([regex]::Escape("::warning title=Chrome slow to start::Chrome $chrome did not finish its first start within 2 s, so it was stopped."))
        $watch.Elapsed.TotalSeconds | Should -BeLessThan 30
    }
}
