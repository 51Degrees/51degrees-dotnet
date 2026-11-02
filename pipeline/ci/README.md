# API Specific CI/CD Approach
This API complies with the `common-ci` approach with the following exceptions:

The following secrets are required:
* `ACCESS_TOKEN` - GitHub [access token](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens#about-personal-access-tokens) for cloning repos, creating PRs, etc.
    * Example: `github_pat_l0ng_r4nd0m_s7r1ng`

The following secrets are required for publishing releases (this should only be needed by 51Degrees):
* `NUGET_API_KEY` - 51Degrees NuGet API key used for publishing
* `CODE_SIGNING_CERT_ALIAS` - Name of the 51Degrees code signing certificate alias
* `CODE_SIGNING_CERT_PASSWORD` - Password for the `CODE_SIGNING_CERT`
* `CODE_SIGNING_CERT` - String containing the 51Degrees code signing certificate in PFX format

## General
The project contains multiple solutions, and these are hardcoded in the scripts, allowing iteration by passing the solution name to the script through the ProjectDir parameter.

## Performance Tests

The script sets up the required directories to store the test results and builds the performance tests project using publish command to produce an executable file that is then passed to the script. If the operating system is Linux, it installs the APR library needed for the tests. It then proceeds to build the performance tests using CMake and generates the necessary executable.

From the generated "summary.json" file, relevant data is extracted to create a performance summary in JSON format. This summary includes DetectionsPerSecond and MsPerDetection metrics.
