# Tests for ci/check-environment-variable-names.ps1.
#
# Each case is one line of text and what the check must make of it. The
# digit-initial name is matched in any case, because Windows reads an
# environment variable name in any case, so a lower case old name is still the
# old name there. The old names keep their case, for the reason the check
# gives.

BeforeAll {
    $script:Check = Join-Path $PSScriptRoot 'check-environment-variable-names.ps1'
    . $script:Check

    function Get-Kind([string]$Line) {
        @(Find-EnvironmentNameProblem $Line | ForEach-Object { $_.Kind })
    }
}

Describe 'Find-EnvironmentNameProblem' {

    It 'finds the digit-initial name <Line>' -ForEach @(
        @{ Line = '$env:51DEGREES_IPI_PATH = $file' }
        @{ Line = '${env:51DEGREES_IPI_PATH} = $env:_51DEGREES_IPI_PATH' }
        @{ Line = 'Environment.GetEnvironmentVariable("51DEGREES_RESOURCE_KEY")' }
        @{ Line = 'the 51degrees_resource_key environment variable' }
        @{ Line = '$env:51Degrees_Ipi_Path = "file.ipi"' }
    ) {
        Get-Kind $Line | Should -Contain 'digit'
    }

    It 'finds the old name <Line>' -ForEach @(
        @{ Line = '$env:SUPER_RESOURCE_KEY = $Keys.TestResourceKey' }
        @{ Line = 'private const string Legacy = "SUPER_RESOURCE_KEY";' }
        @{ Line = 'Environment.GetEnvironmentVariable("FIFTYONE_CLOUD_ENDPOINT")' }
        @{ Line = 'public const string CLOUD_END_POINT_ENV_VAR = "51D_CLOUD_ENDPOINT";' }
        @{ Line = 'private const string Legacy = "51D_RESOURCE_KEY";' }
        @{ Line = 'ExampleUtils.GetKeyFromEnv("RESOURCE_KEY_CLOUD_V5_BESPOKE", v => k = v);' }
        @{ Line = '$env:DEVICEDETECTIONLICENSEKEY_DOTNET = $Keys.DeviceDetection' }
        @{ Line = 'public const string ENV = "IPINTELLIGENCEDATAFILE";' }
        @{ Line = 'public const string ENV = "EVIDENCEFILE";' }
        @{ Line = 'public const string ENV = "GEOIPEVIDENCEFILE";' }
        @{ Line = 'if ($env:FIFTYONE_TEST_FILTER) { $filter = $env:FIFTYONE_TEST_FILTER }' }
        @{ Line = 'Environment.GetEnvironmentVariable("FOD_CLOUD_API_URL")' }
        @{ Line = 'Environment.GetEnvironmentVariable("RESOURCE_KEY")' }
        @{ Line = 'Environment.GetEnvironmentVariable("LICENSE_KEY")' }
        @{ Line = '$env:RESOURCE_KEY = $Keys.TestResourceKey' }
        @{ Line = 'public const string LEGACY_CLOUD_RESOURCE_KEY_ENV_VAR = "x";' }
        @{ Line = 'public const string RESOURCE_KEY_ENV_VAR = "RESOURCE_KEY";' }
    ) {
        Get-Kind $Line | Should -Contain 'fallback'
    }

    # The old names keep their case because several differ from a class or a
    # property name by case alone, and a class is not a variable. 51D_ is also
    # the prefix of the evidence, cookie and query names the pipeline reads,
    # which are on the wire rather than in the environment.
    It 'leaves <Line> alone' -ForEach @(
        @{ Line = 'public class IpIntelligenceDataFile : AspectEngineDataFile' }
        @{ Line = 'var evidenceFile = Path.Combine(dir, "evidence.yml");' }
        @{ Line = 'evidence.Add("query.51D_ProfileIds", ids);' }
        @{ Line = 'cookies["51D_stagetwo"] = value;' }
        @{ Line = 'Environment.GetEnvironmentVariable("_51DEGREES_RESOURCE_KEY")' }
        @{ Line = '$env:_51DEGREES_IPI_PATH = "$RepoPath/file.ipi"' }
        @{ Line = '$env:_51DEGREES_RESOURCE_KEY_BESPOKE = $Keys.TestResourceKey' }
        @{ Line = 'public const string RESOURCE_KEY_ENV_VAR = "_51DEGREES_RESOURCE_KEY";' }
        @{ Line = 'Set !!YOUR_RESOURCE_KEY!! in appsettings.json' }
        @{ Line = 'var paidResourceKey = PAID_RESOURCE_KEY;' }
    ) {
        Get-Kind $Line | Should -BeNullOrEmpty
    }
}

Describe 'the check over a repository' {

    # git quotes a path holding a non-ASCII character unless told not to,
    # and a quoted path is not found on disk.
    It 'reads a file whose name is not ASCII' {
        $repo = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid())
        New-Item -ItemType Directory $repo | Out-Null
        try {
            git -C $repo init -q
            $name = 'Donn' + [char]0xE9 + 'es.cs'
            Set-Content -LiteralPath (Join-Path $repo $name) `
                -Value 'Environment.GetEnvironmentVariable("51DEGREES_IPI_PATH");'
            git -C $repo add -A
            { & $script:Check -RepoRoot $repo } |
                Should -Throw '1 names start with a digit*'
        } finally {
            Remove-Item -Recurse -Force $repo
        }
    }
}
