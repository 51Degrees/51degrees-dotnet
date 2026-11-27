# Writes the arguments that make CMake's own developer and deprecation
# warnings errors when a project is configured. CMake 4.4 and later call the
# developer warnings "author" and report -Werror=dev as deprecated, and
# earlier versions do not know -Werror=author, so the argument depends on the
# version of cmake on the path, or on -Version when it is given.
#
# Warnings are errors only when the _51DEGREES_WARNINGS_AS_ERRORS environment
# variable is "true", in any case, as CI sets it. Otherwise nothing is
# written, so a build from source by anyone else shows CMake's warnings and
# carries on.
param (
    [version]$Version
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

if ("$env:_51DEGREES_WARNINGS_AS_ERRORS" -ne "true") {
    return
}

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
