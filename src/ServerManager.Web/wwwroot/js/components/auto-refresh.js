import { onPageDispose } from '../core/page-scope.js';

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

    const onVisibility = () => {
        if (document.hidden) {
            stop();
            return;
        }
        tick();
        start();
    };
    document.addEventListener('visibilitychange', onVisibility);

    const dispose = () => {
        stop();
        document.removeEventListener('visibilitychange', onVisibility);
    };
    onPageDispose(dispose);

    start();
    return { stop: dispose };
}
