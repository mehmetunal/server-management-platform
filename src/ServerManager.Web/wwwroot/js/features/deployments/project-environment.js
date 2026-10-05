import { confirmAction } from '../../core/dialog.js';
import { element, on, qs, setBusy } from '../../core/dom.js';
import { clearErrors, showErrors } from '../../core/forms.js';
import { failureMessage, getHtml, postForm } from '../../core/http.js';
import { notify } from '../../core/notify.js';

const MAX_IMPORT_BYTES = 64 * 1024;

/**
 * Proje "Ortam değişkenleri" sekmesi: anahtar/değer tablosu, tek değişken ekleme/düzenleme/silme, içe aktarma ve
 * maskelenmiş değerleri tek tek gösterme. Değerler yalnızca "Göster" ile (yetki ve audit) sunucudan istenir.
 */
export function initProjectEnvironment(root) {
    if (!root) return;

    const table = qs('[data-env-table]', root);
    const form = qs('[data-env-form]', root);
    const importForm = qs('[data-env-import]', root);
    const revealed = new Map();

    async function reload() {
        const response = await getHtml(root.dataset.panelUrl);
        if (response.ok) {
            table.innerHTML = response.html;
            revealed.clear();
        } else {
            notify.error(response.message || 'Ortam değişkenleri yenilenemedi.');
        }
    }

    function openForm({ key = '', value = '', isNew }) {
        if (!form) return;
        if (importForm) importForm.hidden = true;
        clearErrors(form);
        form.hidden = false;
        form.elements.IsNew.value = isNew ? 'true' : 'false';
        form.elements.Key.value = key;
        form.elements.Key.readOnly = !isNew;
        form.elements.Value.value = value;
        qs('[data-env-value-hint]', form).hidden = isNew || value !== '';
        (isNew ? form.elements.Key : form.elements.Value).focus();
    }

    async function reveal(row, button) {
        const key = row.dataset.key;
        setBusy(button, true);
        try {
            const response = await postForm(root.dataset.revealUrl, { key });
            if (!response.isSuccess) {
                notify.error(failureMessage(response, 'Değer gösterilemedi.'));
                return;
            }
            const value = response.data ?? '';
            revealed.set(key, value);
            const cell = qs('[data-env-value]', row);
            cell.replaceChildren(value === '' ? element('span', 'env-mask', '(boş)') : document.createTextNode(value));
            button.hidden = true;
            qs('[data-env-hide]', row).hidden = false;
        } finally {
            setBusy(button, false);
        }
    }

    function hide(row) {
        revealed.delete(row.dataset.key);
        qs('[data-env-value]', row).replaceChildren(element('span', 'env-mask', '••••••••'));
        qs('[data-env-hide]', row).hidden = true;
        qs('[data-env-reveal]', row).hidden = false;
    }

    on(root, 'click', '[data-env-reveal]', (event, button) => reveal(button.closest('[data-env-row]'), button));
    on(root, 'click', '[data-env-hide]', (event, button) => hide(button.closest('[data-env-row]')));
    on(root, 'click', '[data-env-add]', () => openForm({ isNew: true }));
    on(root, 'click', '[data-env-edit]', (event, button) => {
        const key = button.closest('[data-env-row]').dataset.key;
        openForm({ key, value: revealed.get(key) ?? '', isNew: false });
    });
    on(root, 'click', '[data-env-cancel]', () => { form.hidden = true; });
    on(root, 'click', '[data-env-delete]', async (event, button) => {
        const key = button.closest('[data-env-row]').dataset.key;
        const response = await confirmAction({
            title: 'Değişken silinsin mi?',
            message: `${key} kayıtlı ortam değişkenlerinden silinecek. Sunucudaki .env bir sonraki deploy'da veya "Uygula / Yeniden başlat" ile güncellenir.`,
            confirmText: 'Sil',
            action: () => postForm(root.dataset.deleteUrl, { key })
        });
        if (!response?.isSuccess) return;
        notify.success(response.message);
        await reload();
    });

    form?.addEventListener('submit', async event => {
        event.preventDefault();
        clearErrors(form);
        const button = qs('[type="submit"]', form);
        setBusy(button, true);
        try {
            const response = await postForm(form.action, {
                Key: form.elements.Key.value.trim(),
                Value: form.elements.Value.value,
                IsNew: form.elements.IsNew.value
            });
            if (!response.isSuccess) {
                if (Object.keys(response.errors ?? {}).length) showErrors(form, response);
                notify.error(failureMessage(response, 'Değişken kaydedilemedi.'));
                return;
            }
            notify.success(response.message);
            form.hidden = true;
            form.elements.Value.value = '';
            await reload();
        } finally {
            setBusy(button, false);
        }
    });

    on(root, 'click', '[data-env-import-open]', () => {
        if (!importForm) return;
        if (form) form.hidden = true;
        clearErrors(importForm);
        importForm.hidden = false;
        importForm.elements.Content.focus();
    });
    on(root, 'click', '[data-env-import-cancel]', () => { importForm.hidden = true; });

    qs('[data-env-import-file]', root)?.addEventListener('change', async event => {
        const file = event.target.files?.[0];
        if (!file) return;
        if (file.size > MAX_IMPORT_BYTES) {
            notify.error('Dosya en fazla 64 KB olabilir.');
            event.target.value = '';
            return;
        }
        importForm.elements.Content.value = await file.text();
    });

    importForm?.addEventListener('submit', async event => {
        event.preventDefault();
        clearErrors(importForm);
        const mode = importForm.elements.Mode.value;
        const send = () => postForm(importForm.action, { Content: importForm.elements.Content.value, Mode: mode });

        let response;
        if (mode === '2') {
            response = await confirmAction({
                title: 'Tüm değişkenler değiştirilsin mi?',
                message: 'Kayıtlı ortam değişkenleri yapıştırdığınız içerikle tamamen değiştirilir; listede olmayan değişkenler silinir.',
                confirmText: 'Tümünü değiştir',
                action: async () => {
                    const result = await send();
                    if (!result.isSuccess && Object.keys(result.errors ?? {}).length) {
                        showErrors(importForm, result);
                    }
                    return result;
                }
            });
            if (!response) return;
        } else {
            const button = qs('[type="submit"]', importForm);
            setBusy(button, true);
            try {
                response = await send();
            } finally {
                setBusy(button, false);
            }
            if (!response.isSuccess) {
                if (Object.keys(response.errors ?? {}).length) showErrors(importForm, response);
                notify.error(failureMessage(response, 'İçe aktarma yapılamadı.'));
                return;
            }
        }

        notify.success(response.message || 'İçe aktarıldı.');
        importForm.hidden = true;
        importForm.elements.Content.value = '';
        const file = qs('[data-env-import-file]', importForm);
        if (file) file.value = '';
        await reload();
    });
}
