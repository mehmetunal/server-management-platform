import { createLogConsole } from '../components/log-console.js';
import { createStepper } from '../components/stepper.js';
import { confirmAction } from '../core/dialog.js';
import { on, qs } from '../core/dom.js';
import { postForm } from '../core/http.js';
import { navigate } from '../core/navigation.js';
import { notify } from '../core/notify.js';
import { refreshRegions } from '../core/regions.js';
import { createDeploymentHub } from '../features/deployments/deployment-hub.js';
import { bindRollback } from '../features/deployments/rollback.js';

const STAGES = ['Preparing', 'Source', 'Building', 'Deploying', 'Completed'];
const STATUS_BADGES = {
    Started: ['badge-info', 'Başladı'],
    Building: ['badge-info', 'Build ediliyor'],
    Deploying: ['badge-info', 'Deploy ediliyor'],
    Succeeded: ['badge-success', 'Başarılı'],
    Failed: ['badge-danger', 'Başarısız'],
    Cancelled: ['badge-warning', 'İptal edildi'],
    Interrupted: ['badge-warning', 'Kesildi'],
    reconnecting: ['badge-warning', 'Yeniden bağlanıyor…'],
    disconnected: ['badge-danger', 'Bağlantı koptu']
};
const STAGE_STATUS = { Preparing: 'Started', Source: 'Started', Building: 'Building', Deploying: 'Deploying' };
const RUNNING_STATUSES = ['Started', 'Building', 'Deploying'];
const REGIONS = ['deployment-heading', 'deployment-actions', 'deployment-summary'];

const root = qs('[data-deployment-view]');
const { deploymentId, projectName } = root.dataset;
const stepper = createStepper(root);
const consoleView = createLogConsole(qs('[data-deployment-console]', root));
const statusBadge = qs('[data-deployment-status]', root);
const resultBox = qs('[data-deployment-result]', root);

let hub = null;
let completed = false;

function renderStatus(status) {
    const [className, text] = STATUS_BADGES[status] ?? STATUS_BADGES.Started;
    statusBadge.className = className;
    qs('[data-deployment-status-text]', statusBadge).textContent = text;
}

function applyStage(stage) {
    const index = STAGES.indexOf(stage);
    if (index < 0) return;
    STAGES.forEach((step, i) => {
        if (i < index || stage === 'Completed') stepper.set(step, 'done');
        else if (i === index) stepper.set(step, 'active');
        else stepper.set(step, null);
    });
    if (!completed && STAGE_STATUS[stage]) renderStatus(STAGE_STATUS[stage]);
}

function showResult(status, message) {
    const tone = status === 'Succeeded' ? 'alert-success' : status === 'Failed' ? 'alert-error' : 'alert-warning';
    resultBox.className = `deployment-result ${tone}`;
    resultBox.textContent = message || STATUS_BADGES[status]?.[1] || '';
    resultBox.hidden = !resultBox.textContent;
}

async function complete(status, message, { live }) {
    completed = true;
    renderStatus(status);
    if (status === 'Succeeded') stepper.setMany(STAGES, 'done');
    else stepper.failActive('Preparing', status === 'Failed' ? 'failed' : 'warning');
    showResult(status, message);

    if (!live) return;
    if (status === 'Succeeded') notify.success(message || 'Deployment tamamlandı.');
    else if (status === 'Failed') notify.error(message || 'Deployment başarısız oldu.');
    else notify.warning(message || 'Deployment durduruldu.');
    hub?.stop();
    await refreshRegions(REGIONS);
}

function handleSnapshot(response) {
    consoleView?.reset();
    if (!response.isSuccess) {
        completed = true;
        showResult('Failed', response.message);
        return;
    }
    if (response.output) consoleView?.write(response.output);
    applyStage(response.stage);

    if (!response.isCompleted) return;
    if (RUNNING_STATUSES.includes(response.status)) {
        // Kayıt hâlâ çalışıyor görünüyor ama bu uygulama örneği izlemiyor (ör. yeniden başlatma sırasında).
        completed = true;
        renderStatus(response.status);
        showResult('Interrupted', response.message);
        return;
    }
    complete(response.status, response.message, { live: false });
    hub?.stop();
}

async function watch() {
    hub = createDeploymentHub(root.dataset.hubUrl, {
        onSnapshot: handleSnapshot,
        onOutput: text => consoleView?.write(text),
        onStage: stage => applyStage(stage),
        onCommit: () => refreshRegions(['deployment-heading', 'deployment-summary']),
        onCompleted: (status, message) => complete(status, message, { live: true }),
        onConnection: state => {
            if (completed) return;
            if (state === 'reconnecting' || state === 'disconnected') renderStatus(state);
        }
    });

    try {
        await hub.watch(deploymentId);
    } catch {
        renderStatus('disconnected');
        consoleView?.notice('Canlı çıktıya bağlanılamadı. Sayfayı yenileyerek tekrar deneyin.', '31');
    }
}

on(document, 'click', '[data-deployment-cancel]', async (event, button) => {
    const response = await confirmAction({
        title: 'Deployment iptal edilsin mi?',
        message: `${projectName} için çalışan komut durdurulacak. Sunucuda o ana kadar yapılan değişiklikler (çekilen kod, build edilen image) geri alınmaz.`,
        confirmText: 'İptal et',
        action: () => postForm(button.dataset.url)
    });
    if (response) notify.info(response.message || 'İptal isteği gönderildi.');
});

on(document, 'click', '[data-deployment-redeploy]', async (event, button) => {
    const commit = (button.dataset.commit ?? '').slice(0, 12);
    const response = await confirmAction({
        title: 'Yeniden deploy edilsin mi?',
        message: `${projectName} projesi ${commit} commit'iyle, projenin güncel ayarları kullanılarak yeniden dağıtılacak.`,
        confirmText: 'Yeniden deploy',
        danger: false,
        action: () => postForm(button.dataset.url)
    });
    if (response?.data) navigate(response.data, response.message);
});

bindRollback(document);

watch();
