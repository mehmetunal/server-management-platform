import { element } from '../../core/dom.js';
import { failureMessage, getJson } from '../../core/http.js';

function note(className, text, link) {
    const box = element('div', `${className} service-probe-note`);
    box.appendChild(element('span', '', text));
    if (link?.href) {
        const anchor = element('a', 'panel-link', link.label);
        anchor.href = link.href;
        box.append(' ', anchor);
    }
    return box;
}

/**
 * Sunucunun servis kurulumuna uygunluğunu (Docker, işlemci mimarisi, Dokploy/Dokku) okur ve [data-service-probe]
 * kutusuna uyarı/bilgi notları yazar. requiresX86 verilirse ARM sunucuda uyarı gösterilir.
 */
export async function loadProbe(container, { url, dockerUrl, dokployUrl, dokkuUrl, requiresX86 = false, onResult } = {}) {
    if (!container || !url) return null;
    container.replaceChildren(element('p', 'service-probe-loading', 'Sunucu kontrol ediliyor…'));

    const response = await getJson(url);
    container.replaceChildren();
    if (!response.isSuccess) {
        container.appendChild(note('alert-warning', `Sunucu kontrol edilemedi: ${failureMessage(response, 'bağlantı kurulamadı.')}`));
        onResult?.(null);
        return null;
    }

    const probe = response.data;
    if (!probe.dockerInstalled) {
        container.appendChild(note('alert-error', 'Sunucuda Docker kurulu değil; servisler Docker container\'ı olarak çalışır. Docker\'ı kurduktan sonra tekrar deneyin.',
            { href: dockerUrl, label: 'Docker sekmesini aç' }));
    } else if (!probe.dockerRunning) {
        container.appendChild(note('alert-error', 'Docker kurulu ama servis çalışmıyor veya erişilemiyor (sudo ayarlarını kontrol edin).',
            { href: dockerUrl, label: 'Docker sekmesini aç' }));
    }

    const isX86 = ['x86_64', 'amd64'].includes(probe.architecture);
    if (requiresX86 && probe.architecture && !isX86) {
        container.appendChild(note('alert-error', `Bu sunucunun işlemci mimarisi ${probe.architecture}. SQL Server yalnızca x86_64 (amd64) sunucularda çalışır; kurulum başarısız olur.`));
    }

    if (probe.dokployDetected) {
        container.appendChild(note('alert-info', 'Bu sunucuda Dokploy kurulu. Veritabanlarını Dokploy üzerinden de yönetebilirsiniz; panelden kurulum yine mümkündür (port çakışmalarına dikkat edin).',
            { href: dokployUrl, label: 'Dokploy sekmesini aç' }));
    }
    if (probe.dokkuDetected) {
        container.appendChild(note('alert-info', 'Bu sunucuda Dokku kurulu. Servisleri Dokku eklentileriyle (dokku postgres:create …) de yönetebilirsiniz; panelden kurulum yine mümkündür.',
            { href: dokkuUrl, label: 'Dokku sekmesini aç' }));
    }

    onResult?.(probe);
    return probe;
}
