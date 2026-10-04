import { on, setBusy } from '../../core/dom.js';
import { failureMessage, postForm } from '../../core/http.js';
import { notify } from '../../core/notify.js';
import { refreshRegions } from '../../core/regions.js';

/**
 * [data-security-scan] butonları: taramayı başlatır, bitince (başarısız da olsa kayıt oluştuğu için)
 * verilen [data-region] bölgelerini yeniler.
 */
export function bindSecurityScan(regions) {
    on(document, 'click', '[data-security-scan]', async (event, button) => {
        if (button.disabled) return;
        setBusy(button, true, 'Taranıyor…');
        try {
            const response = await postForm(button.dataset.url);
            if (response.isSuccess) notify.success(response.message);
            else notify.error(failureMessage(response, 'Güvenlik taraması yapılamadı.'));
            await refreshRegions(regions);
        } finally {
            setBusy(button, false);
        }
    });
}

export function bindCopyButtons() {
    on(document, 'click', '[data-copy-text]', async (event, button) => {
        try {
            await navigator.clipboard.writeText(button.dataset.copyText);
            notify.success('Komut panoya kopyalandı. Sunucuda çalıştırmadan önce gözden geçirin.');
        } catch {
            notify.error('Kopyalanamadı; komutu elle seçip kopyalayın.');
        }
    });
}
