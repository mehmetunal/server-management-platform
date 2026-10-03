const RECONNECT_DELAYS = [0, 2000, 5000, 10000, 20000, 30000];

/**
 * DokployHub üzerinden tek bir kurulumu izler. Katılım yanıtı o ana kadarki çıktıyı ve sıra numarasını verir;
 * yanıt gelmeden ulaşan canlı parçalar bekletilir, numarası yanıttakinden küçük olanlar atılır.
 * Bağlantı yeniden kurulunca kuruluma tekrar katılınır ve ekran baştan çizilir.
 */
export function createInstallHub(url, { onSnapshot, onOutput, onStage, onCompleted, onConnection }) {
    const signalR = window.signalR;
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(url)
        .withAutomaticReconnect(RECONNECT_DELAYS)
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    let target = null;
    let lastSequence = null;
    let pending = [];

    connection.on('installOutput', (installationId, sequence, text) => {
        if (installationId !== target?.installationId) return;
        if (lastSequence === null) {
            pending.push([sequence, text]);
            return;
        }
        if (sequence <= lastSequence) return;
        lastSequence = sequence;
        onOutput(text);
    });

    connection.on('installStage', (installationId, stage, message) => {
        if (installationId === target?.installationId) onStage(stage, message);
    });

    connection.on('installCompleted', (installationId, succeeded, message) => {
        if (installationId === target?.installationId) onCompleted(succeeded, message);
    });

    async function join() {
        lastSequence = null;
        pending = [];
        const response = await connection.invoke('JoinInstallation', target.serverId, target.installationId);
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
        async watch(serverId, installationId) {
            target = { serverId, installationId };
            if (connection.state === signalR.HubConnectionState.Disconnected) await connection.start();
            onConnection('connected');
            await join();
        },
        stop: () => connection.stop()
    };
}
