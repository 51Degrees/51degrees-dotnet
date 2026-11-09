# Builds the .NET Framework web solution with msbuild and runs its old style
# test project with vstest, as CI does on Windows.
$ErrorActionPreference = 'Stop'

$vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath
$msbuild = Join-Path $vs 'MSBuild\Current\Bin\MSBuild.exe'
$vstest = Join-Path $vs 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe'

& $msbuild pipeline/FiftyOne.Pipeline.Web.sln -t:build -restore -verbosity:minimal `
    '-p:Configuration=Release' '-p:Platform=Any CPU' '-p:FiftyOneReferences=Project'
if ($LASTEXITCODE -ne 0) { throw 'msbuild failed' }

$dll = Get-ChildItem -Recurse pipeline/Tests/Web.Framework.Tests -Filter 'FiftyOne.Pipeline.Web.Framework.Tests.dll' |
    Where-Object { $_.FullName -match '\\bin\\' } | Select-Object -First 1
Write-Host "Testing $($dll.FullName)"
& $vstest $dll.FullName /Logger:console`;verbosity=normal
exit $LASTEXITCODE
