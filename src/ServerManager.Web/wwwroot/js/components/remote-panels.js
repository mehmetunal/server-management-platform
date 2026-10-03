import { element, qsa } from '../core/dom.js';
import { getHtml } from '../core/http.js';

function errorPanel(message) {
    const panel = element('section', 'panel');
    panel.appendChild(element('p', 'panel-body text-sm text-red-600 dark:text-red-400', message));
    return panel;
}

/**
 * [data-remote-panel] öğelerini data-url'deki partial ile doldurur.
 * 404 yanıtı da panel hata partial'ı içerdiği için olduğu gibi gösterilir.
 */
export function createRemotePanels({ onLoaded } = {}) {
    const panels = qsa('[data-remote-panel]');

    async function load(panel) {
        panel.setAttribute('aria-busy', 'true');
        const response = await getHtml(panel.dataset.url);
        panel.removeAttribute('aria-busy');
        if (response.ok || response.status === 404) {
            panel.innerHTML = response.html;
        } else if (response.status !== 401) {
            panel.replaceChildren(errorPanel(response.message || 'Panel yüklenemedi.'));
        }
        onLoaded?.(panel);
    }

    panels.forEach(load);

    return {
        panels,
        reload: () => Promise.all(panels.map(load))
    };
}
