import { confirmAction } from '../../core/dialog.js';
import { on } from '../../core/dom.js';
import { postForm } from '../../core/http.js';
import { navigate } from '../../core/navigation.js';

/**
 * [data-deployment-rollback] düğmeleri: onaydan sonra geri dönüş deployment'ı başlatılır ve canlı loguna geçilir.
 * Dockerfile projesinde o commit'in imajı sunucuda duruyorsa build yapılmaz; yoksa commit yeniden çekilip build edilir.
 */
export function bindRollback(root) {
    if (!root) return;
    on(root, 'click', '[data-deployment-rollback]', async (event, button) => {
        event.preventDefault();
        const { project, commit, url } = button.dataset;
        const response = await confirmAction({
            title: 'Bu sürüme geri dönülsün mü?',
            message: `${project} projesi ${commit} commit'ine geri döndürülecek. Dockerfile projesinde bu commit'in imajı sunucuda duruyorsa build yapılmadan çalıştırılır; `
                + 'aksi halde commit yeniden çekilip build edilir. Ortam değişkenleri ve domainler projenin güncel ayarlarından alınır.',
            confirmText: 'Geri dön',
            danger: false,
            action: () => postForm(url)
        });
        if (response?.data) navigate(response.data, response.message);
    });
}
