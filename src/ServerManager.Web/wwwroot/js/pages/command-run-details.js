import { startAutoRefresh } from '../components/auto-refresh.js';
import { qs, qsa } from '../core/dom.js';
import { notify } from '../core/notify.js';
import { refreshRegions } from '../core/regions.js';

const REFRESH_MS = 2500;
const REGION = 'command-run-body';

const isRunning = () => qs('[data-command-run]')?.dataset.running === 'true';

function openState() {
    return new Map(qsa('[data-target-id]').map(item => [item.dataset.targetId, item.open]));
}

let refresher = null;

async function refresh() {
    const state = openState();
    await refreshRegions([REGION]);
    qsa('[data-target-id]').forEach(item => {
        if (state.has(item.dataset.targetId)) item.open = state.get(item.dataset.targetId);
    });
    if (!isRunning()) refresher?.stop();
}

document.addEventListener('click', async event => {
    const button = event.target.closest('[data-copy-output]');
    if (!button) return;
    const output = button.closest('[data-target-id]')?.querySelector('.command-output')?.textContent ?? '';
    try {
        await navigator.clipboard.writeText(output);
        notify.success('Çıktı panoya kopyalandı.');
    } catch {
        notify.error('Panoya kopyalanamadı.');
    }
});

if (isRunning()) refresher = startAutoRefresh(refresh, REFRESH_MS);
