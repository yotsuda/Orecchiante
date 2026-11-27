param(
    [string]$Out = "$PSScriptRoot/../out/extension",   # the staging folder and the .vsix
    [string]$Models = "$PSScriptRoot/../onnx",         # where the models onnx/models.json lists are (tools/fetch-models.ps1 puts them there)
    [ValidateSet('win-x64', 'osx-arm64', 'osx-x64')]
    [string]$Rid = 'win-x64'                           # the system packed for: a macOS one is packed on a Mac (the reader is signed there)
)
# The reader as a VS Code extension (docs/ROADMAP.md, D2 and D4; the shape is Calamo's): orecchiante(.exe)
# published self-contained for the system into extension/bin, the models beside it (where the program
# looks for them), its `orecchiante --version --json` (bin/orecchiante.json, which extension.js hands to Lily#),
# the licenses of what is bundled, and `vsce package --target <win32-x64|darwin-arm64|darwin-x64>`.
# Packages only: it publishes nothing. The icon and the LICENSE are the repository's own (one copy of each).
$ErrorActionPreference = 'Stop'
$target = @{ 'win-x64' = 'win32-x64'; 'osx-arm64' = 'darwin-arm64'; 'osx-x64' = 'darwin-x64' }[$Rid]
$exe = if ($Rid -like 'win-*') { 'orecchiante.exe' } else { 'orecchiante' }
if ($Rid -like 'osx-*' -and -not $IsMacOS) { throw "$Rid is packed on a Mac: the reader is signed as it is built there (Apple silicon runs nothing unsigned)" }
$root = (Resolve-Path "$PSScriptRoot/..").Path
$ext = Join-Path $root 'extension'
# (a command run with no input: on Windows through cmd < NUL, which keeps a console's input from reaching it)
function Run([string]$file, [string[]]$arguments) {
    if ($IsWindows -or $env:OS -eq 'Windows_NT') {
        $line = ($arguments | ForEach-Object { if ($_ -match '[\s"]') { '"' + $_.Replace('"', '\"') + '"' } else { $_ } }) -join ' '
        cmd /d /s /c "$file $line < NUL"
    }
    else { & $file @arguments }
}
# the models as models.json says, each checked by its SHA-256 (a stale one is never packed)
$onnx = (Get-Content (Join-Path $root 'onnx/models.json') -Raw | ConvertFrom-Json).models.file
& "$PSScriptRoot/fetch-models.ps1" -Models $Models -Check | Out-Null

if (Test-Path $Out) { Remove-Item -Recurse -Force $Out }
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$stage = Join-Path $Out 'stage'
Copy-Item $ext $stage -Recurse
Copy-Item (Join-Path $root 'icon.png'), (Join-Path $root 'LICENSE') $stage
$bin = Join-Path $stage 'bin'
Run dotnet @('publish', (Join-Path $root 'src/Orecchiante'), '-c', 'Release', '-r', $Rid, '--self-contained', '-o', $bin, '-v', 'q', '-nologo')
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Get-ChildItem $bin -Include *.pdb, *.lib -Recurse | Remove-Item
foreach ($name in $onnx) { Copy-Item (Join-Path $Models $name) $bin }

# The licenses of what is bundled: the VSIX is all a user receives, and the packages' copies stay on this machine.
$packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget/packages' }
$libraries = (Get-Content (Join-Path $bin 'orecchiante.deps.json') -Raw | ConvertFrom-Json).libraries.PSObject.Properties.Name
function PackageDir([string]$id) {
    $found = $libraries | Where-Object { $_ -like "$id/*" } | Select-Object -First 1
    if (-not $found) { throw "$id is not among the reader's libraries" }
    Join-Path $packages "$($id.ToLowerInvariant())/$($found.Split('/')[1])"
}
$runtime = (Get-Content (Join-Path $bin 'orecchiante.runtimeconfig.json') -Raw | ConvertFrom-Json).runtimeOptions.includedFrameworks[0].version
$dotnet = Join-Path $packages "microsoft.netcore.app.runtime.$Rid/$runtime"
$copies = @{
    'dotnet-LICENSE.txt'                = Join-Path $dotnet 'LICENSE.TXT'
    'dotnet-THIRD-PARTY-NOTICES.txt'    = Join-Path $dotnet 'THIRD-PARTY-NOTICES.TXT'
    'onnxruntime-LICENSE.txt'           = Join-Path (PackageDir 'Microsoft.ML.OnnxRuntime') 'LICENSE'
    'onnxruntime-ThirdPartyNotices.txt' = Join-Path (PackageDir 'Microsoft.ML.OnnxRuntime') 'ThirdPartyNotices.txt'
}
# (NAudio's packages carry no license file: its MIT text is in THIRD-PARTY-NOTICES.md; this only checks it is still there to name)
PackageDir 'NAudio.Wasapi' | Out-Null
# (NMeCab's carry none either: its LGPL-2.1 is extension/licenses/LGPL-2.1.txt; IPAdic's own COPYING comes with it, bin/IpaDic)
PackageDir 'LibNMeCab' | Out-Null
if (-not (Test-Path (Join-Path $bin 'IpaDic/sys.dic')) -or -not (Test-Path (Join-Path $bin 'IpaDic/COPYING'))) { throw "IPAdic is not in bin/IpaDic" }
New-Item -ItemType Directory -Force (Join-Path $bin 'licenses') | Out-Null
foreach ($name in $copies.Keys) { Copy-Item -LiteralPath $copies[$name] (Join-Path $bin "licenses/$name") }

# (recordings are never ours to give: none goes into a package)
$recordings = Get-ChildItem $stage -Recurse -File | Where-Object Extension -in '.wav', '.mp3', '.flac', '.m4a', '.aac', '.mp4', '.wma'
if ($recordings) { throw "recordings in the package: $($recordings.FullName -join ', ')" }

# What the reader says of itself, beside it: extension.js hands it to Lily# without starting anything.
$info = & (Join-Path $bin $exe) --version --json
if ($LASTEXITCODE -ne 0) { throw "orecchiante --version --json failed" }
$version = (Get-Content (Join-Path $stage 'package.json') -Raw | ConvertFrom-Json).version
if ($version -ne ($info | ConvertFrom-Json).version) { throw "extension/package.json is $version, the reader $(($info | ConvertFrom-Json).version): make them one" }
[IO.File]::WriteAllText((Join-Path $bin 'orecchiante.json'), $info)

$size = (Get-ChildItem $bin -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
"reader {0} for {1}, bin {2:F1} MB" -f $version, $target, $size
Push-Location $stage
try {
    Run npx @('--yes', '@vscode/vsce', 'package', '--target', $target, '--out', $Out)
    if ($LASTEXITCODE -ne 0) { throw "vsce package failed" }
} finally { Pop-Location }
Get-ChildItem $Out -Filter *.vsix | ForEach-Object { "{0} {1:F1} MB" -f $_.Name, ($_.Length / 1MB) }
