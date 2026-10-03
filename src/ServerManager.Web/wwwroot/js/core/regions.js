import { getHtml } from './http.js';

/**
 * Sayfanın güncel halini arka planda alıp yalnızca [data-region] ile işaretli bölgeleri değiştirir.
 * Bölgelerdeki etkileşimler olay yetkilendirmesiyle bağlandığı için yeniden bağlama gerekmez.
 */
export async function refreshRegions(names, url = window.location.href) {
    const response = await getHtml(url, { partial: false });
    if (!response.ok) return false;

    const doc = new DOMParser().parseFromString(response.html, 'text/html');
    for (const name of names) {
        const selector = `[data-region="${name}"]`;
        const current = document.querySelector(selector);
        const next = doc.querySelector(selector);
        if (current && next) current.replaceWith(document.importNode(next, true));
    }
    return true;
}
