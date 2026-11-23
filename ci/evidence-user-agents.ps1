# Writes the User-Agent of each record of a YAML evidence file, such as
# '20000 Evidence Records.yml', to a text file with one on each line, which is
# what the shared httpbench script sends. A record with no User-Agent is left
# out.
param (
    [Parameter(Mandatory)][string]$EvidencePath,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = "Stop"

$EvidencePath = (Resolve-Path -LiteralPath $EvidencePath).ProviderPath
$OutputPath = [IO.Path]::GetFullPath($OutputPath, (Get-Location).ProviderPath)

$prefix = 'header.user-agent: '
$writer = [IO.StreamWriter]::new($OutputPath)
try {
    foreach ($line in [IO.File]::ReadLines($EvidencePath)) {
        if (-not $line.StartsWith($prefix, [StringComparison]::Ordinal)) {
            continue
        }
        $value = $line.Substring($prefix.Length)
        # A value YAML has to quote is in single quotes, with each quote
        # inside it doubled.
        if ($value.Length -ge 2 -and $value.StartsWith("'") -and $value.EndsWith("'")) {
            $value = $value.Substring(1, $value.Length - 2).Replace("''", "'")
        }
        $writer.WriteLine($value)
    }
} finally {
    $writer.Dispose()
}
