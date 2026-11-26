# Tests of the warnings the two on-premise engines' native libraries build
# with, run with Pester 5 or later. Every C and C++ warning is an error on
# every compiler, at the level the cxx submodule builds its own tests with,
# linker and CMake warnings are errors too, and nothing in these files or in
# the cxx code the engines' builds compile turns a warning off.

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

    # The cxx sources and CMake files the engines include. The release build
    # of the native libraries also compiles the cxx tests, examples and
    # tools. An empty list means the cxx submodule is not checked out, which
    # fails below.
    $Top = Join-Path $PSScriptRoot '..'
    $CxxFolders = @('src', 'test', 'examples', 'tools') |
        ForEach-Object { Join-Path $Top "cxx/$_" } |
        Where-Object { Test-Path $_ }
    $CxxFiles = @()
    if (Test-Path (Join-Path $Top 'cxx/src')) {
        $CxxFiles = @(
            Get-ChildItem -File -Recurse $CxxFolders |
                Where-Object { $_.Extension -in '.c', '.h', '.cpp', '.hpp', '.i' -or
                    $_.Name -eq 'CMakeLists.txt' } |
                ForEach-Object {
                    [IO.Path]::GetRelativePath($Top, $_.FullName).Replace('\', '/')
                }
            'cxx/CMakeLists.txt'
        )
    }
}

BeforeAll {
    $Root = Join-Path $PSScriptRoot '..'

    # A line without its comment, which starts with # in CMake and
    # PowerShell and with // in a SWIG interface.
    function Remove-Comment([string]$Path, [string]$Line) {
        if ($Path -like '*.i') {
            $Line -replace '//.*$', ''
        } else {
            $Line -replace '#.*$', ''
        }
    }

    # The lines of a file without their comments.
    function Get-Code([string]$Path) {
        @(Get-Content (Join-Path $Root $Path) |
            ForEach-Object { Remove-Comment $Path $_ })
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

    # A warning that only a public API change could clear, and that is only
    # about naming, may be turned off where it arises. The line, or the line
    # before it, then says "Naming exception:" and why, in at least ten
    # characters.
    $NamingException = 'Naming exception:\s*\S.{9,}'

    # Each line of the file that turns a warning off, as path:line: text.
    # With -Code the comments are left out before the patterns are applied.
    # Lines with a naming exception are printed and not returned.
    function Find-Hiding([string]$Path, [switch]$Code) {
        $lines = @(Get-Content (Join-Path $Root $Path))
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $text = $Code ? (Remove-Comment $Path $lines[$i]) : $lines[$i]
            $hides = $Hiding.Where({ $text -cmatch $_ }, 'First').Count -gt 0
            if (-not $hides) {
                continue
            }
            $where = "${Path}:$($i + 1): $($lines[$i].Trim())"
            if ($lines[$i] -cmatch $NamingException -or
                ($i -gt 0 -and $lines[$i - 1] -cmatch $NamingException)) {
                Write-Host "Naming exception accepted at $where"
            } else {
                $where
            }
        }
    }
}

Describe 'the <Engine> engine native build' -ForEach $Engines {
    BeforeAll {
        $CMake = "$Folder/CMakeLists.txt"
    }

    It 'turns no warning off in <_>' -ForEach @(
        'CMakeLists.txt', 'PreBuild.ps1', $Interface) {
        Find-Hiding "$Folder/$_" -Code | Should -BeNullOrEmpty
    }

    It 'needs a CMake that leaves the warning level out of the default flags' {
        $code = (Get-Code $CMake) -join "`n"
        $code -match 'cmake_minimum_required\(VERSION\s+(?<v>\d+\.\d+)[\s.)]' |
            Should -BeTrue
        [version]$Matches['v'] | Should -BeGreaterOrEqual ([version]'3.24')
    }

    It 'warns at /W4 and -Wall -Wextra on every target' {
        $before = (Get-LinesBeforeCxx $CMake) -join "`n"
        $before | Should -MatchExactly 'set\(_51DEGREES_WARNINGS\s+"\s*/W4"\)'
        $before | Should -MatchExactly 'set\(_51DEGREES_WARNINGS\s+"\s*-Wall -Wextra"\)'
        $before | Should -MatchExactly 'string\(APPEND\s+CMAKE_C_FLAGS\s+"\$\{_51DEGREES_WARNINGS\}"\)'
        $before | Should -MatchExactly 'string\(APPEND\s+CMAKE_CXX_FLAGS\s+"\$\{_51DEGREES_WARNINGS\}"\)'
    }

    # CMake 4.5 and later report each directory-wide directive as an author
    # warning, which the configure arguments make an error.
    It 'uses no directory-wide directive' {
        $code = (Get-Code $CMake) -join "`n"
        $code | Should -Not -Match ('(?m)^\s*(add_compile_options|add_link_options|' +
            'add_definitions|add_compile_definitions|include_directories|' +
            'link_directories|link_libraries)\s*\(')
    }

    It 'builds position independent code for every target' {
        $before = (Get-LinesBeforeCxx $CMake) -join "`n"
        $before | Should -Match 'set\(\s*CMAKE_POSITION_INDEPENDENT_CODE\s+ON\s*\)'
    }

    It 'warns at -Wall -Wextra on the wrapper' {
        $code = (Get-Code $CMake) -join "`n"
        $code | Should -MatchExactly 'target_compile_options\([^)]*"-Wall"[^)]*"-Wextra"[^)]*"-Werror"'
    }

    It 'makes every warning an error' {
        $before = (Get-LinesBeforeCxx $CMake) -join "`n"
        $before | Should -Match 'set\(\s*CMAKE_COMPILE_WARNING_AS_ERROR\s+ON\s*\)'
        $code = (Get-Code $CMake) -join "`n"
        $code | Should -MatchExactly 'target_compile_options\([^)]*"/WX"'
        $code | Should -MatchExactly 'target_compile_options\([^)]*"-Werror"'
        $code | Should -MatchExactly 'target_link_options\([^)]*/WX'
    }

    It 'makes every linker warning an error on every target' {
        $before = (Get-LinesBeforeCxx $CMake) -join "`n"
        $before | Should -MatchExactly 'set\(_51DEGREES_LINK_WARNINGS\s+"\s*/WX"\)'
        $before | Should -MatchExactly 'string\(APPEND\s+CMAKE_STATIC_LINKER_FLAGS\s+"[^"]*/WX'
        $before | Should -MatchExactly 'set\(_51DEGREES_LINK_WARNINGS\s+"\s*-Wl,--fatal-warnings"\)'
        $before | Should -MatchExactly 'set\(_51DEGREES_LINK_WARNINGS\s+"\s*-Wl,-fatal_warnings"\)'
        foreach ($kind in 'EXE', 'SHARED', 'MODULE') {
            $before | Should -MatchExactly (
                "string\(APPEND\s+CMAKE_${kind}_LINKER_FLAGS\s+`"\`$\{_51DEGREES_LINK_WARNINGS\}`"\)")
        }
    }

    # CMake 4.4 and later ignore these variables and warn when they are set.
    It 'leaves the deprecation variables to the configure arguments' {
        $code = (Get-Code $CMake) -join "`n"
        $code | Should -Not -MatchExactly 'CMAKE_(ERROR|WARN)_DEPRECATED'
    }

    It 'configures with CMake warnings as errors in PreBuild.ps1' {
        $code = Get-Code "$Folder/PreBuild.ps1"
        ($code -join "`n") | Should -MatchExactly (
            '\$CMakeWarnings\s*=\s*@\(&\s*"\$PSScriptRoot/\.\./\.\./ci/cmake-warning-args\.ps1"\)')
        $configures = @($code -cmatch '^\s*cmake\s+\.\./\.\./\.\.\s')
        $configures.Count | Should -Be 2
        $configures | ForEach-Object { $_ | Should -MatchExactly '\s@CMakeWarnings\s' }
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

Describe 'the package requirements build' {
    It 'configures with CMake warnings as errors' {
        $code = (Get-Code 'ci/build-package-requirements.ps1') -join "`n"
        $code | Should -MatchExactly (
            '\$extra\s*=\s*@\(&\s*"\$PSScriptRoot/cmake-warning-args\.ps1"\)')
        $code | Should -MatchExactly 'build-project\.ps1\s[^\n]*-ExtraArgs\s+\$extra\b'
    }
}

Describe 'ci/cmake-warning-args.ps1' {
    BeforeAll {
        $Script = Join-Path $Root 'ci/cmake-warning-args.ps1'
    }

    It 'names the developer warnings <Name> for CMake <Version>' -ForEach @(
        @{ Version = '3.24.0'; Name = '-Werror=dev' }
        @{ Version = '3.31.6'; Name = '-Werror=dev' }
        @{ Version = '4.3.2'; Name = '-Werror=dev' }
        @{ Version = '4.4'; Name = '-Werror=author' }
        @{ Version = '4.4.5'; Name = '-Werror=author' }
        @{ Version = '4.5.0'; Name = '-Werror=author' }
    ) {
        $written = @(& $Script -Version $Version)
        $written | Should -Be @($Name, '-Werror=deprecated')
    }

    It 'reads the version of the cmake on the path' -Skip:(
        -not (Get-Command cmake -ErrorAction SilentlyContinue)) {
        $written = @(& $Script)
        $written.Count | Should -Be 2
        $written[0] | Should -BeIn '-Werror=dev', '-Werror=author'
        $written[1] | Should -Be '-Werror=deprecated'
    }
}

Describe 'the cxx code the native engines build' {
    It 'has the cxx submodule checked out' {
        Join-Path $Root 'cxx/src/common/CMakeLists.txt' | Should -Exist
    }

    # Every line is read whole, comments included, because # starts a
    # preprocessor line in C and SWIG files.
    It 'turns no warning off in <_>' -ForEach $CxxFiles {
        Find-Hiding $_ | Should -BeNullOrEmpty
    }
}
