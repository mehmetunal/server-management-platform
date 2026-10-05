import { createLogConsole } from '../components/log-console.js';
import { createStepper } from '../components/stepper.js';
import { qs } from '../core/dom.js';
import { notify } from '../core/notify.js';
import { onPageDispose } from '../core/page-scope.js';
import { createServiceHub } from '../features/managed-services/service-hub.js';

const STATUS_BADGES = {
    Running: ['badge-info', 'Sürüyor'],
    Succeeded: ['badge-success', 'Başarılı'],
    Failed: ['badge-danger', 'Başarısız'],
    Interrupted: ['badge-warning', 'Kesildi'],
    reconnecting: ['badge-warning', 'Yeniden bağlanıyor…'],
    disconnected: ['badge-danger', 'Bağlantı koptu']
};

const root = qs('[data-operation-view]');
const steps = root.dataset.steps.split(',');
const stepper = createStepper(root);
const consoleView = createLogConsole(qs('[data-operation-console]', root));
const statusBadge = qs('[data-operation-status]', root);
const resultBox = qs('[data-operation-result]', root);
const isRemove = root.dataset.kind === 'Remove';

let hub = null;
let completed = false;

function renderStatus(status) {
    const [className, text] = STATUS_BADGES[status] ?? STATUS_BADGES.Running;
    statusBadge.className = className;
    qs('[data-operation-status-text]', statusBadge).textContent = text;
}

function applyStage(stage) {
    const index = steps.indexOf(stage);
    if (index < 0) return;
    steps.forEach((step, i) => {
        if (i < index || stage === 'Completed') stepper.set(step, 'done');
        else if (i === index) stepper.set(step, 'active');
        else stepper.set(step, null);
    });
}

function showResult(status, message) {
    const tone = status === 'Succeeded' ? 'alert-success' : status === 'Failed' ? 'alert-error' : 'alert-warning';
    resultBox.className = `service-operation-result ${tone}`;
    qs('[data-operation-result-text]', resultBox).textContent = message || STATUS_BADGES[status]?.[1] || '';
    const dockerLink = qs('[data-operation-docker-link]', resultBox);
    const dockerProblem = status === 'Failed' && /Docker/i.test(message ?? '') && /kurulu değil|çalışmıyor/i.test(message ?? '');
    dockerLink.hidden = !dockerProblem;
    if (dockerProblem) dockerLink.href = root.dataset.dockerUrl;
    resultBox.hidden = false;
}

function revealLinks() {
    const log = qs('[data-operation-log]');
    const service = qs('[data-operation-service]');
    if (log) log.hidden = false;
    if (service && !isRemove) service.hidden = false;
}

function complete(status, message, { live }) {
    completed = true;
    renderStatus(status);
    if (status === 'Succeeded') stepper.setMany(steps, 'done');
    else stepper.failActive(steps[0], status === 'Failed' ? 'failed' : 'warning');
    showResult(status, message);
    revealLinks();

    if (!live) return;
    hub?.stop();
    if (status === 'Succeeded') {
        notify.success(message || 'İşlem tamamlandı.');
        if (isRemove) setTimeout(() => window.location.assign(root.dataset.listUrl), 1500);
    } else if (status === 'Failed') {
        notify.error(message || 'İşlem başarısız oldu.');
    } else {
        notify.warning(message || 'İşlem durduruldu.');
    }
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
    if (response.status === 'Running') {
        // Kayıt hâlâ sürüyor görünüyor ama bu uygulama örneği izlemiyor (ör. yeniden başlatma sırasında).
        completed = true;
        renderStatus('Interrupted');
        showResult('Interrupted', response.message);
        return;
    }
    complete(response.status, response.message, { live: false });
    hub?.stop();
}

async function watch() {
    hub = createServiceHub(root.dataset.hubUrl, {
        onSnapshot: handleSnapshot,
        onOutput: text => consoleView?.write(text),
        onStage: stage => applyStage(stage),
        onCompleted: (status, message) => complete(status, message, { live: true }),
        onConnection: state => {
            if (completed) return;
            if (state === 'reconnecting' || state === 'disconnected') renderStatus(state);
            else if (state === 'connected') renderStatus('Running');
        }
    });

    try {
        await hub.watch(root.dataset.operationId);
    } catch {
        renderStatus('disconnected');
        consoleView?.notice('Canlı çıktıya bağlanılamadı. Sayfayı yenileyerek tekrar deneyin.', '31');
    }
}

onPageDispose(() => hub?.stop());
watch();
