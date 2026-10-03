const THEME = {
    background: '#0b1020',
    foreground: '#e2e8f0',
    cursor: '#0b1020',
    selectionBackground: '#4f46e580'
};

/** Uzun süren işlemlerin çıktısı için salt okunur xterm ekranı (.log-console); kullanıcı girdisi sunucuya gitmez. */
export function createLogConsole(screen) {
    if (!screen || !window.Terminal) return null;

    const term = new window.Terminal({
        disableStdin: true,
        cursorBlink: false,
        cursorStyle: 'bar',
        fontFamily: '"JetBrains Mono", ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace',
        fontSize: 12.5,
        scrollback: 20000,
        theme: THEME
    });
    const fit = window.FitAddon ? new window.FitAddon.FitAddon() : null;
    if (fit) term.loadAddon(fit);
    term.open(screen);

    const fitSafely = () => {
        try {
            fit?.fit();
        } catch {
            // Gizli paneldeyken boyut hesaplanamaz; görünür olunca tekrar denenir.
        }
    };

    const observer = new ResizeObserver(() => {
        if (screen.offsetParent !== null) fitSafely();
    });
    observer.observe(screen);
    fitSafely();

    return {
        write: text => term.write(text),
        reset: () => term.reset(),
        notice: (text, color = '33') => term.write(`\x1b[${color}m${text}\x1b[0m\r\n`),
        fit: fitSafely
    };
}
