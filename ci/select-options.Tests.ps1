# Tests for ci/select-options.ps1, run with Pester 5 or later.
#
# Each test gives the script every entry of ci/options.json and the files a
# pull request might change, and checks the entries it chooses and the
# options it sets on them.

BeforeAll {
    $script:Select = Join-Path $PSScriptRoot 'select-options.ps1'
    $script:OptionsJson = Get-Content -Raw (Join-Path $PSScriptRoot 'options.json')

    # The names of the entries chosen for the files given, in the order of
    # options.json.
    function Get-Chosen([string[]]$Changed, [string[]]$CommentOnly = @()) {
        $options = @(ConvertFrom-Json $OptionsJson -AsHashtable)
        @(& $Select -Options $options -ChangedFiles $Changed `
            -CommentOnlyFiles $CommentOnly 6>$null)
    }
    function Get-Names([object[]]$Entries) {
        @($Entries | ForEach-Object { $_.Name })
    }

    $script:Standard = @('Windows_x64_Release', 'Ubuntu_x64_Release')
    $script:WithNative = @('Windows_x64_Release', 'Windows_x86_Release',
        'Ubuntu_x64_Release', 'Ubuntu_ARM_Release')
    $script:AllButMac = @('Windows_x64_Release', 'Windows_x86_Release',
        'Windows_x64_Debug', 'Ubuntu_x64_Release', 'Ubuntu_x64_Debug',
        'Ubuntu_ARM_Release')
}

Describe 'select-options.ps1' {
    It 'builds the standard set for a change to managed package code' {
        $chosen = Get-Chosen @('pipeline/Core/FlowElements/Pipeline.cs')
        Get-Names $chosen | Should -Be $Standard
        $chosen | ForEach-Object { $_.Depth | Should -Be 'standard' }
    }

    It 'adds Linux ARM and Windows x86 for a native engine' {
        $chosen = Get-Chosen @('device-detection/Hash.Engine.OnPremise/FlowElements/DeviceDetectionHashEngine.cs')
        Get-Names $chosen | Should -Be $WithNative
        $chosen | ForEach-Object { $_.Depth | Should -Be 'standard' }
    }

    It 'adds Linux ARM and Windows x86 for the cxx submodule, at depth deep' {
        $chosen = Get-Chosen @('cxx')
        Get-Names $chosen | Should -Be $WithNative
        $chosen | ForEach-Object { $_.Depth | Should -Be 'deep' }
    }

    It 'runs the Enterprise tests for an IP Intelligence change' {
        $chosen = Get-Chosen @('ip-intelligence/IpIntelligence/IpiPipelineBuilder.cs')
        Get-Names $chosen | Should -Be $Standard
        $chosen | ForEach-Object { $_.Depth | Should -Be 'deep' }
    }

    It 'treats a native change in comments only as a managed one' {
        $file = 'device-detection/Hash.Engine.OnPremise/FlowElements/DeviceDetectionHashEngine.cs'
        Get-Names (Get-Chosen @($file) @($file)) | Should -Be $Standard
    }

    It 'runs the web example tests on Linux only for a web change' {
        $chosen = Get-Chosen @('pipeline/Web/Services/WebRequestEvidenceService.cs')
        Get-Names $chosen | Should -Be $Standard
        ($chosen | Where-Object { $_.Name -eq 'Ubuntu_x64_Release' }).WebExamples |
            Should -BeTrue
        ($chosen | Where-Object { $_.Name -eq 'Windows_x64_Release' }).WebExamples |
            Should -BeFalse
    }

    It 'builds every entry but macOS for a change outside the package folders' {
        $chosen = Get-Chosen @('ci/run-unit-tests.ps1')
        Get-Names $chosen | Should -Be $AllButMac
        $chosen | ForEach-Object { $_.WebExamples | Should -BeTrue }
    }

    It 'runs the Selenium contract on one entry and performance on none' {
        $chosen = Get-Chosen @('ci/run-unit-tests.ps1')
        @($chosen | Where-Object { $_.SeleniumContract }).Name |
            Should -Be 'Ubuntu_x64_Release'
        $chosen | ForEach-Object { $_.RunPerformance | Should -BeFalse }
    }
}
