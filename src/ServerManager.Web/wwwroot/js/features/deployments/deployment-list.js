import { createAjaxList } from '../../components/ajax-list.js';
import { on, qs, setBusy } from '../../core/dom.js';
import { onPageDispose, pageSignal } from '../../core/page-scope.js';

const RUNNING_REFRESH_MS = 5000;

function hasRunning(root) {
    return qs('[data-deployment-rows][data-running="true"]', root) !== null;
}

/**
 * Deployment listesi; çalışan deployment varken liste belirli aralıklarla yenilenir.
 * Proje/sunucu sayfasına gömülü listelerde history kapalıdır ve adres data-url'den okunur.
 */
export function initDeploymentList(root, { form, embedded = false } = {}) {
    if (!root) return null;

    let timer = null;
    const schedule = () => {
        clearTimeout(timer);
        if (hasRunning(root) && !document.hidden) timer = setTimeout(() => list.reload(), RUNNING_REFRESH_MS);
    };

    const list = createAjaxList(root, {
        form,
        history: !embedded,
        url: embedded ? root.dataset.url : undefined,
        onLoaded: schedule
    });

    const signal = pageSignal();
    document.addEventListener('visibilitychange', schedule, signal ? { signal } : undefined);
    onPageDispose(() => clearTimeout(timer));
    on(document, 'click', '[data-deployments-refresh]', async (event, button) => {
        setBusy(button, true);
        try {
            await list.reload();
        } finally {
            setBusy(button, false);
        }
    });

    schedule();
    return list;
}
