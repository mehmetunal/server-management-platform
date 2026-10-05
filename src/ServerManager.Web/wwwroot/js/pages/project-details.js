import { bindAjaxActions } from '../components/ajax-actions.js';
import { createTabs } from '../components/tabs.js';
import { confirmAction } from '../core/dialog.js';
import { element, on, qs, setBusy } from '../core/dom.js';
import { clearErrors, showErrors } from '../core/forms.js';
import { failureMessage, getHtml, getJson, postForm } from '../core/http.js';
import { navigate } from '../core/navigation.js';
import { notify } from '../core/notify.js';
import { initDeploymentList } from '../features/deployments/deployment-list.js';
import { initProjectEnvironment } from '../features/deployments/project-environment.js';
import { initProjectLogs } from '../features/deployments/project-logs.js';
import { initProjectWebhook } from '../features/deployments/project-webhook.js';

const deployForm = qs('[data-deploy-form]');

bindAjaxActions(document);
initDeploymentList(qs('[data-ajax-list]'), { embedded: true });
initProjectEnvironment(qs('[data-env]'));
initProjectWebhook(qs('[data-webhook]'));
const runtimeLogs = initProjectLogs(qs('[data-project-logs]'));

const tabs = createTabs({
    defaultTab: 'overview',
    activators: { logs: () => runtimeLogs?.activate() }
});
on(document, 'click', '[data-open-tab]', (event, button) => tabs?.activate(button.dataset.openTab));

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

const domains = qs('[data-domains]');
const tlsHints = { 1: 'cloudflare', 2: 'letsencrypt', 3: 'custom' };

function domainForm() {
    return qs('[data-domain-form]', domains);
}

function syncDomainForm() {
    const form = domainForm();
    if (!form) return;
    const composeField = qs('[data-compose-field]', form);
    if (composeField) composeField.hidden = domains.dataset.buildType !== domains.dataset.compose;
    const mode = form.elements.TlsMode.value;
    qs('[data-custom-fields]', form).hidden = mode !== domains.dataset.custom;
    domains.querySelectorAll('[data-tls-hint]').forEach(hint => {
        hint.hidden = hint.dataset.tlsHint !== tlsHints[mode];
    });
    const keep = qs('[data-cert-keep]', form);
    if (keep) keep.hidden = !form.elements.Id.value || mode !== domains.dataset.custom;
}

function openDomainForm(values) {
    const form = domainForm();
    if (!form) return;
    clearErrors(form);
    form.hidden = false;
    form.elements.Id.value = values?.id ?? '';
    form.elements.Host.value = values?.host ?? '';
    form.elements.Path.value = values?.path ?? '';
    form.elements.ContainerPort.value = values?.port ?? '80';
    form.elements.ServiceName.value = values?.service ?? '';
    form.elements.TlsMode.value = values?.tls ?? '1';
    form.elements.CertificatePem.value = '';
    form.elements.PrivateKey.value = '';
    syncDomainForm();
    form.elements.Host.focus();
}

async function reloadDomains() {
    const list = qs('[data-domain-list]', domains);
    const response = await getHtml(list.dataset.url);
    if (response.ok) list.innerHTML = response.html;
    else notify.error(response.message || 'Domain listesi yenilenemedi.');
}

async function loadProxyStatus() {
    const status = qs('[data-proxy-status]', domains);
    if (!status) return;
    const response = await getJson(status.dataset.url);
    status.textContent = response.isSuccess ? (response.data?.message || response.message) : failureMessage(response, 'Vekil durumu okunamadı.');
}

async function postBusy(button, fallback) {
    setBusy(button, true);
    try {
        const response = await postForm(button.dataset.url, {});
        if (!response.isSuccess) {
            notify.error(failureMessage(response, fallback));
            return;
        }
        notify.success(response.message);
        await loadProxyStatus();
    } finally {
        setBusy(button, false);
    }
}

if (domains && domains.dataset.buildType !== domains.dataset.commands) {
    const form = domainForm();
    loadProxyStatus();
    syncDomainForm();
    if (form) {
        form.elements.TlsMode.addEventListener('change', syncDomainForm);
        form.addEventListener('submit', async event => {
            event.preventDefault();
            clearErrors(form);
            const button = qs('[type="submit"]', form);
            setBusy(button, true);
            try {
                const payload = {
                    Host: form.elements.Host.value,
                    Path: form.elements.Path.value,
                    ContainerPort: form.elements.ContainerPort.value,
                    ServiceName: form.elements.ServiceName.value,
                    TlsMode: form.elements.TlsMode.value,
                    CertificatePem: form.elements.CertificatePem.value,
                    PrivateKey: form.elements.PrivateKey.value
                };
                if (form.elements.Id.value) payload.Id = form.elements.Id.value;
                const response = await postForm(form.action, payload);
                if (!response.isSuccess) {
                    if (Object.keys(response.errors ?? {}).length) showErrors(form, response);
                    notify.error(failureMessage(response, 'Domain kaydedilemedi.'));
                    return;
                }
                notify.success(response.message);
                form.hidden = true;
                await reloadDomains();
            } finally {
                setBusy(button, false);
            }
        });
    }

    on(domains, 'click', '[data-domain-add]', () => openDomainForm());
    on(domains, 'click', '[data-domain-cancel]', () => { domainForm().hidden = true; });
    on(domains, 'click', '[data-domain-edit]', (event, button) => openDomainForm(button.dataset));
    on(domains, 'click', '[data-proxy-install]', (event, button) => postBusy(button, 'Vekil kurulamadı.'));
    on(domains, 'click', '[data-routing-apply]', async (event, button) => {
        await postBusy(button, 'Yönlendirme uygulanamadı.');
    });
    on(domains, 'click', '[data-domain-delete]', async (event, button) => {
        const host = button.dataset.host;
        const response = await confirmAction({
            title: 'Domain silinsin mi?',
            message: `${host} yönlendirmesi panelden kaldırılır. DNS kaydına dokunulmaz.`,
            confirmText: 'Sil',
            expected: host,
            action: ({ confirmation }) => postForm(domains.dataset.deleteUrl, { domainId: button.dataset.id, confirmationHost: confirmation })
        });
        if (!response?.isSuccess) return;
        notify.success(response.message);
        await reloadDomains();
    });
}
