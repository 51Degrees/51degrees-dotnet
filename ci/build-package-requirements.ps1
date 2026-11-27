param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Arch = "x64"
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

# Every warning is an error in the builds 51Degrees runs. Each CI script
# that builds sets this until the shared workflows set it in every job.
$env:_51DEGREES_WARNINGS_AS_ERRORS = "true"

. "$PSScriptRoot/packages.ps1"

# Builds the native library of each on-premise engine for this platform and
# puts it where build-package.ps1 collects it from, as
# package-files/<os>/<arch>/<library>. The matrix runs this once per platform
# and the build job downloads every result.
$os = $IsWindows ? "windows" : ($IsLinux ? "linux" : "macos")
$RepoPath = [IO.Path]::Combine($pwd, $RepoName)

foreach ($engine in $NativeEngines) {
    Write-Host "::group::Building $($engine.Library) for $os $Arch"
    # CMake's own developer and deprecation warnings are errors too.
    $extra = @(& "$PSScriptRoot/cmake-warning-args.ps1")
    if ($Arch -eq "x86") {
        $extra += "-D_51DEGREES_32BIT=on"
    }
    ./cxx/build-project.ps1 -RepoName $RepoName -ProjectDir $engine.Project -Configuration "Release" -Arch $Arch -ExtraArgs $extra
    $library = Get-ChildItem -Recurse -File -Path "$RepoPath/$($engine.Project)/build" -Filter $engine.Library | Select-Object -First 1
    if (-not $library) {
        throw "$($engine.Library) was not built"
    }
    $target = New-Item -ItemType Directory -Force "package-files/$os/$Arch"
    Copy-Item $library.FullName "$target/$($engine.Library)" -Force
    Remove-Item -Recurse -Force "$RepoPath/$($engine.Project)/build"
    Write-Host "::endgroup::"
}
