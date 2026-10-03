import { bindAjaxActions } from '../../components/ajax-actions.js';
import { on, qs, setBusy } from '../../core/dom.js';
import { failureMessage, postForm } from '../../core/http.js';
import { notify } from '../../core/notify.js';
import { refreshRegions } from '../../core/regions.js';
import { startLiveUpdates } from '../monitoring/live.js';

function showResult(success, message) {
    const box = qs('[data-connection-result]');
    if (!box) return;
    box.className = success ? 'alert-success mb-6' : 'alert-error mb-6';
    box.textContent = message;
    box.hidden = false;
}

async function testConnection(button, regions) {
    setBusy(button, true, 'Test ediliyor…');
    const response = await postForm(button.dataset.url);
    setBusy(button, false);

    const data = response.data;
    if (!response.isSuccess || !data) {
        const message = failureMessage(response, 'Bağlantı testi yapılamadı.');
        showResult(false, message);
        notify.error(message);
        return;
    }

    let message = data.message;
    if (data.fingerprintTrustedNow) message += ` Host key kaydedildi: ${data.hostKeyFingerprint}`;
    if (!data.fingerprintMismatch) await refreshRegions(regions);
    showResult(data.isSuccess, message);
    if (data.isSuccess) notify.success(data.message);
    else notify.error(data.message);
}

async function collectMetrics(button, live, regions) {
    setBusy(button, true, 'Toplanıyor…');
    const response = await postForm(button.dataset.url);
    setBusy(button, false);

    const data = response.data;
    if (!response.isSuccess || !data) {
        notify.error(failureMessage(response, 'Metrikler toplanamadı.'));
        return;
    }
    if (data.isSuccess) notify.success(data.message);
    else notify.error(data.message);

    if (live) live.handleUpdate(data);
    else await refreshRegions(regions);
}

/**
 * Sunucu başlığını (_ServerHeader) kullanan sayfaların ortak davranışı: bağlantı testi, elle metrik
 * toplama, silme gibi [data-ajax-action] işlemleri ve canlı durum güncellemesi.
 * regions: bağlantı testinden sonra yeniden alınacak [data-region] adları.
 */
export function initServerPage({ onUpdate, onActionSuccess, regions = ['server-header'] } = {}) {
    const live = startLiveUpdates({ onUpdate });
    bindAjaxActions(document, { onSuccess: onActionSuccess });
    on(document, 'click', '[data-connection-test]', (event, button) => testConnection(button, regions));
    on(document, 'click', '[data-collect-metrics]', (event, button) => collectMetrics(button, live, regions));
    return { live };
}
