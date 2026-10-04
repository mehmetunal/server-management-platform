import { on } from '../core/dom.js';
import { notify } from '../core/notify.js';

on(document, 'click', '[data-copy-hash]', async (event, button) => {
    try {
        await navigator.clipboard.writeText(button.dataset.copyHash);
        notify.success('İmza panoya kopyalandı.');
    } catch {
        notify.error('Kopyalanamadı; imzayı elle seçip kopyalayın.');
    }
});
