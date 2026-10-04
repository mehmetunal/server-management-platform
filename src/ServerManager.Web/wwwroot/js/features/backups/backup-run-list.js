import { createAjaxList } from '../../components/ajax-list.js';
import { qs } from '../../core/dom.js';

const RUNNING_REFRESH_MS = 3000;

function hasRunning(root) {
    return qs('[data-backup-rows][data-running="true"]', root) !== null;
}

/**
 * Yedek çalışmaları listesi; süren yedek veya geri yükleme varken liste kısa aralıklarla yenilenir.
 * İş sayfasına gömülü listede history kapalıdır ve adres data-url'den okunur.
 */
export function initBackupRunList(root, { form, embedded = false } = {}) {
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

    document.addEventListener('visibilitychange', schedule);
    schedule();
    return list;
}
