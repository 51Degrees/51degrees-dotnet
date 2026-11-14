# Tests for ci/build-log-lines.ps1, run with Pester 5 or later.
#
# Each test writes test results files to a folder of its own and asks the
# script for the lines.

BeforeAll {
    $script:Script = Join-Path $PSScriptRoot 'build-log-lines.ps1'
    $script:Root = Join-Path ([IO.Path]::GetTempPath()) "build-log-lines-$([Guid]::NewGuid())"
    New-Item -ItemType Directory -Path $Root | Out-Null

    # Writes a results file holding one test with the output given, in the
    # shape the trx logger writes.
    function New-Results {
        param($Folder, $Name, $Outcome, $StdOut)
        New-Item -ItemType Directory -Force -Path $Folder | Out-Null
        $escaped = [Security.SecurityElement]::Escape($StdOut)
        @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testName="Measures" outcome="$Outcome">
      <Output>
        <StdOut>$escaped</StdOut>
      </Output>
    </UnitTestResult>
  </Results>
</TestRun>
"@ | Set-Content -LiteralPath (Join-Path $Folder $Name) -Encoding utf8
    }
}

AfterAll {
    Remove-Item -Recurse -Force -LiteralPath $Root
}

Describe 'build-log-lines' {
    It 'gives the line a test that passed wrote, with its results file' {
        $folder = Join-Path $Root 'passed'
        New-Results $folder 'run_net8.0.trx' 'Passed' "Build log: kept 3 MB of the 2174 MB the pipeline used."

        $lines = @(& $Script -ResultPath $folder)

        $lines.Count | Should -Be 2
        $lines[0] | Should -Be 'Lines the tests wrote for the build log:'
        $lines[1] | Should -Be '  Build log: kept 3 MB of the 2174 MB the pipeline used.  (run_net8.0.trx)'
    }

    It 'gives every such line of every results file, in the order of the file names' {
        $folder = Join-Path $Root 'several'
        New-Results $folder 'b.trx' 'Passed' "Build log: second`nnot for the log`nBuild log: third"
        New-Results $folder 'a.trx' 'Failed' "Build log: first"

        $lines = @(& $Script -ResultPath $folder)

        $lines | Should -Be @(
            'Lines the tests wrote for the build log:',
            '  Build log: first  (a.trx)',
            '  Build log: second  (b.trx)',
            '  Build log: third  (b.trx)')
    }

    It 'gives the text as the test wrote it' {
        $folder = Join-Path $Root 'escaped'
        New-Results $folder 'run.trx' 'Passed' "Build log: 3 < 10 & 'kept'"

        $lines = @(& $Script -ResultPath $folder)

        $lines[1] | Should -Be "  Build log: 3 < 10 & 'kept'  (run.trx)"
    }

    It 'gives nothing when no test wrote a line' {
        $folder = Join-Path $Root 'none'
        New-Results $folder 'run.trx' 'Passed' "Nothing for the log."

        @(& $Script -ResultPath $folder).Count | Should -Be 0
    }

    It 'gives nothing for a folder that is not there' {
        @(& $Script -ResultPath (Join-Path $Root 'missing')).Count | Should -Be 0
    }
}
