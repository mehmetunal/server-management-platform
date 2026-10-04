import { qs } from '../core/dom.js';
import { confirmAction } from '../core/dialog.js';
import { clearErrors, showErrors } from '../core/forms.js';
import { postForm } from '../core/http.js';
import { navigate } from '../core/navigation.js';

const form = qs('[data-restore-form]');
const server = qs('[data-restore-server]', form);

form.addEventListener('submit', async event => {
    event.preventDefault();
    clearErrors(form);

    const serverName = server.selectedOptions[0]?.textContent.trim() ?? '';
    let failed = null;
    const response = await confirmAction({
        title: 'Geri yükleme başlatılsın mı?',
        message: `${form.dataset.sourceName} yedeği ${serverName} sunucusuna yazılacak. Hedefteki aynı adlı veri yedektekiyle değiştirilir ve bu işlem geri alınamaz.`,
        confirmText: 'Geri yükle',
        checkLabel: 'Hedefteki verinin üzerine yazılacağını anladım.',
        action: async ({ checked }) => {
            const data = new FormData(form);
            data.set('Confirmed', checked ? 'true' : 'false');
            const result = await postForm(form.action, data);
            if (!result.isSuccess && result.errors && Object.keys(result.errors).length) failed = result;
            return result;
        }
    });

    if (failed) showErrors(form, failed);
    if (response) navigate(response.data, response.message);
});
