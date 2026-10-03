import { copyFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const modules = path.join(root, 'node_modules');
const lib = path.join(root, 'wwwroot', 'lib');

const files = {
    'chart.js': ['chart.js/dist/chart.umd.min.js', 'chart.js/LICENSE.md'],
    signalr: ['@microsoft/signalr/dist/browser/signalr.min.js'],
    xterm: [
        '@xterm/xterm/lib/xterm.js',
        '@xterm/xterm/css/xterm.css',
        '@xterm/xterm/LICENSE',
        '@xterm/addon-fit/lib/addon-fit.js'
    ],
    sweetalert2: ['sweetalert2/dist/sweetalert2.min.js', 'sweetalert2/LICENSE'],
    toastr: ['toastr/build/toastr.min.js'],
    jquery: ['jquery/dist/jquery.min.js', 'jquery/LICENSE.txt']
};

for (const [folder, sources] of Object.entries(files)) {
    const target = path.join(lib, folder);
    await mkdir(target, { recursive: true });
    await Promise.all(sources.map(source => copyFile(path.join(modules, source), path.join(target, path.basename(source)))));
}

console.log(`Vendor dosyaları kopyalandı: ${Object.keys(files).join(', ')}`);
