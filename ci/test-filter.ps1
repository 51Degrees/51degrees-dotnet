# Writes the test filter the environment gives a run, or nothing when it
# gives none. _51DEGREES_TEST_FILTER is read first. FIFTYONE_TEST_FILTER is
# its old name, read only when the new one is not set, with a warning.
$TestFilter = $env:_51DEGREES_TEST_FILTER
if (-not $TestFilter -and $env:FIFTYONE_TEST_FILTER) {
    Write-Warning ("The environment variable FIFTYONE_TEST_FILTER is " +
        "deprecated and _51DEGREES_TEST_FILTER replaces it. Set " +
        "_51DEGREES_TEST_FILTER to the same value instead.")
    $TestFilter = $env:FIFTYONE_TEST_FILTER
}
if ($TestFilter) {
    $TestFilter
}
