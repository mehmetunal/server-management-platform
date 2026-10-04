import { createAjaxList } from '../components/ajax-list.js';
import { alertError, alertSuccess } from '../core/dialog.js';
import { on, qs, setBusy } from '../core/dom.js';
import { failureMessage, postForm } from '../core/http.js';
import { notify } from '../core/notify.js';

const form = qs('[data-list-filter]');
createAjaxList(qs('[data-ajax-list]'), { form });

on(document, 'click', '[data-audit-export]', (event, link) => {
    event.preventDefault();
    const params = new URLSearchParams();
    for (const [key, value] of new FormData(form)) {
        if (typeof value === 'string' && value.trim() !== '') params.append(key, value.trim());
    }
    const query = params.toString();
    window.location.href = query ? `${link.href}?${query}` : link.href;
    notify.info('CSV dosyası hazırlanıyor; indirme birazdan başlar.');
});

on(document, 'click', '[data-audit-verify]', async (event, button) => {
    if (button.disabled) return;
    setBusy(button, true, 'Doğrulanıyor…');
    try {
        const response = await postForm(button.dataset.url);
        if (!response.isSuccess) {
            notify.error(failureMessage(response, 'Bütünlük doğrulaması yapılamadı.'));
            return;
        }
        const result = response.data;
        const summary = `Kontrol edilen kayıt: ${result.checkedCount}, imzalı: ${result.signedCount}, zincir öncesi imzasız: ${result.unsignedCount}.`;
        if (result.isValid) await alertSuccess('Audit log sağlam', `${result.message} ${summary}`);
        else await alertError('Audit log bütünlüğü bozuk', `${result.message} ${summary}`);
    } finally {
        setBusy(button, false);
    }
});
