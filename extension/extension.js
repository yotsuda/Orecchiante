// Orecchiante: carries the reader (bin/orecchiante.exe on Windows, bin/orecchiante on macOS, the models beside it) for
// the Lily# extension, which has the menu that runs it ("Convert Recording to Lily# (.lys)…"). Nothing here starts on
// its own: Lily# activates it (vscode.extensions.getExtension('yotsuda.orecchiante').activate()) and reads what
// activate returns, the reader's path, version and protocol, as it does Calamo's. Until 0.3.0 this extension had a
// command of its own (orecchiante.transcribe); it went when Lily# had the menu (docs/ROADMAP.md, D4).
'use strict';

const fs = require('fs');
const path = require('path');

// the systems a package is built for (tools/pack-extension.ps1 -Rid), and the reader's name on each
const READERS = { 'win32-x64': 'orecchiante.exe', 'darwin-arm64': 'orecchiante', 'darwin-x64': 'orecchiante' };

/**
 * @returns {{ path: string, version: string, protocol: number, inputs: string[] } | undefined}
 *   the reader and its `orecchiante --version --json` (written to bin/orecchiante.json when the package is built,
 *   so nothing is started here); undefined where it cannot run (another system, or bin missing)
 */
function activate(context) {
    const name = READERS[`${process.platform}-${process.arch}`];
    if (!name) return undefined;
    const bin = path.join(context.extensionPath, 'bin');
    const exe = path.join(bin, name);
    try {
        const info = JSON.parse(fs.readFileSync(path.join(bin, 'orecchiante.json'), 'utf8'));
        if (!fs.existsSync(exe)) return undefined;
        if (process.platform !== 'win32') {
            // (a package's files may come out of the .vsix without the right to run: given it once)
            try { fs.accessSync(exe, fs.constants.X_OK); } catch { fs.chmodSync(exe, 0o755); }
        }
        return Object.freeze({ path: exe, version: info.version, protocol: info.protocol, inputs: Object.freeze([...info.inputs]) });
    } catch {
        // (no bin/orecchiante.json: a package built without the reader)
    }
    return undefined;
}

function deactivate() { }

module.exports = { activate, deactivate };
