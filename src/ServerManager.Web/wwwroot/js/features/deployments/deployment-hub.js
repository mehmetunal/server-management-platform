const RECONNECT_DELAYS = [0, 2000, 5000, 10000, 20000, 30000];

/**
 * DeploymentHub üzerinden tek bir deployment'ı izler. Katılım yanıtı o ana kadarki çıktıyı ve sıra numarasını verir;
 * yanıt gelmeden ulaşan canlı parçalar bekletilir, numarası yanıttakinden küçük olanlar atılır.
 * Bağlantı yeniden kurulunca deployment'a tekrar katılınır ve ekran baştan çizilir.
 */
export function createDeploymentHub(url, { onSnapshot, onOutput, onStage, onCommit, onCompleted, onConnection }) {
    const signalR = window.signalR;
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(url)
        .withAutomaticReconnect(RECONNECT_DELAYS)
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    let target = null;
    let lastSequence = null;
    let pending = [];

    connection.on('deploymentOutput', (deploymentId, sequence, text) => {
        if (deploymentId !== target) return;
        if (lastSequence === null) {
            pending.push([sequence, text]);
            return;
        }
        if (sequence <= lastSequence) return;
        lastSequence = sequence;
        onOutput(text);
    });

    connection.on('deploymentStage', (deploymentId, stage, message) => {
        if (deploymentId === target) onStage(stage, message);
    });

    connection.on('deploymentCommit', (deploymentId, sha) => {
        if (deploymentId === target) onCommit?.(sha);
    });

    connection.on('deploymentCompleted', (deploymentId, status, message) => {
        if (deploymentId === target) onCompleted(status, message);
    });

    async function join() {
        lastSequence = null;
        pending = [];
        const response = await connection.invoke('JoinDeployment', target);
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
        async watch(deploymentId) {
            target = deploymentId;
            if (connection.state === signalR.HubConnectionState.Disconnected) await connection.start();
            onConnection('connected');
            await join();
        },
        stop: () => connection.stop()
    };
}
