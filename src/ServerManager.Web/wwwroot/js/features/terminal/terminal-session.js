import { confirmAction } from '../../core/dialog.js';
import { notify } from '../../core/notify.js';

const INPUT_CHUNK = 4096;
const NOTICE_COLORS = { info: '36', warning: '33', error: '31' };

/**
 * Tek SSH terminal oturumu: xterm görünümü ile hub'daki oturum arasındaki bağ.
 * Oturum kimliği biliniyorsa (sayfa yenilendi, bağlantı koptu) önce aynı oturuma yeniden bağlanılır.
 *
 * Durumlar: idle, connecting, connected, reconnecting, disconnected, detached, error, closed.
 */
export function createTerminalSession({ hub, view, start, sessionId = null, onStatus, onSession, onClosed, onCommand, onTitle }) {
    let id = sessionId;
    let state = 'idle';
    let confirming = false;

    function setState(next, text) {
        state = next;
        onStatus?.(next, text);
    }

    function setId(next) {
        id = next;
        onSession?.(next);
    }

    async function askConfirmation({ token, command, description }) {
        if (confirming) return;
        confirming = true;
        try {
            const approved = await confirmAction({
                title: 'Tehlikeli komut',
                message: `${description}. Bu komut ikinci onay gerektiriyor; çalıştırmak istediğinize emin misiniz?`,
                code: command,
                confirmText: 'Çalıştır'
            });
            if (id) hub.send('Confirm', id, token, approved !== null);
        } finally {
            confirming = false;
            view.focus();
        }
    }

    function finish(reason) {
        if (state === 'closed') return;
        if (id) hub.unregister(id);
        view.writeNotice(`[Oturum kapandı${reason ? `: ${reason}` : ''}]`);
        setState('closed', 'Oturum kapandı');
        setId(null);
        onClosed?.(reason);
    }

    const handlers = {
        output: data => view.write(data),
        closed: reason => finish(reason),
        detached: () => {
            setState('detached', 'Başka pencerede açık');
            view.writeNotice('[Bu oturum başka bir pencerede açıldı. Buraya almak için "Buraya al" düğmesini kullanın.]');
        },
        confirm: payload => askConfirmation(payload),
        notice: ({ level, message }) => {
            view.writeNotice(message, NOTICE_COLORS[level] ?? '36');
            if (level === 'warning' || level === 'error') notify[level](message);
        },
        command: text => onCommand?.(text)
    };

    async function attach() {
        setState('connecting', 'Yeniden bağlanıyor…');
        const pending = [];
        hub.register(id, Object.fromEntries(Object.keys(handlers).map(event => [event, payload => pending.push([event, payload])])));

        let result;
        try {
            result = await hub.invoke('Attach', id, view.cols, view.rows);
        } catch (error) {
            hub.unregister(id);
            setState('disconnected', 'Bağlantı koptu');
            view.writeNotice(`Yeniden bağlanılamadı${error?.message ? `: ${error.message}` : ''}.`, '31');
            return null;
        }

        if (!result?.success) {
            hub.unregister(id);
            setId(null);
            return false;
        }

        view.reset();
        if (result.output) view.write(result.output);
        hub.register(id, handlers);
        pending.forEach(([event, payload]) => handlers[event](payload));
        if (result.title) onTitle?.(result.title);
        if (state === 'connecting') setState('connected', 'Bağlı');
        if (result.pendingConfirmation) askConfirmation(result.pendingConfirmation);
        view.focus();
        return true;
    }

    async function startNew() {
        setState('connecting', 'Bağlanıyor…');
        let result;
        try {
            result = await start(view.cols, view.rows);
        } catch (error) {
            view.writeNotice(`Terminal açılamadı${error?.message ? `: ${error.message}` : ''}.`, '31');
            setState('error', 'Açılamadı');
            return false;
        }

        if (!result?.success) {
            view.writeNotice(result?.message || 'Terminal açılamadı.', '31');
            setState('error', 'Açılamadı');
            return false;
        }

        setId(result.sessionId);
        setState('connected', 'Bağlı');
        hub.register(result.sessionId, handlers);
        view.focus();
        return state === 'connected';
    }

    const offState = hub.onState(hubState => {
        if (!id || state === 'closed') return;
        if (hubState === 'reconnecting') setState('reconnecting', 'Bağlantı koptu, yeniden bağlanıyor…');
        if (hubState === 'disconnected') setState('disconnected', 'Bağlantı koptu');
    });
    const offReconnected = hub.onReconnected(() => {
        if (id && state !== 'closed' && state !== 'detached') attach();
    });

    return {
        get id() {
            return id;
        },
        get state() {
            return state;
        },
        /** restoreOnly: true iken önceki oturum bulunamazsa yeni oturum açılmaz. */
        async open({ restoreOnly = false } = {}) {
            if (id) {
                const attached = await attach();
                if (attached !== false || restoreOnly) return attached === true;
                view.writeNotice('[Önceki oturum sona ermiş; yeni oturum açılıyor]');
            }
            if (restoreOnly) return false;
            return startNew();
        },
        async reconnect() {
            if (id) {
                const attached = await attach();
                if (attached !== false) return attached === true;
            }
            return startNew();
        },
        input(data) {
            if (state !== 'connected' || confirming || !id) return;
            for (let i = 0; i < data.length; i += INPUT_CHUNK) hub.send('Input', id, data.slice(i, i + INPUT_CHUNK));
        },
        resize(columns, rows) {
            if (state === 'connected' && id) hub.send('Resize', id, columns, rows);
        },
        async stop() {
            if (!id) return;
            if (!hub.isConnected) {
                finish('Oturum kapatıldı.');
                return;
            }
            try {
                await hub.invoke('Stop', id);
            } catch {
                finish('Oturum kapatıldı.');
            }
        },
        dispose() {
            offState();
            offReconnected();
            if (id) hub.unregister(id);
        }
    };
}
