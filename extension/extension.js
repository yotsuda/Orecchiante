// Dettato: carries the reader (bin/dettato.exe, with its two models beside it) and one command that runs it
// on a recording and opens the chart. Starts only when the command is run or another extension asks for it:
// what activate returns (the reader's path, version and protocol) is for the Lily# extension, as Calamo's is
// (vscode.extensions.getExtension('yotsuda.dettato')). The command here stays until Lily# has the menu
// (docs/ROADMAP.md, D4).
'use strict';

const cp = require('child_process');
const fs = require('fs');
const path = require('path');
const vscode = require('vscode');

let log;

/**
 * Runs the reader on one recording (`dettato <recording> -o <chart> --force`: the two files are written at the
 * end, whole or not at all). Resolves to its exit code and the last line it said (undefined when cancelled).
 */
function read(exe, recording, chart, token) {
    return new Promise((resolve, reject) => {
        const child = cp.spawn(exe, [recording, '-o', chart, '--force'], { windowsHide: true });
        let cancelled = false;
        let said = '';
        const stop = token.onCancellationRequested(() => { cancelled = true; child.kill(); });
        child.stdout.on('data', text => log.append(text.toString()));
        child.stderr.on('data', text => { said += text.toString(); log.append(text.toString()); });
        child.on('error', error => { stop.dispose(); reject(error); });
        child.on('close', code => {
            stop.dispose();
            const lines = said.split(/\r?\n/).filter(line => line.trim());
            resolve(cancelled ? undefined : { code, last: lines.length ? lines[lines.length - 1] : '' });
        });
    });
}

async function transcribe(reader, uri) {
    if (!uri) {
        const picked = await vscode.window.showOpenDialog({
            title: 'Transcribe Recording to Lily# Chord Chart', openLabel: 'Transcribe',
            canSelectMany: false, filters: { Recordings: reader.inputs },
        });
        if (!picked) {
            return;
        }
        uri = picked[0];
    }
    const kind = path.extname(uri.fsPath).slice(1).toLowerCase();
    if (uri.scheme !== 'file' || !reader.inputs.includes(kind)) {
        void vscode.window.showErrorMessage(`Dettato reads a recording on this computer (${reader.inputs.join(', ')}).`);
        return;
    }
    const recording = uri.fsPath;
    const name = path.basename(recording, path.extname(recording));
    const chart = path.join(path.dirname(recording), name + '.lys');
    // A chart beside the recording may hold the user's corrections: never written over without asking.
    if (fs.existsSync(chart)) {
        const answer = await vscode.window.showWarningMessage(
            `${name}.lys is already there. Read the recording again and replace it?`, { modal: true }, 'Replace');
        if (answer !== 'Replace') {
            return;
        }
    }
    try {
        log.appendLine(`dettato ${recording}`);
        const done = await vscode.window.withProgress(
            { location: vscode.ProgressLocation.Notification, title: `Dettato: listening to ${path.basename(recording)}…`, cancellable: true },
            (_progress, token) => read(reader.path, recording, chart, token));
        if (done === undefined) {
            return;
        }
        if (done.code !== 0) {
            // (the reader's last line says why, "dettato: x.mp3 was not read: …")
            const why = done.last.replace(/^dettato: /, '') || `exit code ${done.code}`;
            const choice = await vscode.window.showErrorMessage(`Dettato: ${why}`, 'Show Log');
            if (choice === 'Show Log') {
                log.show();
            }
            return;
        }
        await vscode.window.showTextDocument(await vscode.workspace.openTextDocument(chart));
    } catch (error) {
        log.appendLine(String(error));
        void vscode.window.showErrorMessage(`Dettato: ${error.message || error}`);
    }
}

/**
 * @returns {{ path: string, version: string, protocol: number, inputs: string[] } | undefined}
 *   the reader and its `dettato --version --json` (written to bin/dettato.json when the package is built,
 *   so nothing is started here); undefined where it cannot run (not Windows x64, or bin missing)
 */
function activate(context) {
    log = vscode.window.createOutputChannel('Dettato');
    const bin = path.join(context.extensionPath, 'bin');
    const exe = path.join(bin, 'dettato.exe');
    let reader;
    try {
        const info = JSON.parse(fs.readFileSync(path.join(bin, 'dettato.json'), 'utf8'));
        if (process.platform === 'win32' && process.arch === 'x64' && fs.existsSync(exe)) {
            reader = Object.freeze({ path: exe, version: info.version, protocol: info.protocol, inputs: Object.freeze([...info.inputs]) });
        }
    } catch {
        reader = undefined;
    }
    context.subscriptions.push(log, vscode.commands.registerCommand('dettato.transcribe', uri => {
        if (!reader) {
            void vscode.window.showErrorMessage('Dettato runs on Windows x64 only.');
            return undefined;
        }
        return transcribe(reader, uri);
    }));
    return reader;
}

function deactivate() { }

module.exports = { activate, deactivate };
