import { bindAjaxActions } from '../components/ajax-actions.js';
import { confirmAction } from '../core/dialog.js';
import { element, on, qs, setBusy } from '../core/dom.js';
import { clearErrors, showErrors } from '../core/forms.js';
import { failureMessage, getJson, postForm } from '../core/http.js';
import { navigate } from '../core/navigation.js';
import { notify } from '../core/notify.js';
import { initDeploymentList } from '../features/deployments/deployment-list.js';

const deployForm = qs('[data-deploy-form]');

bindAjaxActions(document);
initDeploymentList(qs('[data-ajax-list]'), { embedded: true });

async function startDeployment(event) {
    event.preventDefault();
    clearErrors(deployForm);
    const { projectName, branch } = deployForm.dataset;
    const commit = deployForm.elements.CommitSha.value.trim();
    const target = commit ? `${commit.slice(0, 12)} commit'i` : `${branch} dalının son hali`;

    let failed = null;
    const response = await confirmAction({
        title: 'Deployment başlatılsın mı?',
        message: `${projectName} projesi için ${target} sunucuya çekilip build edilecek ve çalışan uygulama yenisiyle değiştirilecek.`,
        confirmText: 'Deploy et',
        danger: false,
        action: async () => {
            const result = await postForm(deployForm.action, { CommitSha: commit });
            if (!result.isSuccess && Object.keys(result.errors ?? {}).length) {
                failed = result;
                return { isSuccess: true };
            }
            return result;
        }
    });
    if (failed) {
        showErrors(deployForm, failed);
        return;
    }
    if (response?.data) navigate(response.data, response.message);
}

function renderBranches(container, data) {
    container.replaceChildren();
    const ok = data.configuredBranchExists;
    container.className = ok ? 'project-branches alert-success' : 'project-branches alert-warning';
    container.appendChild(element('p', 'font-medium', ok
        ? `Depoya erişildi; ${data.configuredBranch} dalı mevcut.`
        : `Depoya erişildi ancak ${data.configuredBranch} dalı bulunamadı.`));
    if (data.branches.length) {
        const list = element('div', 'mt-2 flex flex-wrap gap-1');
        data.branches.slice(0, 30).forEach(name => list.appendChild(element('span', 'tag', name)));
        if (data.branches.length > 30) list.appendChild(element('span', 'text-xs', `+${data.branches.length - 30} dal daha`));
        container.appendChild(list);
    }
    container.hidden = false;
}

async function checkBranches(button) {
    const container = qs('[data-branches-result]', deployForm);
    setBusy(button, true);
    try {
        const response = await getJson(button.dataset.url);
        if (!response.isSuccess || !response.data) {
            const message = failureMessage(response, 'Depoya erişilemedi.');
            container.className = 'project-branches alert-error';
            container.textContent = message;
            container.hidden = false;
            notify.error(message);
            return;
        }
        renderBranches(container, response.data);
    } finally {
        setBusy(button, false);
    }
}

if (deployForm) {
    deployForm.addEventListener('submit', startDeployment);
    on(deployForm, 'click', '[data-branches-check]', (event, button) => checkBranches(button));
}
