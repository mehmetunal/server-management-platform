import { confirmAction } from '../core/dialog.js';
import { element, on, qs, qsa, setBusy } from '../core/dom.js';
import { getHtml, postForm } from '../core/http.js';
import { notify } from '../core/notify.js';
import { createInstallConsole } from '../features/dokploy/install-console.js';
import { createInstallHub } from '../features/dokploy/install-hub.js';
import { createStepper } from '../features/dokploy/install-stepper.js';
import { initServerPage } from '../features/servers/server-page.js';

const CHECK_STEPS = ['compatibility', 'docker', 'ports'];
const STATUS_BADGES = {
    running: ['badge-info', 'Sürüyor'],
    succeeded: ['badge-success', 'Tamamlandı'],
    failed: ['badge-danger', 'Başarısız'],
    reconnecting: ['badge-warning', 'Yeniden bağlanıyor…'],
    disconnected: ['badge-danger', 'Bağlantı koptu']
};
const GROUP_STATES = { passed: 'done', warning: 'warning', failed: 'failed' };

const root = qs('[data-dokploy-wizard]');
const stepper = createStepper(root);
const panels = Object.fromEntries(qsa('[data-wizard-panel]', root).map(panel => [panel.dataset.wizardPanel, panel]));
const statusBadge = qs('[data-install-status]', root);
const form = qs('[data-install-form]', root);
const { serverId, serverName } = root.dataset;
const canInstall = root.dataset.canInstall === 'true';

let consoleView = null;
let hub = null;
let completed = false;

initServerPage();

function showPanel(name, visible = true) {
    if (panels[name]) panels[name].hidden = !visible;
}

function renderStatus(state) {
    if (!statusBadge) return;
    const [className, text] = STATUS_BADGES[state] ?? STATUS_BADGES.running;
    statusBadge.className = className;
    qs('[data-install-status-text]', statusBadge).textContent = text;
}

function applyStage(stage) {
    switch (stage) {
        case 'HealthCheck':
            stepper.set('install', 'done');
            stepper.set('health', 'active');
            break;
        case 'Completed':
            stepper.setMany(['install', 'health', 'complete'], 'done');
            break;
        case 'Failed':
            stepper.failActive('install');
            break;
        default:
            stepper.set('install', 'active');
    }
}

function showResult(succeeded, message) {
    completed = true;
    renderStatus(succeeded ? 'succeeded' : 'failed');
    const result = qs('[data-install-result]', root);
    const text = qs('[data-install-message]', root);
    text.className = succeeded ? 'alert-success' : 'alert-error';
    text.textContent = message || (succeeded ? 'Dokploy kuruldu.' : 'Kurulum başarısız oldu.');
    const again = qs('[data-install-again]', root);
    if (again) again.hidden = succeeded;
    result.hidden = false;
}

async function watch(installationId) {
    CHECK_STEPS.concat('confirm').forEach(step => stepper.set(step, 'done'));
    stepper.set('install', 'active');
    showPanel('check', false);
    showPanel('confirm', false);
    showPanel('install');
    consoleView ??= createInstallConsole(qs('[data-install-console]', root));
    renderStatus('running');

    hub = createInstallHub(root.dataset.hubUrl, {
        onSnapshot: response => {
            consoleView?.reset();
            if (!response.isSuccess) {
                showResult(false, response.message);
                stepper.failActive('install');
                return;
            }
            if (response.output) consoleView?.write(response.output);
            applyStage(response.stage);
            if (response.isCompleted) showResult(response.succeeded, response.message);
        },
        onOutput: text => consoleView?.write(text),
        onStage: stage => applyStage(stage),
        onCompleted: (succeeded, message) => {
            showResult(succeeded, message);
            if (succeeded) notify.success(message || 'Dokploy kuruldu.');
            else notify.error(message || 'Kurulum başarısız oldu.');
        },
        onConnection: state => {
            if (completed) return;
            if (state === 'connected') renderStatus('running');
            else if (state === 'reconnecting' || state === 'disconnected') renderStatus(state);
        }
    });

    try {
        await hub.watch(serverId, installationId);
    } catch {
        renderStatus('disconnected');
        consoleView?.notice('Canlı çıktıya bağlanılamadı. Sayfayı yenileyerek tekrar deneyin.', '31');
    }
}

function applyCompatibility(container) {
    const report = qs('[data-compatibility]', container);
    if (!report) {
        stepper.setMany(CHECK_STEPS, 'failed');
        showPanel('confirm', false);
        return;
    }
    qsa('[data-check-group]', report).forEach(group => stepper.set(group.dataset.checkGroup, GROUP_STATES[group.dataset.status] ?? 'done'));
    const ready = report.dataset.canInstall === 'true';
    showPanel('confirm', ready);
    stepper.set('confirm', ready ? 'active' : null);
}

async function runCompatibility(button) {
    const container = qs('[data-compatibility-panel]', root);
    stepper.setMany(CHECK_STEPS, 'active');
    stepper.set('confirm', null);
    showPanel('confirm', false);
    setBusy(button, true);
    try {
        const response = await getHtml(root.dataset.compatibilityUrl);
        if (response.ok || response.status === 404) {
            container.innerHTML = response.html;
        } else if (response.status !== 401) {
            const panel = element('section', 'panel');
            panel.appendChild(element('p', 'panel-body text-sm text-red-600 dark:text-red-400', response.message || 'Kontrol yapılamadı.'));
            container.replaceChildren(panel);
        }
        applyCompatibility(container);
    } finally {
        setBusy(button, false);
    }
}

function selectedVersion() {
    const choice = form.querySelector('input[name="VersionChoice"]:checked')?.value ?? '';
    if (choice === 'custom') return form.elements.Version.value.trim();
    return choice;
}

async function startInstall(event) {
    event.preventDefault();
    const version = selectedVersion();
    const versionText = version || 'son kararlı sürüm';
    const response = await confirmAction({
        title: 'Dokploy kurulumu',
        message: `${serverName} sunucusuna Dokploy (${versionText}) kurulacak. Betik root yetkisiyle çalışır ve Docker Swarm'ı yeniden başlatır.`,
        confirmText: 'Kurulumu başlat',
        expected: serverName,
        action: ({ confirmation }) => postForm(root.dataset.startUrl, { Version: version, ConfirmationName: confirmation })
    });
    if (!response) return;

    notify.success(response.message || 'Kurulum başlatıldı.');
    const installationId = response.data;
    history.replaceState(null, '', `${root.dataset.installUrl}?installationId=${encodeURIComponent(installationId)}`);
    await watch(installationId);
}

if (root.dataset.installationId) {
    watch(root.dataset.installationId);
} else if (canInstall) {
    showPanel('check');
    runCompatibility(qs('[data-compatibility-retry]', root));
    on(root, 'click', '[data-compatibility-retry], [data-panel-refresh]', (event, button) => runCompatibility(button));
    form?.addEventListener('submit', startInstall);
    on(root, 'change', 'input[name="VersionChoice"]', () => {
        const custom = form.querySelector('input[name="VersionChoice"]:checked')?.value === 'custom';
        qs('[data-custom-version]', form).hidden = !custom;
        if (custom) form.elements.Version.focus();
    });
}
