import { element, on, qs, setBusy } from '../../core/dom.js';
import { bindAjaxForm } from '../../core/forms.js';
import { failureMessage, getJson } from '../../core/http.js';
import { navigate } from '../../core/navigation.js';
import { notify } from '../../core/notify.js';

/**
 * Servis sayfasındaki "Projeye bağla": aday projeler ve önerilen değişkenler (parolası gizli önizleme) sunucudan okunur;
 * kullanıcı anahtar adlarını düzenleyip seçer. Değerler sunucuda servisin kayıtlı bilgisinden yazılır.
 */
export function initLinkProject(section) {
    if (!section) return;
    const form = qs('[data-link-form]', section);
    const openButton = qs('[data-link-open]', section);
    if (!form || !openButton) return;

    const projectSelect = qs('[data-link-project]', form);
    const tbody = qs('[data-link-variables]', form);
    const conflictHint = qs('[data-link-conflict-hint]', form);
    let projects = [];

    const existingKeys = () => new Set(projects.find(p => p.id === projectSelect.value)?.existingKeys ?? []);

    const markConflicts = () => {
        const keys = existingKeys();
        let conflict = false;
        tbody.querySelectorAll('tr').forEach(row => {
            const key = qs('[data-link-key]', row).value.trim();
            const included = qs('[data-link-include]', row).checked;
            const exists = included && keys.has(key);
            qs('[data-link-exists]', row).hidden = !exists;
            conflict ||= exists;
        });
        conflictHint.hidden = !conflict;
    };

    const render = data => {
        projects = data.projects ?? [];
        projectSelect.replaceChildren(element('option', null, 'Proje seçin'));
        projectSelect.firstChild.value = '';
        projects.forEach(project => {
            const option = element('option', null, project.isLinked ? `${project.name} (zaten bağlı)` : project.name);
            option.value = project.id;
            option.disabled = project.isLinked;
            projectSelect.appendChild(option);
        });

        tbody.replaceChildren();
        (data.variables ?? []).forEach((variable, index) => {
            const row = element('tr');
            const includeCell = element('td');
            const include = element('input', 'form-check');
            include.type = 'checkbox';
            include.name = `Variables[${index}].Include`;
            include.value = 'true';
            include.checked = true;
            include.dataset.linkInclude = '';
            include.setAttribute('aria-label', `${variable.key} eklensin`);
            const source = element('input');
            source.type = 'hidden';
            source.name = `Variables[${index}].SourceKey`;
            source.value = variable.key;
            includeCell.append(include, source);

            const keyCell = element('td');
            const key = element('input', 'form-control form-control-mono');
            key.name = `Variables[${index}].Key`;
            key.value = variable.key;
            key.maxLength = 200;
            key.spellcheck = false;
            key.dataset.linkKey = '';
            key.setAttribute('aria-label', 'Anahtar adı');
            const exists = element('span', 'badge-warning mt-1', 'Projede var');
            exists.dataset.linkExists = '';
            exists.hidden = true;
            keyCell.append(key, exists);

            const valueCell = element('td', 'font-mono text-xs break-all', variable.preview);
            row.append(includeCell, keyCell, valueCell);
            tbody.appendChild(row);
        });
        if (!data.variables?.length) {
            const row = element('tr');
            const cell = element('td', 'text-sm text-slate-500', 'Bu servis için önerilen değişken yok; yalnızca ağ bağlantısı kurulur.');
            cell.colSpan = 3;
            row.appendChild(cell);
            tbody.appendChild(row);
        }
        markConflicts();
    };

    on(section, 'click', '[data-link-open]', async (event, button) => {
        setBusy(button, true);
        try {
            const response = await getJson(button.dataset.url);
            if (!response.isSuccess || !response.data) {
                notify.error(failureMessage(response, 'Bağlantı bilgisi alınamadı.'));
                return;
            }
            render(response.data);
            if (!projects.some(p => !p.isLinked)) notify.info('Bu sunucuda bağlanabilecek (Compose veya Dockerfile) proje yok.');
            form.hidden = false;
            projectSelect.focus();
        } finally {
            setBusy(button, false);
        }
    });

    on(form, 'click', '[data-link-cancel]', () => { form.hidden = true; });
    projectSelect.addEventListener('change', markConflicts);
    on(form, 'input', '[data-link-key]', markConflicts);
    on(form, 'change', '[data-link-include]', markConflicts);

    bindAjaxForm(form, {
        onSuccess: response => navigate(response.data || window.location.href, response.message || 'Servis projeye bağlandı.')
    });
}
