const THEME = {
    background: '#0b1020',
    foreground: '#e2e8f0',
    cursor: '#a5b4fc',
    selectionBackground: '#4f46e580'
};
const SEARCH_DECORATIONS = {
    matchBackground: '#854d0e',
    matchOverviewRuler: '#ca8a04',
    activeMatchBackground: '#ca8a04',
    activeMatchColorOverviewRuler: '#facc15'
};

function copyWithFallback(text) {
    if (navigator.clipboard?.writeText) return navigator.clipboard.writeText(text);
    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.opacity = '0';
    document.body.appendChild(area);
    area.select();
    const copied = document.execCommand('copy');
    area.remove();
    return copied ? Promise.resolve() : Promise.reject(new Error('Kopyalanamadı.'));
}

/**
 * xterm.js ekranı: boyutlandırma, arama, pano ve çıktı dışa aktarma.
 * Ctrl+Shift+C seçimi kopyalar, Ctrl+Shift+V yapıştırır; normal Ctrl+V tarayıcı yapıştırmasıyla çalışır.
 */
export function createTerminalView(screen, { onData, onResize } = {}) {
    const term = new window.Terminal({
        allowProposedApi: true,
        cursorBlink: true,
        fontFamily: '"JetBrains Mono", ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace',
        fontSize: 13,
        scrollback: 10000,
        theme: THEME
    });
    const fit = new window.FitAddon.FitAddon();
    const search = window.SearchAddon ? new window.SearchAddon.SearchAddon() : null;
    term.loadAddon(fit);
    if (search) term.loadAddon(search);
    term.open(screen);

    const fitSafely = () => {
        try {
            fit.fit();
        } catch {
            // Gizli sekmede boyut hesaplanamaz; görünür olunca tekrar denenir.
        }
    };

    async function copy() {
        const text = term.getSelection();
        if (!text) return false;
        await copyWithFallback(text);
        return true;
    }

    async function paste() {
        if (!navigator.clipboard?.readText) throw new Error('Tarayıcı panodan okumaya izin vermiyor. Ctrl+V kullanın.');
        const text = await navigator.clipboard.readText();
        if (text) term.paste(text);
    }

    term.attachCustomKeyEventHandler(event => {
        if (event.type !== 'keydown' || !event.ctrlKey || !event.shiftKey) return true;
        if (event.code === 'KeyC') {
            copy().catch(() => { });
            return false;
        }
        if (event.code === 'KeyV') {
            paste().catch(() => { });
            return false;
        }
        return true;
    });

    term.onData(data => onData?.(data));
    term.onResize(size => onResize?.(size.cols, size.rows));

    const observer = new ResizeObserver(() => {
        if (screen.offsetParent !== null) fitSafely();
    });
    observer.observe(screen);
    fitSafely();

    return {
        get cols() {
            return term.cols;
        },
        get rows() {
            return term.rows;
        },
        write: data => term.write(data),
        writeNotice: (text, color = '33') => term.write(`\r\n\x1b[${color}m${text}\x1b[0m\r\n`),
        reset: () => term.reset(),
        clear: () => term.clear(),
        focus: () => term.focus(),
        fit: fitSafely,
        copy,
        paste,
        findNext: query => search?.findNext(query, { decorations: SEARCH_DECORATIONS }) ?? false,
        findPrevious: query => search?.findPrevious(query, { decorations: SEARCH_DECORATIONS }) ?? false,
        clearSearch: () => search?.clearDecorations(),
        /** Ekran ve geri kaydırma tamponundaki tüm metin. */
        exportText() {
            const buffer = term.buffer.active;
            const lines = [];
            for (let i = 0; i < buffer.length; i++) lines.push(buffer.getLine(i)?.translateToString(true) ?? '');
            return lines.join('\n').replace(/\s+$/, '') + '\n';
        },
        dispose() {
            observer.disconnect();
            term.dispose();
        }
    };
}
