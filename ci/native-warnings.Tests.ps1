# Tests of the warnings the two on-premise engines' native libraries build
# with, run with Pester 5 or later. Every target warns at the level the cxx
# submodule builds its own tests with. When _51DEGREES_WARNINGS_AS_ERRORS is
# "true", in any case, as CI sets it, every C and C++ warning is an error on
# every compiler, and linker and CMake warnings are errors too. Nothing in
# these files or in the cxx code the engines' builds compile turns a warning
# off.

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

    # The top-level blocks of a CMake file that apply only when
    # _51DEGREES_WARNINGS_AS_ERRORS is "true", and the rest of the file
    # without them, each as one string.
    $Gate = '(?ms)^if \(FIFTYONE_WARNINGS_AS_ERRORS STREQUAL "true"\)\s*$(?<body>.*?)^(else|endif)\(\)'
    function Get-Gated([string]$Path) {
        $code = (Get-Code $Path) -join "`n"
        ([regex]::Matches($code, $Gate) | ForEach-Object { $_.Groups['body'].Value }) -join "`n"
    }
    function Get-Ungated([string]$Path) {
        $code = (Get-Code $Path) -join "`n"
        [regex]::Replace($code, $Gate, '')
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
        $before | Should -MatchExactly 'set\(FIFTYONE_WARNINGS\s+"\s*/W4"\)'
        $before | Should -MatchExactly 'set\(FIFTYONE_WARNINGS\s+"\s*-Wall -Wextra"\)'
        $before | Should -MatchExactly 'string\(APPEND\s+CMAKE_C_FLAGS\s+"\$\{FIFTYONE_WARNINGS\}"\)'
        $before | Should -MatchExactly 'string\(APPEND\s+CMAKE_CXX_FLAGS\s+"\$\{FIFTYONE_WARNINGS\}"\)'
    }

    # environment-variables.md keeps the _51DEGREES_ prefix for the names
    # of environment variables and build options.
    It 'names its own variables without the _51DEGREES_ prefix' {
        $code = (Get-Code $CMake) -join "`n"
        $own = [regex]::Matches($code, '\bset\(\s*(?<name>_51DEGREES_\w+)') |
            ForEach-Object { $_.Groups['name'].Value } |
            Where-Object { $_ -notin '_51DEGREES_BUILD_DEVICE_DETECTION',
                '_51DEGREES_BUILD_IP_INTELLIGENCE', '_51DEGREES_WARNINGS_AS_ERRORS' }
        $own | Should -BeNullOrEmpty
    }

    # cxx decides whether its own targets' warnings are errors from its
    # option of the same name, which must agree with the variable.
    It 'gives cxx the same answer for _51DEGREES_WARNINGS_AS_ERRORS' {
        $before = (Get-LinesBeforeCxx $CMake) -join "`n"
        $ungated = Get-Ungated $CMake
        $gated = Get-Gated $CMake
        $ungated | Should -MatchExactly 'set\(_51DEGREES_WARNINGS_AS_ERRORS\s+OFF\)'
        $gated | Should -MatchExactly 'set\(_51DEGREES_WARNINGS_AS_ERRORS\s+ON\)'
        ([regex]::Matches($before, 'set\(_51DEGREES_WARNINGS_AS_ERRORS\s')).Count |
            Should -Be 2
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

    It 'warns at /W4 and -Wall -Wextra on the wrapper' {
        $code = Get-Ungated $CMake
        $code | Should -MatchExactly 'target_compile_options\([^)]*"/W4"'
        $code | Should -MatchExactly 'target_compile_options\([^)]*"-Wall"[^)]*"-Wextra"'
    }

    It 'reads _51DEGREES_WARNINGS_AS_ERRORS from the environment, in any case' {
        $before = (Get-LinesBeforeCxx $CMake) -join "`n"
        $before | Should -MatchExactly (
            'string\(TOLOWER\s+"\$ENV\{_51DEGREES_WARNINGS_AS_ERRORS\}"\s+FIFTYONE_WARNINGS_AS_ERRORS\)')
        # Before cxx, and again for the wrapper.
        [regex]::Matches((Get-Code $CMake) -join "`n", $Gate).Count | Should -Be 2
        $before | Should -MatchExactly '(?m)^if \(FIFTYONE_WARNINGS_AS_ERRORS STREQUAL "true"\)'
    }

    It 'makes every warning an error when the variable is "true"' {
        $gated = Get-Gated $CMake
        $gated | Should -Match 'set\(\s*CMAKE_COMPILE_WARNING_AS_ERROR\s+ON\s*\)'
        $gated | Should -MatchExactly 'set_target_properties\([^)]*COMPILE_WARNING_AS_ERROR\s+ON'
        $gated | Should -MatchExactly 'target_compile_options\([^)]*"\$\{FIFTYONE_WARNINGS_ERROR\}"'
        $gated | Should -MatchExactly 'target_link_options\([^)]*"/WX"'
        $before = (Get-LinesBeforeCxx $CMake) -join "`n"
        $before | Should -MatchExactly 'set\(FIFTYONE_WARNINGS_ERROR\s+"/WX"\)'
        $before | Should -MatchExactly 'set\(FIFTYONE_WARNINGS_ERROR\s+"-Werror"\)'
    }

    It 'makes every linker warning an error on every target when the variable is "true"' {
        $before = (Get-LinesBeforeCxx $CMake) -join "`n"
        $before | Should -MatchExactly 'set\(FIFTYONE_LINK_WARNINGS\s+"\s*/WX"\)'
        $before | Should -MatchExactly 'set\(FIFTYONE_LINK_WARNINGS\s+"\s*-Wl,--fatal-warnings"\)'
        $before | Should -MatchExactly 'set\(FIFTYONE_LINK_WARNINGS\s+"\s*-Wl,-fatal_warnings"\)'
        $gated = Get-Gated $CMake
        $gated | Should -MatchExactly 'string\(APPEND\s+CMAKE_STATIC_LINKER_FLAGS\s+"[^"]*/WX'
        foreach ($kind in 'EXE', 'SHARED', 'MODULE') {
            $gated | Should -MatchExactly (
                "string\(APPEND\s+CMAKE_${kind}_LINKER_FLAGS\s+`"\`$\{FIFTYONE_LINK_WARNINGS\}`"\)")
        }
    }

    # A build from source by anyone else, without the variable, shows the
    # warnings and carries on.
    It 'makes no warning an error when the variable is not "true"' {
        $ungated = Get-Ungated $CMake
        # The switches are only named, in set(), outside the gate.
        $ungated = $ungated -replace 'set\(FIFTYONE_(WARNINGS_ERROR|LINK_WARNINGS)\s+"[^"]*"\)', ''
        $ungated | Should -Not -Match 'WARNING_AS_ERROR\s+ON'
        $ungated | Should -Not -Match '/WX|-Werror|fatal.warnings'
        $ungated | Should -Not -Match 'FIFTYONE_(WARNINGS_ERROR|LINK_WARNINGS)'
        $ungated | Should -Not -Match '_LINKER_FLAGS'
    }

    # CMake 4.4 and later ignore these variables and warn when they are set.
    It 'leaves the deprecation variables to the configure arguments' {
        $code = (Get-Code $CMake) -join "`n"
        $code | Should -Not -MatchExactly 'CMAKE_(ERROR|WARN)_DEPRECATED'
    }

    It 'configures with CMake warnings as errors in PreBuild.ps1 when the variable is "true"' {
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
    It 'configures with CMake warnings as errors when the variable is "true"' {
        $code = (Get-Code 'ci/build-package-requirements.ps1') -join "`n"
        $code | Should -MatchExactly (
            '\$extra\s*=\s*@\(&\s*"\$PSScriptRoot/cmake-warning-args\.ps1"\)')
        $code | Should -MatchExactly 'build-project\.ps1\s[^\n]*-ExtraArgs\s+\$extra\b'
    }
}

Describe 'ci/cmake-warning-args.ps1' {
    BeforeAll {
        $Script = Join-Path $Root 'ci/cmake-warning-args.ps1'
        $Saved = $env:_51DEGREES_WARNINGS_AS_ERRORS
        $env:_51DEGREES_WARNINGS_AS_ERRORS = 'true'
    }

    AfterAll {
        $env:_51DEGREES_WARNINGS_AS_ERRORS = $Saved
    }

    It 'writes the arguments when the variable is <_>' -ForEach @('true', 'TRUE', 'True') {
        $env:_51DEGREES_WARNINGS_AS_ERRORS = $_
        @(& $Script -Version '4.5.0') | Should -Be @('-Werror=author', '-Werror=deprecated')
        $env:_51DEGREES_WARNINGS_AS_ERRORS = 'true'
    }

    It 'writes nothing when the variable is <_>' -ForEach @($null, '', 'false', 'yes', 'on', '1') {
        $env:_51DEGREES_WARNINGS_AS_ERRORS = $_
        @(& $Script -Version '4.5.0') | Should -BeNullOrEmpty
        $env:_51DEGREES_WARNINGS_AS_ERRORS = 'true'
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

    # CMake 4.5 and later report each directory-wide directive as an author
    # warning, which the configure arguments make an error. The cxx the
    # submodule pins has the ones listed below in cxx/CMakeLists.txt. The
    # engine builds reach the two inside if (NOT MSVC) on Linux and macOS,
    # and the coverage one in a GCC Debug build. The sanitizer ones need
    # options the engines never set. The Linux image's CMake does not warn
    # about any of them, so they are allowed only while the macOS entries
    # install that older CMake, as ci/install-cmake.ps1 says. Any other
    # directive fails.
    It 'uses no directory-wide directive in <_>' -ForEach @(
        $CxxFiles | Where-Object { $_ -like '*CMakeLists.txt' }) {
        $directive = '^\s*(add_compile_options|add_link_options|' +
            'add_definitions|add_compile_definitions|include_directories|' +
            'link_directories|link_libraries)\s*\('
        $known = @(
            'add_compile_options(-fsanitize=address)'
            'add_link_options(-fsanitize=address)'
            'add_compile_options(-fsanitize=thread -fprofile-update=atomic)'
            'add_link_options(-fsanitize=thread)'
            'add_compile_options(-fprofile-update=prefer-atomic)'
            'include_directories(/usr/local/include)'
            'link_directories(/usr/local/lib)'
        )
        $path = $_
        $found = @(Get-Code $path | Where-Object { $_ -match $directive } |
            ForEach-Object { $_.Trim() })
        if ($path -eq 'cxx/CMakeLists.txt') {
            $found = @($found | Where-Object { $_ -notin $known })
        }
        $found | Should -BeNullOrEmpty
    }
}

Describe 'the CMake the macOS entries install' {
    BeforeAll {
        $Options = Get-Content (Join-Path $Root 'ci/options.json') -Raw |
            ConvertFrom-Json
        $Install = Get-Content (Join-Path $Root 'ci/install-cmake.ps1') -Raw
        $CxxCode = (Get-Code 'cxx/CMakeLists.txt') -join "`n"
    }

    It 'is set on every macOS entry and on no other' {
        foreach ($entry in $Options) {
            if ($entry.Image -like 'macos*') {
                $entry.CMakeVersion | Should -Not -BeNullOrEmpty -Because $entry.Name
            } else {
                $entry.PSObject.Properties.Name | Should -Not -Contain 'CMakeVersion' -Because $entry.Name
            }
        }
    }

    It 'is older than 4.5 and has its SHA-256 recorded' {
        foreach ($entry in @($Options | Where-Object CMakeVersion)) {
            [version]$entry.CMakeVersion | Should -BeLessThan ([version]'4.5')
            $Install | Should -Match ('"' + [regex]::Escape($entry.CMakeVersion) +
                '"\s*=\s*"[0-9a-f]{64}"')
        }
    }

    # When cxx no longer has the two directives, the pin and this test go.
    It 'is still needed by the directives in cxx/CMakeLists.txt' {
        $CxxCode | Should -Match '(?m)^\s*include_directories\(/usr/local/include\)'
    }
}
