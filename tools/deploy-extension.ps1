param(
    [string]$Code,          # the VS Code CLI (code.cmd); found on the PATH or in the user install when not given
    [switch]$NoPack,        # install the .vsix already in out/extension instead of packing it again
    [string]$Out = "$PSScriptRoot\..\out\extension"
)
# Packs the extension (pack-extension.ps1) and installs it into this PC's VS Code, over any earlier one.
# Never publishes.
$ErrorActionPreference = 'Stop'

if (-not $NoPack) {
    & "$PSScriptRoot\pack-extension.ps1" -Out $Out
}
$vsix = Get-ChildItem $Out -Filter *.vsix -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $vsix) { throw "no .vsix in ${Out}: run without -NoPack" }

if (-not $Code) {
    $Code = (Get-Command code.cmd -ErrorAction SilentlyContinue).Source
    if (-not $Code) { $Code = "$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd" }
}
if (-not (Test-Path $Code)) { throw "no VS Code CLI at ${Code}: pass -Code <path to code.cmd>" }

# (a reader still running from the installed extension holds its dettato.exe: the old folder cannot go)
$running = Get-Process dettato -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*yotsuda.dettato*' }
if ($running) { throw "dettato.exe is running from the installed extension (pid $($running.Id -join ', ')): let it finish first" }

"installing {0} into {1}" -f $vsix.Name, $Code
cmd /d /s /c "`"$Code`" --install-extension `"$($vsix.FullName)`" --force < NUL"
if ($LASTEXITCODE -ne 0) { throw "code --install-extension failed" }
cmd /d /s /c "`"$Code`" --list-extensions --show-versions < NUL" | Select-String '^yotsuda\.' | ForEach-Object { "  $_" }
"done: reload the VS Code windows (Developer: Reload Window) to use the new reader"
