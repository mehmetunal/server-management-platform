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
        '@xterm/addon-fit/lib/addon-fit.js',
        '@xterm/addon-search/lib/addon-search.js'
    ],
    codemirror: [
        'codemirror/lib/codemirror.js',
        'codemirror/lib/codemirror.css',
        'codemirror/LICENSE',
        'codemirror/theme/material-darker.css',
        'codemirror/addon/mode/simple.js',
        'codemirror/addon/mode/overlay.js',
        'codemirror/addon/dialog/dialog.js',
        'codemirror/addon/dialog/dialog.css',
        'codemirror/addon/search/search.js',
        'codemirror/addon/search/searchcursor.js',
        'codemirror/addon/search/jump-to-line.js',
        'codemirror/addon/edit/matchbrackets.js',
        'codemirror/addon/edit/closebrackets.js',
        'codemirror/addon/selection/active-line.js',
        'codemirror/mode/meta.js',
        ...['javascript', 'css', 'xml', 'htmlmixed', 'yaml', 'clike', 'properties', 'markdown', 'nginx',
            'dockerfile', 'shell', 'sql', 'python', 'toml', 'diff', 'go', 'php']
            .map(mode => `codemirror/mode/${mode}/${mode}.js`)
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
