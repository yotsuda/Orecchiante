// Dettato: carries the reader (bin/dettato.exe, with its two models beside it) and one command that runs it
// on a recording and opens the chart. Starts only when the command is run or another extension asks for it:
// what activate returns (the reader's path and version) is for the Lily# extension, as Calamo's is
// (vscode.extensions.getExtension('yotsuda.dettato')).
'use strict';

const cp = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');
const vscode = require('vscode');

/** The reader takes .wav only (docs/ROADMAP.md, D3: other formats are to come). */
const RECORDINGS = { 'Recordings (WAV)': ['wav'] };

let log;

/** Runs the reader on one recording; resolves to its exit code (undefined when cancelled). */
function read(exe, recording, folder, token) {
    return new Promise((resolve, reject) => {
        const child = cp.spawn(exe, ['read', recording, '--out', folder], { windowsHide: true });
        let cancelled = false;
        const stop = token.onCancellationRequested(() => { cancelled = true; child.kill(); });
        child.stdout.on('data', text => log.append(text.toString()));
        child.stderr.on('data', text => log.append(text.toString()));
        child.on('error', error => { stop.dispose(); reject(error); });
        child.on('close', code => { stop.dispose(); resolve(cancelled ? undefined : code); });
    });
}

async function transcribe(exe, uri) {
    if (!uri) {
        const picked = await vscode.window.showOpenDialog({
            title: 'Transcribe Recording to Lily# Chord Chart', openLabel: 'Transcribe',
            canSelectMany: false, filters: RECORDINGS,
        });
        if (!picked) {
            return;
        }
        uri = picked[0];
    }
    if (uri.scheme !== 'file' || path.extname(uri.fsPath).toLowerCase() !== '.wav') {
        void vscode.window.showErrorMessage('Dettato reads a .wav file on this computer (other formats are to come).');
        return;
    }
    const recording = uri.fsPath;
    const name = path.basename(recording, path.extname(recording));
    const chart = path.join(path.dirname(recording), name + '.lys');
    const sidecar = path.join(path.dirname(recording), name + '.dettato.json');
    // A chart beside the recording may hold the user's corrections: never written over without asking.
    if (fs.existsSync(chart)) {
        const answer = await vscode.window.showWarningMessage(
            `${name}.lys is already there. Read the recording again and replace it?`, { modal: true }, 'Replace');
        if (answer !== 'Replace') {
            return;
        }
    }
    // Read into a folder of its own, so that a failed or cancelled reading leaves nothing half-written.
    const folder = fs.mkdtempSync(path.join(os.tmpdir(), 'dettato-'));
    try {
        log.appendLine(`dettato read ${recording}`);
        const code = await vscode.window.withProgress(
            { location: vscode.ProgressLocation.Notification, title: `Dettato: listening to ${path.basename(recording)}…`, cancellable: true },
            (_progress, token) => read(exe, recording, folder, token));
        if (code === undefined) {
            return;
        }
        const written = path.join(folder, name + '.lys');
        if (code !== 0 || !fs.existsSync(written)) {
            const choice = await vscode.window.showErrorMessage(
                `Dettato could not read ${path.basename(recording)} (exit code ${code}).`, 'Show Log');
            if (choice === 'Show Log') {
                log.show();
            }
            return;
        }
        fs.copyFileSync(written, chart);
        fs.copyFileSync(path.join(folder, name + '.dettato.json'), sidecar);
        await vscode.window.showTextDocument(await vscode.workspace.openTextDocument(chart));
    } catch (error) {
        log.appendLine(String(error));
        void vscode.window.showErrorMessage(`Dettato: ${error.message || error}`);
    } finally {
        fs.rmSync(folder, { recursive: true, force: true });
    }
}

/**
 * @returns {{ path: string, version: string } | undefined}
 *   the reader; undefined where it cannot run (not Windows x64, or bin missing)
 */
function activate(context) {
    log = vscode.window.createOutputChannel('Dettato');
    const exe = path.join(context.extensionPath, 'bin', 'dettato.exe');
    const able = process.platform === 'win32' && process.arch === 'x64' && fs.existsSync(exe);
    context.subscriptions.push(log, vscode.commands.registerCommand('dettato.transcribe', uri => {
        if (!able) {
            void vscode.window.showErrorMessage('Dettato runs on Windows x64 only.');
            return undefined;
        }
        return transcribe(exe, uri);
    }));
    return able ? Object.freeze({ path: exe, version: context.extension.packageJSON.version }) : undefined;
}

function deactivate() { }

module.exports = { activate, deactivate };
