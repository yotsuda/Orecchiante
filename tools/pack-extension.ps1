param(
    [string]$Out = "$PSScriptRoot\..\out\extension",   # the staging folder and the .vsix
    [string]$Models = "$PSScriptRoot\..\onnx"          # where the models onnx/models.json lists are (tools/fetch-models.ps1 puts them there)
)
# The reader as a VS Code extension (docs/ROADMAP.md, D2 and D4; the shape is Calamo's): orecchiante.exe
# published self-contained for win-x64 into extension/bin, the models beside it (where the program
# looks for them), its `orecchiante --version --json` (bin/orecchiante.json, which extension.js hands to Lily#),
# the licenses of what is bundled, and `vsce package --target win32-x64`.
# Packages only: it publishes nothing. The icon and the LICENSE are the repository's own (one copy of each).
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."
$ext = "$root\extension"
# the models as models.json says, each checked by its SHA-256 (a stale one is never packed)
$onnx = (Get-Content "$root\onnx\models.json" -Raw | ConvertFrom-Json).models.file
& "$PSScriptRoot\fetch-models.ps1" -Models $Models -Check | Out-Null

if (Test-Path $Out) { Remove-Item -Recurse -Force $Out }
$stage = "$Out\stage"
Copy-Item $ext $stage -Recurse
Copy-Item "$root\icon.png", "$root\LICENSE" $stage
$bin = "$stage\bin"
cmd /d /s /c "dotnet publish `"$root\src\Orecchiante`" -c Release -r win-x64 --self-contained -o `"$bin`" -v q -nologo < NUL"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Get-ChildItem $bin -Include *.pdb, *.lib -Recurse | Remove-Item
foreach ($name in $onnx) { Copy-Item "$Models\$name" $bin }

# The licenses of what is bundled: the VSIX is all a user receives, and the packages' copies stay on this machine.
$packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { "$HOME\.nuget\packages" }
$libraries = (Get-Content "$bin\orecchiante.deps.json" -Raw | ConvertFrom-Json).libraries.PSObject.Properties.Name
function PackageDir([string]$id) {
    $found = $libraries | Where-Object { $_ -like "$id/*" } | Select-Object -First 1
    if (-not $found) { throw "$id is not among the reader's libraries" }
    "$packages\$($id.ToLowerInvariant())\$($found.Split('/')[1])"
}
$runtime = (Get-Content "$bin\orecchiante.runtimeconfig.json" -Raw | ConvertFrom-Json).runtimeOptions.includedFrameworks[0].version
$copies = @{
    'dotnet-LICENSE.txt'                = "$packages\microsoft.netcore.app.runtime.win-x64\$runtime\LICENSE.TXT"
    'dotnet-THIRD-PARTY-NOTICES.txt'    = "$packages\microsoft.netcore.app.runtime.win-x64\$runtime\THIRD-PARTY-NOTICES.TXT"
    'onnxruntime-LICENSE.txt'           = "$(PackageDir 'Microsoft.ML.OnnxRuntime')\LICENSE"
    'onnxruntime-ThirdPartyNotices.txt' = "$(PackageDir 'Microsoft.ML.OnnxRuntime')\ThirdPartyNotices.txt"
}
# (NAudio's packages carry no license file: its MIT text is in THIRD-PARTY-NOTICES.md; this only checks it is still there to name)
PackageDir 'NAudio.Wasapi' | Out-Null
# (NMeCab's carry none either: its LGPL-2.1 is extension/licenses/LGPL-2.1.txt; IPAdic's own COPYING comes with it, bin/IpaDic)
PackageDir 'LibNMeCab' | Out-Null
if (-not (Test-Path "$bin\IpaDic\sys.dic") -or -not (Test-Path "$bin\IpaDic\COPYING")) { throw "IPAdic is not in bin\IpaDic" }
New-Item -ItemType Directory -Force "$bin\licenses" | Out-Null
foreach ($name in $copies.Keys) { Copy-Item -LiteralPath $copies[$name] "$bin\licenses\$name" }

# (recordings are never ours to give: none goes into a package)
$recordings = Get-ChildItem $stage -Recurse -File | Where-Object Extension -in '.wav', '.mp3', '.flac', '.m4a', '.aac', '.mp4', '.wma'
if ($recordings) { throw "recordings in the package: $($recordings.FullName -join ', ')" }

# What the reader says of itself, beside it: extension.js hands it to Lily# without starting anything.
$info = & "$bin\orecchiante.exe" --version --json
if ($LASTEXITCODE -ne 0) { throw "orecchiante --version --json failed" }
$version = (Get-Content "$stage\package.json" -Raw | ConvertFrom-Json).version
if ($version -ne ($info | ConvertFrom-Json).version) { throw "extension/package.json is $version, the reader $(($info | ConvertFrom-Json).version): make them one" }
[IO.File]::WriteAllText("$bin\orecchiante.json", $info)

$size = (Get-ChildItem $bin -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
"reader {0}, bin {1:F1} MB" -f $version, $size
Push-Location $stage
try {
    cmd /d /s /c "npx --yes @vscode/vsce package --target win32-x64 --out `"$Out`" < NUL"
    if ($LASTEXITCODE -ne 0) { throw "vsce package failed" }
} finally { Pop-Location }
Get-ChildItem $Out -Filter *.vsix | ForEach-Object { "{0} {1:F1} MB" -f $_.Name, ($_.Length / 1MB) }
