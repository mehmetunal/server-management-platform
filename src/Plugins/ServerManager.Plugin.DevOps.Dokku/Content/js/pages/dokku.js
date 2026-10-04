import { on, qs, setBusy } from '@app/core/dom.js';
import { confirmAction } from '@app/core/dialog.js';
import { postForm } from '@app/core/http.js';
import { notify } from '@app/core/notify.js';
import { createRemotePanels } from '@app/components/remote-panels.js';
import { initServerPage } from '@app/features/servers/server-page.js';

let poll;

const panels = createRemotePanels({
    onLoaded: () => {
        clearTimeout(poll);
        if (qs('[data-dokku-installing]')) poll = setTimeout(() => panels.reload(), 2000);
    }
});

initServerPage({ onActionSuccess: () => panels.reload() });

async function reload(button) {
    setBusy(button, true);
    try {
        await panels.reload();
    } finally {
        setBusy(button, false);
    }
}

on(document, 'click', '[data-panel-refresh]', (event, button) => reload(button));

on(document, 'click', '[data-dokku-install]', async (event, button) => {
    const response = await confirmAction({
        title: 'Dokku kurulsun mu?',
        message: 'Resmi kurulum betiği bu sunucuda sudo ile çalışır. İşlem birkaç dakika sürer.',
        confirmText: 'Kur',
        danger: false,
        action: () => postForm(button.dataset.url)
    });
    if (!response?.isSuccess) return;
    notify.success(response.message || 'Kurulum başlatıldı.');
    await panels.reload();
});

on(document, 'click', '[data-dokku-restart]', async (event, button) => {
    const app = button.dataset.app || 'uygulama';
    const response = await confirmAction({
        title: `${app} yeniden başlatılsın mı?`,
        message: 'Çalışan süreç durur ve Dokku tarafından yeniden açılır.',
        confirmText: 'Yeniden başlat',
        action: () => postForm(button.dataset.url)
    });
    if (!response?.isSuccess) return;
    notify.success(response.message || 'Uygulama yeniden başlatıldı.');
    await panels.reload();
});
