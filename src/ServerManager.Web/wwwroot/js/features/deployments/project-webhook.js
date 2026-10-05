import { confirmAction } from '../../core/dialog.js';
import { on, qs, setBusy } from '../../core/dom.js';
import { failureMessage, getHtml, postForm } from '../../core/http.js';
import { notify } from '../../core/notify.js';

async function copy(text) {
    try {
        await navigator.clipboard.writeText(text);
        notify.success('Panoya kopyalandı.');
    } catch {
        notify.error('Panoya kopyalanamadı; metni seçip kopyalayın.');
    }
}

/**
 * Proje "Otomatik deploy" sekmesi: push webhook'unu açma/kapama ve gizli anahtar üretme. Yeni anahtar yalnızca
 * üretildiği cevapta gelir; panel yenilendikten sonra bir kez gösterilir.
 */
export function initProjectWebhook(root) {
    if (!root) return;

    const body = qs('[data-webhook-body]', root);

    async function reload(secret) {
        const response = await getHtml(root.dataset.panelUrl);
        if (response.ok) body.innerHTML = response.html;
        else notify.error(response.message || 'Webhook bilgisi yenilenemedi.');

        if (!secret) return;
        const box = qs('[data-webhook-secret-box]', body);
        qs('[data-webhook-secret]', box).textContent = secret;
        box.hidden = false;
        qs('[data-webhook-secret-state]', body).hidden = true;
    }

    on(root, 'click', '[data-webhook-toggle]', async (event, button) => {
        setBusy(button, true);
        try {
            const response = await postForm(root.dataset.toggleUrl, { enabled: button.dataset.enabled });
            if (!response.isSuccess) {
                notify.error(failureMessage(response, 'Ayar kaydedilemedi.'));
                return;
            }
            notify.success(response.message);
            await reload(response.data?.secret);
        } finally {
            setBusy(button, false);
        }
    });

    on(root, 'click', '[data-webhook-regenerate]', async () => {
        const response = await confirmAction({
            title: 'Gizli anahtar yenilensin mi?',
            message: 'Yeni anahtar üretilir ve eski anahtarla imzalanan istekler reddedilir. GitHub / GitLab webhook ayarına yeni anahtarı girmeniz gerekir.',
            confirmText: 'Yenile',
            action: () => postForm(root.dataset.regenerateUrl, {})
        });
        if (!response?.isSuccess) return;
        notify.success(response.message);
        await reload(response.data?.secret);
    });

    on(root, 'click', '[data-copy-text]', (event, button) => copy(button.dataset.copyText));
    on(root, 'click', '[data-webhook-secret-copy]', () => copy(qs('[data-webhook-secret]', root).textContent));
}
