const RECONNECT_DELAYS = [0, 2000, 5000, 10000, 20000, 30000];
const EVENTS = ['output', 'closed', 'detached', 'confirm', 'notice', 'command'];
const MAX_QUEUED_EVENTS = 500;

/**
 * TerminalHub bağlantısı. Tek bağlantı üzerinden birden çok oturum taşınır; olaylar oturum kimliğine göre dağıtılır.
 * Oturum kaydedilmeden önce gelen olaylar (Start yanıtından önce basılan karşılama metni gibi) kuyrukta bekletilir.
 */
export function createTerminalHub(url) {
    const signalR = window.signalR;
    const handlers = new Map();
    const queued = new Map();
    const stateListeners = new Set();
    const reconnectListeners = new Set();
    let starting = null;
    let hasStarted = false;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(url)
        .withAutomaticReconnect(RECONNECT_DELAYS)
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    const emitState = state => stateListeners.forEach(listener => listener(state));
    const emitReconnected = () => reconnectListeners.forEach(listener => listener());

    function dispatch(sessionId, event, payload) {
        const handler = handlers.get(sessionId);
        if (handler) {
            handler[event]?.(payload);
            return;
        }
        const queue = queued.get(sessionId) ?? [];
        if (queue.length < MAX_QUEUED_EVENTS) queue.push([event, payload]);
        queued.set(sessionId, queue);
    }

    for (const event of EVENTS) {
        connection.on(event, (sessionId, payload) => dispatch(sessionId, event, payload));
    }

    connection.onreconnecting(() => emitState('reconnecting'));
    connection.onreconnected(() => {
        emitState('connected');
        emitReconnected();
    });
    connection.onclose(() => emitState('disconnected'));

    async function ensureStarted() {
        const { Connected, Disconnected } = signalR.HubConnectionState;
        if (connection.state === Connected) return;
        if (connection.state === Disconnected && !starting) {
            const wasStarted = hasStarted;
            starting = connection.start()
                .then(() => {
                    hasStarted = true;
                    emitState('connected');
                    if (wasStarted) emitReconnected();
                })
                .finally(() => {
                    starting = null;
                });
        }
        if (starting) return starting;
        throw new Error('Bağlantı yeniden kuruluyor, lütfen bekleyin.');
    }

    return {
        ensureStarted,
        get isConnected() {
            return connection.state === signalR.HubConnectionState.Connected;
        },
        async invoke(method, ...args) {
            await ensureStarted();
            return connection.invoke(method, ...args);
        },
        send(method, ...args) {
            if (connection.state !== signalR.HubConnectionState.Connected) return;
            connection.send(method, ...args).catch(() => { });
        },
        register(sessionId, handler) {
            handlers.set(sessionId, handler);
            const queue = queued.get(sessionId);
            queued.delete(sessionId);
            queue?.forEach(([event, payload]) => handler[event]?.(payload));
        },
        unregister(sessionId) {
            handlers.delete(sessionId);
            queued.delete(sessionId);
        },
        onState(listener) {
            stateListeners.add(listener);
            return () => stateListeners.delete(listener);
        },
        onReconnected(listener) {
            reconnectListeners.add(listener);
            return () => reconnectListeners.delete(listener);
        }
    };
}
