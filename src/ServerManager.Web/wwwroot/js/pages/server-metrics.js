import { debounce, on, qs, qsa } from '../core/dom.js';
import { confirmAction, showSecretList } from '../core/dialog.js';
import { getHtml, postForm } from '../core/http.js';
import { notify } from '../core/notify.js';
import { createChartGroups } from '../features/monitoring/charts.js';
import { initServerPage } from '../features/servers/server-page.js';

const [charts] = createChartGroups();
const panel = qs('[data-metrics-panel]');
const agentPanel = qs('[data-agent-panel]');

async function refreshPanel() {
    if (!panel) return;
    const response = await getHtml(panel.dataset.panelUrl);
    if (response.ok) panel.innerHTML = response.html;
}

async function refreshAgentPanel() {
    if (!agentPanel) return;
    const response = await getHtml(agentPanel.dataset.panelUrl);
    if (response.ok) agentPanel.innerHTML = response.html;
}

const refreshLiveViews = debounce(() => {
    if (charts?.live) charts.refresh();
    refreshPanel();
    refreshAgentPanel();
}, 1500);

initServerPage({
    onUpdate: refreshLiveViews,
    regions: ['server-header'],
    onActionSuccess: async trigger => {
        if (agentPanel?.contains(trigger)) await refreshAgentPanel();
    }
});

on(document, 'click', '[data-range-link]', (event, link) => {
    event.preventDefault();
    if (!charts || link.classList.contains('is-active')) return;
    qsa('[data-range-link]').forEach(item => item.classList.toggle('is-active', item === link));
    qs('[data-range-description]').textContent = link.dataset.description;
    window.history.replaceState(window.history.state, '', link.href);
    charts.setSource(link.dataset.seriesUrl, link.dataset.live === 'true');
});

if (agentPanel) {
    refreshAgentPanel();

    on(agentPanel, 'click', '[data-agent-create]', async (event, button) => {
        event.preventDefault();
        const replace = button.dataset.replace === 'true';
        const response = await confirmAction({
            title: replace ? 'Yeni agent token\'ı oluşturulsun mu?' : 'Agent token\'ı oluşturulsun mu?',
            message: replace
                ? `${button.dataset.serverName} için yeni token üretilecek. Eski token hemen geçersiz olur; agent'ı yeni komutla yeniden kurmanız gerekir.`
                : `${button.dataset.serverName} için token üretilecek ve kurulum komutu bir kez gösterilecek.`,
            confirmText: 'Oluştur',
            danger: replace,
            action: () => postForm(button.dataset.url, new FormData())
        });
        if (!response) return;

        await showSecretList({
            title: 'Agent kurulum komutu',
            message: 'Komutu sunucuda root yetkisiyle çalıştırın. Token yalnızca şimdi gösterilir ve panelde saklanmaz; kaybederseniz yeni token oluşturun.',
            items: [response.data.installCommand]
        });
        await refreshAgentPanel();
    });

    on(agentPanel, 'click', '[data-copy-command]', async (event, button) => {
        event.preventDefault();
        const text = button.closest('.agent-command')?.querySelector('code')?.textContent ?? '';
        try {
            await navigator.clipboard.writeText(text);
            notify.success('Komut panoya kopyalandı.');
        } catch {
            notify.error('Panoya kopyalanamadı.');
        }
    });
}
