param (
    # The folder that holds the test results files of a run.
    [Parameter(Mandatory)][string]$ResultPath
)
$ErrorActionPreference = "Stop"

# Gives the lines the tests wrote for the build log, under a heading, or
# nothing when there are none. A test that measures something writes a line
# starting "Build log: " to its output. The output of a test that passes is
# kept only in its results file, so the lines are read from the results files
# for the caller to print where the job log shows them.

$Pattern = 'Build log: [^<\r\n]*'
if (-not (Test-Path -LiteralPath $ResultPath)) {
    return
}
$lines = @(Get-ChildItem -LiteralPath $ResultPath -Filter "*.trx" -File |
    Sort-Object Name |
    ForEach-Object {
        $file = $_.Name
        Select-String -LiteralPath $_.FullName -Pattern $Pattern -AllMatches |
            ForEach-Object { $_.Matches } |
            ForEach-Object {
                # The results file is XML, so the text is decoded.
                "  $([Net.WebUtility]::HtmlDecode($_.Value))  ($file)"
            }
    })
if ($lines.Count -eq 0) {
    return
}
"Lines the tests wrote for the build log:"
$lines
