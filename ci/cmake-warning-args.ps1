# Writes the arguments that make CMake's own developer and deprecation
# warnings errors when a project is configured. CMake 4.4 and later call the
# developer warnings "author" and report -Werror=dev as deprecated, and
# earlier versions do not know -Werror=author, so the argument depends on the
# version of cmake on the path, or on -Version when it is given.
param (
    [version]$Version
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$AuthorWarningsVersion = [version]"4.4"

if (-not $Version) {
    $first = @(cmake --version)[0]
    if ($first -notmatch 'cmake version (?<v>\d+\.\d+(\.\d+)?)') {
        throw "Cannot read the CMake version from '$first'"
    }
    $Version = [version]$Matches['v']
}

if ($Version -ge $AuthorWarningsVersion) {
    "-Werror=author"
} else {
    "-Werror=dev"
}
"-Werror=deprecated"
