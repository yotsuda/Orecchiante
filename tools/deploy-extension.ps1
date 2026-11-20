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

# (a reader still running from the installed extension holds its orecchiante.exe: the old folder cannot go)
$running = Get-Process orecchiante -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*yotsuda.orecchiante*' }
if ($running) { throw "orecchiante.exe is running from the installed extension (pid $($running.Id -join ', ')): let it finish first" }

"installing {0} into {1}" -f $vsix.Name, $Code
cmd /d /s /c "`"$Code`" --install-extension `"$($vsix.FullName)`" --force < NUL"
if ($LASTEXITCODE -ne 0) { throw "code --install-extension failed" }
cmd /d /s /c "`"$Code`" --list-extensions --show-versions < NUL" | Select-String '^yotsuda\.' | ForEach-Object { "  $_" }

# The name under the extension's title: VS Code shows the publisher's display name from the Marketplace, and an
# extension not published there gets only "yotsuda". The display name is written into the record of what is
# installed (extensions.json, the entry's metadata), which VS Code shows when the Marketplace has nothing
# (the owner, 2026-11-20: "Yoshifumi Tsuda", as the extensions published are shown). Each install writes the entry
# anew, so this runs after each.
$id = 'yotsuda.' + (Get-Content "$PSScriptRoot\..\extension\package.json" -Raw | ConvertFrom-Json).name
$record = "$HOME\.vscode\extensions\extensions.json"
if (Test-Path $record) {
    $entries = Get-Content $record -Raw | ConvertFrom-Json
    $entry = $entries | Where-Object { $_.identifier.id -eq $id } | Select-Object -Last 1
    if ($entry -and $entry.metadata) {
        $entry.metadata | Add-Member -NotePropertyName publisherDisplayName -NotePropertyValue 'Yoshifumi Tsuda' -Force
        [IO.File]::WriteAllText($record, (ConvertTo-Json -InputObject @($entries) -Depth 20 -Compress), [Text.UTF8Encoding]::new($false))
        "  $id shown as by Yoshifumi Tsuda"
    } else { "  (no entry for $id in ${record}: its publisher is shown as yotsuda)" }
}
"done: reload the VS Code windows (Developer: Reload Window) to use the new reader"
