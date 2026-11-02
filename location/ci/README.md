# API Specific CI/CD Approach
This API complies with the `common-ci` approach.

The following secrets are required:
* `ACCESS_TOKEN` - GitHub [access token](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens#about-personal-access-tokens) for cloning repos, creating PRs, etc.
    * Example: `github_pat_l0ng_r4nd0m_s7r1ng`

The following secrets are requred to run cloud tests:
* `SUPER_RESOURCE_KEY` - [resource key](https://51degrees.com/documentation/_info__resource_keys.html?utm_source=github&utm_medium=readme&utm_campaign=location-dotnet&utm_content=ci-readme.md&utm_term=api-specific-ci-cd-approach) for integration tests
    * Example: `R4nd0m-S7r1ng`

The following secrets are required for publishing releases (this should only be needed by 51Degrees):
* `NUGET_API_KEY` - 51Degrees NuGet API key used for publishing
* `CODE_SIGNING_CERT_ALIAS` - Name of the 51Degrees code signing certificate alias
* `CODE_SIGNING_CERT_PASSWORD` - Password for the `CODE_SIGNING_CERT`
* `CODE_SIGNING_CERT` - String containing the 51Degrees code signing certificate in PFX format
