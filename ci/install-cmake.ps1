# Installs one exact CMake release from Kitware on a macOS runner and puts it
# first on the path, for the rest of this process.
#
# The macOS entries in ci/options.json use the CMake version of the Linux
# runner image, because the newer CMake on the macOS image stops on the
# directory-wide commands at cxx/CMakeLists.txt lines 198 and 199, in the cxx
# the submodule pins. CMake 4.5 reports each of them as an author warning,
# which ci/cmake-warning-args.ps1 makes an error. The Windows builds never
# reach those lines, as they sit inside if (NOT MSVC). This goes when the cxx
# change that removes those commands, 51Degrees/51degrees-cxx#247, reaches
# this repository.
#
# The download is checked against the SHA-256 that Kitware publishes, in the
# signed cmake-<version>-SHA-256.txt of the release.
param (
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$Sha256 = @{
    "3.31.6" = "330b9514f5112e5ed4fb08b8b05803b776fd9b539a6ae12927d14dcc0ee2ba8d"
}

if (-not $IsMacOS) {
    throw "CMake is only installed this way on macOS"
}
if (-not $Sha256.ContainsKey($Version)) {
    throw "No SHA-256 is recorded for CMake $Version"
}

$name = "cmake-$Version-macos-universal"
$url = "https://github.com/Kitware/CMake/releases/download/v$Version/$name.tar.gz"
$root = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$folder = Join-Path $root "cmake-$Version"
$archive = Join-Path $root "$name.tar.gz"

Write-Host "Downloading $url"
Invoke-WebRequest -Uri $url -OutFile $archive
$hash = (Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant()
if ($hash -ne $Sha256[$Version]) {
    throw "The SHA-256 of $name.tar.gz is $hash, not $($Sha256[$Version])"
}

New-Item -ItemType Directory -Force $folder | Out-Null
tar -xzf $archive -C $folder
$bin = Join-Path $folder "$name/CMake.app/Contents/bin"
$env:PATH = "$bin$([IO.Path]::PathSeparator)$env:PATH"

$first = @(cmake --version)[0]
if ((-split $first)[-1] -ne $Version) {
    throw "Expected CMake $Version first on the path, found '$first'"
}
Write-Host "$first is first on the path, from $bin"
