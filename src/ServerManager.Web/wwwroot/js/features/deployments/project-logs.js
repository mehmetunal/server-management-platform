import { element, qs } from '../../core/dom.js';
import { failureMessage, getJson } from '../../core/http.js';
import { createLogViewer } from '../docker/logs.js';

/**
 * Proje "Çalışma logları" sekmesi: projenin container'larından biri seçilir ve Docker log görüntüleyicisi
 * (son N satır, arama, canlı takip, indirme) o container için çalışır. "Yalnızca hata/uyarı" sunucunun işaretlediği
 * satırları (error, fatal, panic, exception, warn) gösterir.
 */
export function initProjectLogs(root) {
    if (!root) return null;

    const select = qs('[data-logs-container]', root);
    const output = qs('[data-logs-output]', root);
    const viewer = createLogViewer(root);
    let loaded = false;

    const sourceUrl = name => `${root.dataset.logsUrl}?container=${encodeURIComponent(name)}`;

    function choose(name) {
        if (!name) return;
        viewer.setSource(sourceUrl(name), name);
        viewer.activate();
    }

    async function loadContainers() {
        const response = await getJson(root.dataset.containersUrl);
        select.replaceChildren();
        if (!response.isSuccess) {
            select.appendChild(element('option', '', '—'));
            output.replaceChildren(element('p', 'log-error', failureMessage(response, 'Container\'lar okunamadı.')));
            return;
        }

        const containers = response.data ?? [];
        if (containers.length === 0) {
            select.appendChild(element('option', '', '—'));
            output.replaceChildren(element('p', 'log-empty', 'Projeye ait container bulunamadı. Proje henüz deploy edilmemiş veya container\'lar silinmiş olabilir.'));
            return;
        }

        containers.forEach(container => {
            const label = container.service && container.service !== container.name
                ? `${container.service} (${container.name}) · ${container.state}`
                : `${container.name} · ${container.state}`;
            const option = element('option', '', label);
            option.value = container.name;
            select.appendChild(option);
        });
        const running = containers.find(c => c.state === 'running') ?? containers[0];
        select.value = running.name;
        choose(running.name);
    }

    select.addEventListener('change', () => choose(select.value));

    return {
        activate() {
            if (loaded) return;
            loaded = true;
            loadContainers();
        }
    };
}
