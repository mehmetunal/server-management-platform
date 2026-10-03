/**
 * Sekme görünürken fn'i belirli aralıklarla çağırır; sekme gizliyken durur, geri gelince hemen yeniler.
 * Önceki çağrı bitmeden yenisi başlatılmaz.
 */
export function startAutoRefresh(fn, intervalMs) {
    let timer = null;
    let running = false;

    const tick = async () => {
        if (document.hidden || running) return;
        running = true;
        try {
            await fn();
        } finally {
            running = false;
        }
    };
    const stop = () => {
        clearInterval(timer);
        timer = null;
    };
    const start = () => {
        stop();
        timer = setInterval(tick, intervalMs);
    };

    document.addEventListener('visibilitychange', () => {
        if (document.hidden) {
            stop();
            return;
        }
        tick();
        start();
    });

    start();
    return { stop };
}
