# Tests that warnings are errors in every build 51Degrees runs and in no
# other, run with Pester 5 or later. The one switch is the
# _51DEGREES_WARNINGS_AS_ERRORS environment variable: "true", in any case,
# makes warnings errors, and anything else leaves them warnings. Each CI
# script and workflow that builds sets it, so these tests fail when CI could
# build without it.

BeforeDiscovery {
    $Top = Join-Path $PSScriptRoot '..'

    # The commands that compile code. 'dotnet test' and 'dotnet run' compile
    # unless given --no-build.
    $BuildPattern = '(?<![\w-])(dotnet\s+(build|pack|publish)\b|' +
        'dotnet\s+(test|run)\b(?!.*--no-build)|msbuild\s|cmake\s(?!--version)|' +
        'cmake-warning-args\.ps1|cxx/build-project\.ps1)'

    # The CI scripts that build, each with the line of its first build
    # command. cmake-warning-args.ps1 only writes arguments for the scripts
    # that build, and reads the variable itself.
    $BuildingScripts = @(
        Get-ChildItem (Join-Path $Top 'ci') -Filter '*.ps1' |
            Where-Object { $_.Name -notlike '*.Tests.ps1' -and $_.Name -ne 'cmake-warning-args.ps1' } |
            ForEach-Object {
                $lines = @(Get-Content $_.FullName | ForEach-Object { $_ -replace '#.*$', '' })
                for ($i = 0; $i -lt $lines.Count; $i++) {
                    if ($lines[$i] -cmatch $BuildPattern) {
                        @{ Script = $_.Name; FirstBuild = $i + 1 }
                        break
                    }
                }
            }
    )

    # The workflows that build in a step of their own, rather than through
    # a shared workflow that runs the CI scripts.
    $BuildingWorkflows = @(
        Get-ChildItem (Join-Path $Top '.github/workflows') -Filter '*.yml' |
            Where-Object {
                $code = @(Get-Content $_.FullName | ForEach-Object { $_ -replace '#.*$', '' })
                @($code -cmatch $BuildPattern).Count -gt 0
            } |
            ForEach-Object { @{ Workflow = $_.Name } }
    )
}

BeforeAll {
    $Root = Join-Path $PSScriptRoot '..'
    $Set = '^\s*\$env:_51DEGREES_WARNINGS_AS_ERRORS\s*=\s*"true"\s*$'

    # The lines of a file without their comments.
    function Get-Code([string]$Path) {
        @(Get-Content (Join-Path $Root $Path) | ForEach-Object { $_ -replace '#.*$', '' })
    }
}

Describe 'the CI scripts that build' {
    It 'include the scripts that build the projects and the native libraries' -ForEach @(
        @{ Names = @($BuildingScripts | ForEach-Object { $_.Script }) }) {
        $names = $Names
        foreach ($name in 'build-project.ps1', 'build-package.ps1',
            'build-package-requirements.ps1', 'install-package.ps1') {
            $names | Should -Contain $name
        }
    }

    It 'sets _51DEGREES_WARNINGS_AS_ERRORS to "true" in <Script> before line <FirstBuild>' -ForEach $BuildingScripts {
        $code = Get-Code "ci/$Script"
        $at = @(for ($i = 0; $i -lt $code.Count; $i++) {
            if ($code[$i] -match $Set) { $i + 1 }
        })
        $at.Count | Should -BeGreaterThan 0 -Because "$Script builds, so warnings must be errors in it"
        $at[0] | Should -BeLessThan $FirstBuild
    }
}

Describe 'the workflows that build in their own steps' {
    It 'sets _51DEGREES_WARNINGS_AS_ERRORS to "true" in <Workflow>' -ForEach $BuildingWorkflows {
        $code = (Get-Code ".github/workflows/$Workflow") -join "`n"
        $code | Should -Match '(?m)^\s+_51DEGREES_WARNINGS_AS_ERRORS:\s*["'']?true["'']?\s*$'
    }
}

Describe 'the switch' {
    # A global property, given as -p:, overrides Directory.Build.targets, so
    # it would make C# warnings errors without the variable, or hide them
    # with it. The native switches are tested in native-warnings.Tests.ps1.
    It 'is not passed round in <_>' -ForEach @(
        Get-ChildItem (Join-Path $PSScriptRoot '..' 'ci') -Filter '*.ps1' |
            Where-Object { $_.Name -notlike '*.Tests.ps1' } |
            ForEach-Object { "ci/$($_.Name)" }
        Get-ChildItem (Join-Path $PSScriptRoot '..' '.github/workflows') -Filter '*.yml' |
            ForEach-Object { ".github/workflows/$($_.Name)" }
    ) {
        $code = (Get-Code $_) -join "`n"
        $code | Should -Not -Match '(?i)TreatWarningsAsErrors|[-/]warnaserror'
    }

    It 'decides TreatWarningsAsErrors in the root Directory.Build.targets' {
        $text = Get-Content (Join-Path $Root 'Directory.Build.targets') -Raw
        $text | Should -Match ([regex]::Escape(
            "`$(_51DEGREES_WARNINGS_AS_ERRORS.ToLowerInvariant().Equals('true'))"))
        $text | Should -Match '(?s)<PropertyGroup Condition="''\$\(_FiftyOneWarningsAsErrors\)'' != ''True''">\s*<TreatWarningsAsErrors>false</TreatWarningsAsErrors>'
    }

    It 'is set nowhere else in MSBuild' {
        $others = @(Get-ChildItem $Root -Recurse -File -Include 'Directory.Build.targets', '*.targets', '*.props' |
            Where-Object { $_.FullName -notmatch '[\\/](cxx|bin|obj|node_modules)[\\/]' -and
                $_.FullName -ne (Resolve-Path (Join-Path $Root 'Directory.Build.targets')).Path } |
            Where-Object { (Get-Content $_.FullName -Raw) -match 'TreatWarningsAsErrors' })
        $others | Should -BeNullOrEmpty
    }
}

Describe 'TreatWarningsAsErrors as MSBuild evaluates it' -Skip:(
    -not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    BeforeAll {
        # A project that sets TreatWarningsAsErrors to true itself.
        $Project = Join-Path $Root 'pipeline/Core/FiftyOne.Pipeline.Core.csproj'
        $Saved = $env:_51DEGREES_WARNINGS_AS_ERRORS

        function Get-TreatWarningsAsErrors {
            $json = dotnet msbuild $Project -nologo -getProperty:TreatWarningsAsErrors -p:TargetFramework=netstandard2.0
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet msbuild failed: $json"
            }
            # One property is printed as its value alone.
            "$json".Trim()
        }
    }

    AfterAll {
        $env:_51DEGREES_WARNINGS_AS_ERRORS = $Saved
    }

    It 'is <Expected> when the variable is <Value>' -ForEach @(
        @{ Value = $null; Expected = 'false' }
        @{ Value = 'true'; Expected = 'true' }
        @{ Value = 'TRUE'; Expected = 'true' }
        @{ Value = 'True'; Expected = 'true' }
        @{ Value = 'false'; Expected = 'false' }
        @{ Value = 'yes'; Expected = 'false' }
        @{ Value = 'on'; Expected = 'false' }
        @{ Value = '1'; Expected = 'false' }
    ) {
        $env:_51DEGREES_WARNINGS_AS_ERRORS = $Value
        Get-TreatWarningsAsErrors | Should -Be $Expected
    }
}
