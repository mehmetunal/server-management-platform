const RECONNECT_DELAYS = [0, 2000, 5000, 10000, 20000, 30000];

/**
 * ServiceHub üzerinden tek bir servis işlemini izler. Katılım yanıtı o ana kadarki çıktıyı ve sıra numarasını verir;
 * yanıt gelmeden ulaşan canlı parçalar bekletilir, numarası yanıttakinden küçük olanlar atılır.
 * Bağlantı yeniden kurulunca işleme tekrar katılınır ve ekran baştan çizilir.
 */
export function createServiceHub(url, { onSnapshot, onOutput, onStage, onCompleted, onConnection }) {
    const signalR = window.signalR;
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(url)
        .withAutomaticReconnect(RECONNECT_DELAYS)
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    let target = null;
    let lastSequence = null;
    let pending = [];

    connection.on('serviceOutput', (operationId, sequence, text) => {
        if (operationId !== target) return;
        if (lastSequence === null) {
            pending.push([sequence, text]);
            return;
        }
        if (sequence <= lastSequence) return;
        lastSequence = sequence;
        onOutput(text);
    });

    connection.on('serviceStage', (operationId, stage, message) => {
        if (operationId === target) onStage(stage, message);
    });

    connection.on('serviceCompleted', (operationId, status, message) => {
        if (operationId === target) onCompleted(status, message);
    });

    async function join() {
        lastSequence = null;
        pending = [];
        const response = await connection.invoke('JoinOperation', target);
        if (!response.isSuccess) {
            onSnapshot(response);
            return;
        }
        lastSequence = response.sequence;
        onSnapshot(response);
        for (const [sequence, text] of pending) {
            if (sequence > lastSequence) {
                lastSequence = sequence;
                onOutput(text);
            }
        }
        pending = [];
    }

    connection.onreconnecting(() => onConnection('reconnecting'));
    connection.onreconnected(() => {
        onConnection('connected');
        join().catch(() => onConnection('error'));
    });
    connection.onclose(() => onConnection('disconnected'));

    return {
        async watch(operationId) {
            target = operationId;
            if (connection.state === signalR.HubConnectionState.Disconnected) await connection.start();
            onConnection('connected');
            await join();
        },
        stop: () => connection.stop()
    };
}
