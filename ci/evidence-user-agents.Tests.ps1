# Tests for ci/evidence-user-agents.ps1, run with Pester 5 or later.

BeforeAll {
    $script:Script = Join-Path $PSScriptRoot 'evidence-user-agents.ps1'
    $script:Root = Join-Path ([IO.Path]::GetTempPath()) "evidence-user-agents-$([Guid]::NewGuid())"
    New-Item -ItemType Directory -Path $Root | Out-Null

    function Get-UserAgents([string[]]$Evidence) {
        $evidencePath = Join-Path $Root 'evidence.yml'
        $outputPath = Join-Path $Root 'user-agents.txt'
        Set-Content -LiteralPath $evidencePath -Value $Evidence
        & $Script -EvidencePath $evidencePath -OutputPath $outputPath
        @(Get-Content -LiteralPath $outputPath)
    }
}

AfterAll {
    Remove-Item -Recurse -Force -LiteralPath $Root -ErrorAction SilentlyContinue
}

Describe 'evidence-user-agents.ps1' {
    It 'writes the User-Agent of each record on a line of its own, in order' {
        $agents = Get-UserAgents @(
            '---'
            'header.user-agent: First (Windows NT 10.0; Win64; x64)'
            '---'
            'header.sec-ch-ua-mobile: ?0'
            'header.user-agent: Second [FBAN/FBIOS;FBAV/1]'
            "header.sec-ch-ua: '`"Chromium`";v=`"142`"'")

        $agents | Should -Be @(
            'First (Windows NT 10.0; Win64; x64)'
            'Second [FBAN/FBIOS;FBAV/1]')
    }

    It 'leaves out a record with no User-Agent' {
        $agents = Get-UserAgents @(
            '---'
            'header.accept-language: en-GB'
            '---'
            'header.user-agent: Only')

        $agents | Should -Be @('Only')
    }

    It 'takes the quotes off a quoted value and keeps each quote inside it' {
        $agents = Get-UserAgents @(
            '---'
            "header.user-agent: 'An ''app'' {ua}'")

        $agents | Should -Be @("An 'app' {ua}")
    }
}
