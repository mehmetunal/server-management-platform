import { confirmAction } from '@app/core/dialog.js';
import { on, qs, setBusy } from '@app/core/dom.js';
import { bindAjaxForm } from '@app/core/forms.js';
import { failureMessage, getHtml, postForm } from '@app/core/http.js';
import { notify } from '@app/core/notify.js';

const list = qs('[data-app-list]');

async function reloadList() {
    if (!list) return;
    list.setAttribute('aria-busy', 'true');
    try {
        const response = await getHtml(list.dataset.url);
        if (response.ok) {
            list.innerHTML = response.html;
        } else if (response.status !== 401) {
            notify.error(response.message || 'GitHub uygulamaları yüklenemedi.');
        }
    } finally {
        list.removeAttribute('aria-busy');
    }
}

/** GitHub manifest akışı tarayıcının GitHub'a doğrudan POST etmesini gerektirir; form geçici olarak oluşturulur. */
function submitManifest({ postUrl, manifest }) {
    const form = document.createElement('form');
    form.method = 'post';
    form.action = postUrl;
    form.hidden = true;
    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = 'manifest';
    input.value = manifest;
    form.append(input);
    document.body.append(form);
    form.submit();
}

bindAjaxForm(qs('[data-manifest-form]'), {
    onSuccess: response => {
        notify.info(response.message);
        submitManifest(response.data);
    }
});

bindAjaxForm(qs('[data-manual-form]'), {
    onSuccess: async (response, form) => {
        notify.success(response.message);
        form.reset();
        await reloadList();
    }
});

on(document, 'click', '[data-app-delete]', async (_, button) => {
    const card = button.closest('[data-app-id]');
    if (!card || !list) return;
    const name = card.dataset.appName;
    const response = await confirmAction({
        title: 'GitHub App kaldırılsın mı?',
        message: `${name} panelden kaldırılacak. GitHub'daki uygulama ve kurulumları silinmez; gerekirse GitHub ayarlarından kaldırın.`,
        confirmText: 'Kaldır',
        expected: name,
        action: () => postForm(list.dataset.deleteUrl, { id: card.dataset.appId })
    });
    if (!response) return;
    notify.success(response.message);
    await reloadList();
});

on(document, 'click', '[data-github-refresh]', async (_, button) => {
    setBusy(button, true);
    try {
        const response = await postForm(button.dataset.url, {});
        if (!response.isSuccess) {
            notify.error(failureMessage(response));
            return;
        }
        await reloadList();
        notify.success(response.message);
    } finally {
        setBusy(button, false);
    }
});
