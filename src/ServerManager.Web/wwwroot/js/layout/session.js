import { on, setBusy } from '../core/dom.js';
import { postForm, failureMessage } from '../core/http.js';
import { notify } from '../core/notify.js';
import { navigate } from '../core/navigation.js';

export function initLogout() {
    on(document, 'click', '[data-logout]', async (event, button) => {
        setBusy(button, true);
        const response = await postForm(button.dataset.url);
        if (!response.isSuccess) {
            setBusy(button, false);
            notify.error(failureMessage(response, 'Çıkış yapılamadı.'));
            return;
        }
        navigate(response.data, response.message, 'info');
    });
}
