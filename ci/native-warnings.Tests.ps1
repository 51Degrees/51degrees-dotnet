# Tests of the warnings the two on-premise engines' native libraries build
# with, run with Pester 5 or later. Every C and C++ warning is an error on
# every compiler, at the level the cxx submodule builds its own tests with,
# and nothing in these files turns a warning off.

BeforeDiscovery {
    $Engines = @(
        @{
            Engine = 'Device Detection'
            Folder = 'device-detection/Hash.Engine.OnPremise'
            Interface = 'hash_csharp.i'
        }
        @{
            Engine = 'IP Intelligence'
            Folder = 'ip-intelligence/Engine.OnPremise'
            Interface = 'ipi_csharp.i'
        }
    )
}

BeforeAll {
    $Root = Join-Path $PSScriptRoot '..'

    # The lines of a file without its comments, which start with # in CMake
    # and PowerShell and with // in a SWIG interface.
    function Get-Code([string]$Path) {
        @(Get-Content (Join-Path $Root $Path) | ForEach-Object {
            if ($Path -like '*.i') {
                $_ -replace '//.*$', ''
            } else {
                $_ -replace '#.*$', ''
            }
        })
    }

    # The CMake lines before the file includes cxx. They apply to every
    # target cxx adds as well as to the wrapper.
    function Get-LinesBeforeCxx([string]$Path) {
        foreach ($line in Get-Code $Path) {
            if ($line -match '^\s*include\(.*cxx/CMakeLists\.txt') {
                break
            }
            $line
        }
    }

    # Switches that hide warnings from MSVC, GCC or Clang, or that turn off
    # the CRT and C++ library deprecation warnings.
    $Hiding = @(
        '(?<![\w-])[/-]wd\d+'
        '(?<![\w-])[/-]w(?![\w-])'
        '(?<![\w-])-Wno-'
        '_CRT_SECURE_NO_WARNINGS'
        '(?<!SWIG_NO)_CRT_SECURE_NO_DEPRECATE'
        '_SCL_SECURE_NO_WARNINGS'
        '(?<!SWIG_NO)_SCL_SECURE_NO_DEPRECATE'
        'pragma\s+warning\s*\(\s*disable'
        'diagnostic\s+ignored'
        'compile-no-warning-as-error'
        'CMAKE_COMPILE_WARNING_AS_ERROR\s*=\s*(OFF|FALSE|0)'
    )
}

Describe 'the <Engine> engine native build' -ForEach $Engines {
    BeforeAll {
        $CMake = "$Folder/CMakeLists.txt"
    }

    It 'turns no warning off in <_>' -ForEach @(
        'CMakeLists.txt', 'PreBuild.ps1', $Interface) {
        $code = (Get-Code "$Folder/$_") -join "`n"
        foreach ($pattern in $Hiding) {
            $code | Should -Not -MatchExactly $pattern
        }
    }

    It 'needs a CMake that leaves the warning level out of the default flags' {
        $code = (Get-Code $CMake) -join "`n"
        $code -match 'cmake_minimum_required\(VERSION\s+(?<v>\d+\.\d+)[\s.)]' |
            Should -BeTrue
        [version]$Matches['v'] | Should -BeGreaterOrEqual ([version]'3.24')
    }

    It 'warns at /W4 and -Wall on every target' {
        $before = (Get-LinesBeforeCxx $CMake) -join "`n"
        $before | Should -MatchExactly 'add_compile_options\([^)]*/W4'
        $before | Should -MatchExactly 'add_compile_options\([^)]*-Wall'
    }

    It 'makes every warning an error' {
        $before = (Get-LinesBeforeCxx $CMake) -join "`n"
        $before | Should -Match 'set\(\s*CMAKE_COMPILE_WARNING_AS_ERROR\s+ON\s*\)'
        $code = (Get-Code $CMake) -join "`n"
        $code | Should -MatchExactly 'target_compile_options\([^)]*"/WX"'
        $code | Should -MatchExactly 'target_compile_options\([^)]*"-Werror"'
        $code | Should -MatchExactly 'target_link_options\([^)]*/WX'
    }

    It 'keeps the warnings the SWIG wrapper would turn off' {
        $code = (Get-Code $CMake) -join "`n"
        foreach ($name in 'SWIG_MSC_UNSUPPRESS_4505',
            'SWIG_NO_CRT_SECURE_NO_DEPRECATE', 'SWIG_NO_SCL_SECURE_NO_DEPRECATE') {
            $code | Should -Match "target_compile_definitions\([^)]*\b$name\b"
        }
    }

    It 'stops PreBuild.ps1 at the first CMake command that fails' {
        $code = (Get-Code "$Folder/PreBuild.ps1") -join "`n"
        $code | Should -Match '(?m)^\s*\$ErrorActionPreference\s*=\s*"Stop"'
        $code | Should -Match '(?m)^\s*\$PSNativeCommandUseErrorActionPreference\s*=\s*\$true'
    }
}
