import { qs } from '../../core/dom.js';

const INPUT_CHUNK = 4096;
const STATUS_CLASSES = { connected: 'badge-success', connecting: 'badge-info', error: 'badge-danger', idle: 'badge-neutral' };
const TERMINAL_THEME = { background: '#0b1020', foreground: '#e2e8f0', cursor: '#a5b4fc', selectionBackground: '#4f46e580' };

/**
 * Container terminali: xterm.js ekranı ile ContainerTerminalHub arasında köprü.
 * Oturum sunucu tarafında kapanırsa (closed) bekleyen Start sonucu yok sayılır.
 */
export function createTerminal(root) {
    if (!root || !window.Terminal || !window.signalR) return null;

    const screen = qs('[data-terminal-screen]', root);
    const statusBadge = qs('[data-terminal-status]', root);
    const statusText = qs('[data-terminal-status-text]', root);
    const connectButton = qs('[data-terminal-connect]', root);
    const disconnectButton = qs('[data-terminal-disconnect]', root);

    let term = null;
    let fit = null;
    let connection = null;
    let active = false;
    let opened = false;

    function setStatus(kind, text) {
        statusBadge.className = STATUS_CLASSES[kind] ?? 'badge-neutral';
        statusText.textContent = text;
        connectButton.hidden = kind === 'connected' || kind === 'connecting';
        disconnectButton.hidden = kind !== 'connected';
    }

    const fitSafely = () => {
        try {
            fit?.fit();
        } catch {
            // Gizli sekmede boyut hesaplanamaz; sekme açıldığında tekrar denenir.
        }
    };

    function ensureTerminal() {
        if (term) return;
        term = new window.Terminal({
            cursorBlink: true,
            fontFamily: '"JetBrains Mono", ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace',
            fontSize: 13,
            scrollback: 5000,
            theme: TERMINAL_THEME
        });
        fit = new window.FitAddon.FitAddon();
        term.loadAddon(fit);
        term.open(screen);
        fitSafely();

        term.onData(data => {
            if (!active || !connection) return;
            for (let i = 0; i < data.length; i += INPUT_CHUNK) {
                connection.send('Input', data.slice(i, i + INPUT_CHUNK)).catch(() => { });
            }
        });
        term.onResize(size => {
            if (active && connection) connection.send('Resize', size.cols, size.rows).catch(() => { });
        });
        new ResizeObserver(() => {
            if (!root.closest('[hidden]')) fitSafely();
        }).observe(screen);
    }

    const writeNotice = (text, color = '33') => term?.write(`\r\n\x1b[${color}m${text}\x1b[0m\r\n`);

    function reset(current) {
        connection = null;
        current?.stop().catch(() => { });
    }

    async function connect() {
        ensureTerminal();
        if (connection) return;
        setStatus('connecting', 'Bağlanıyor…');

        const current = new window.signalR.HubConnectionBuilder()
            .withUrl(root.dataset.hubUrl)
            .configureLogging(window.signalR.LogLevel.Warning)
            .build();
        connection = current;

        let sessionClosed = false;
        current.on('output', data => term?.write(data));
        current.on('closed', reason => {
            sessionClosed = true;
            active = false;
            writeNotice(`[Oturum kapandı${reason ? `: ${reason}` : ''}]`);
            setStatus('idle', 'Bağlı değil');
            if (connection === current) reset(current);
        });
        current.onclose(() => {
            if (active) {
                active = false;
                writeNotice('[Bağlantı koptu]', '31');
                setStatus('error', 'Bağlantı koptu');
            }
            if (connection === current) connection = null;
        });

        try {
            await current.start();
            fitSafely();
            const result = await current.invoke('Start', root.dataset.serverId, root.dataset.container, term.cols, term.rows);
            if (sessionClosed) return;
            if (!result?.success) {
                writeNotice(result?.message || 'Terminal açılamadı.', '31');
                setStatus('error', 'Açılamadı');
                reset(current);
                return;
            }
            active = true;
            setStatus('connected', 'Bağlı');
            term.focus();
        } catch (error) {
            if (sessionClosed) return;
            writeNotice(`Terminal bağlantısı kurulamadı${error?.message ? `: ${error.message}` : ''}.`, '31');
            setStatus('error', 'Bağlantı hatası');
            reset(current);
        }
    }

    async function disconnect() {
        const current = connection;
        if (!current) return;
        try {
            await current.invoke('Stop');
        } catch {
            // Bağlantı zaten kapanmış olabilir.
        }
        active = false;
        reset(current);
        setStatus('idle', 'Bağlı değil');
    }

    connectButton.addEventListener('click', connect);
    disconnectButton.addEventListener('click', disconnect);
    window.addEventListener('beforeunload', () => connection?.stop());

    return {
        activate() {
            ensureTerminal();
            setTimeout(() => {
                fitSafely();
                term.focus();
            }, 0);
            if (!opened) {
                opened = true;
                connect();
            }
        }
    };
}
