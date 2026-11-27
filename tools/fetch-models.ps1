param(
    [string]$Models = "$PSScriptRoot/../onnx",     # where the models go (and onnx/models.json is read from)
    [switch]$Check                                 # only say which are missing or differ; fetch nothing
)
# The models the reader runs, as onnx/models.json lists them: each one not already there with its SHA-256 is
# fetched from its url (an asset of a GitHub Release of this repository) and checked before it takes its place.
# The only network this does is those downloads. The release workflow runs it before packing; run it once after
# cloning, and again when models.json changes.
$ErrorActionPreference = 'Stop'
$list = Get-Content "$PSScriptRoot/../onnx/models.json" -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force $Models | Out-Null
$bad = @()
foreach ($m in $list.models) {
    $path = Join-Path $Models $m.file
    if ((Test-Path $path) -and (Get-FileHash $path -Algorithm SHA256).Hash -eq $m.sha256) { "{0}: there" -f $m.file; continue }
    if ($Check) { $bad += $m.file; "{0}: {1}" -f $m.file, $(if (Test-Path $path) { 'differs' } else { 'missing' }); continue }
    "{0}: fetching {1:F1} MB" -f $m.file, ($m.bytes / 1MB)
    $part = "$path.part"
    Invoke-WebRequest $m.url -OutFile $part -UseBasicParsing
    $hash = (Get-FileHash $part -Algorithm SHA256).Hash
    if ($hash -ne $m.sha256) { Remove-Item $part; throw "$($m.file): its SHA-256 is $hash, models.json says $($m.sha256)" }
    Move-Item $part $path -Force
}
if ($bad) { throw "not as models.json says: $($bad -join ', ') (run tools/fetch-models.ps1)" }
