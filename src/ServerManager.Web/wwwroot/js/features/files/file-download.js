import { notify } from '../../core/notify.js';

/**
 * İndirme gizli iframe üzerinden yapılır: dosya tarayıcıya doğrudan akar (belleğe alınmaz).
 * Başarılı yanıt ek (attachment) olduğu için iframe'e belge yüklenmez; hata yanıtı ise düz metin
 * olarak iframe'e yüklenir ve kullanıcıya bildirim olarak gösterilir.
 */
export function createDownloader(frame) {
    if (!frame) return { download: url => window.open(url, '_blank') };

    frame.addEventListener('load', () => {
        let message = '';
        try {
            message = frame.contentDocument?.body?.innerText?.trim() ?? '';
        } catch {
            message = '';
        }
        if (message) notify.error(message);
    });

    return {
        download(url) {
            frame.src = url;
        }
    };
}
