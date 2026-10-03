import { qs, qsa, setBusy } from '../../core/dom.js';
import { clearErrors, showErrors } from '../../core/forms.js';
import { postForm } from '../../core/http.js';
import { notify } from '../../core/notify.js';

const toOctal = mode => (mode & 0o7777).toString(8).padStart(4, '0');

/**
 * İzin/sahiplik formu: rwx tablosu sekizli modu üretir; gelişmiş alandaki ham değer doluysa onun yerine gönderilir.
 * Yalnızca değişen değerler (mod, sahip, grup) gönderilir; böylece gereksiz chown çalıştırılmaz.
 */
export function createPermissionsForm(form, { onSuccess } = {}) {
    if (!form) return null;

    const bits = qsa('[data-mode-bit]', form);
    const preview = qs('[data-mode-preview]', form);
    const advanced = qs('[data-permission-advanced]', form);
    const recursiveRow = qs('[data-permission-recursive-row]', form);
    const fields = form.elements;
    let original = { mode: 0, owner: '', group: '' };

    const currentMode = () => bits.reduce((mode, bit) => (bit.checked ? mode | Number(bit.dataset.modeBit) : mode), 0);
    const renderPreview = () => {
        preview.textContent = toOctal(currentMode());
    };

    bits.forEach(bit => bit.addEventListener('change', renderPreview));

    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (form.dataset.submitting === 'true') return;
        clearErrors(form);

        const raw = fields.Mode.value.trim();
        const mode = currentMode();
        const owner = fields.Owner.value.trim();
        const group = fields.Group.value.trim();
        const payload = {
            Path: fields.Path.value,
            Mode: raw || (mode !== original.mode ? toOctal(mode) : ''),
            Owner: owner !== original.owner ? owner : '',
            Group: group !== original.group ? group : '',
            Recursive: fields.Recursive.checked ? 'true' : 'false'
        };

        if (!payload.Mode && !payload.Owner && !payload.Group) {
            if (!fields.Recursive.checked) {
                notify.info('Değişiklik yapılmadı.');
                return;
            }
            payload.Mode = toOctal(mode);
        }

        const submit = qs('[type="submit"]', form);
        form.dataset.submitting = 'true';
        setBusy(submit, true);
        try {
            const response = await postForm(form.action, payload);
            if (!response.isSuccess) {
                if (response.errors?.Mode?.length) advanced.open = true;
                showErrors(form, response);
                return;
            }
            await onSuccess?.(response);
        } finally {
            setBusy(submit, false);
            delete form.dataset.submitting;
        }
    });

    return {
        /** entry: { path, mode (sekizli metin), owner, group, isDirectory } */
        load(entry) {
            clearErrors(form);
            const mode = parseInt(entry.mode || '0', 8) || 0;
            original = { mode, owner: entry.owner ?? '', group: entry.group ?? '' };
            fields.Path.value = entry.path;
            fields.Owner.value = original.owner;
            fields.Group.value = original.group;
            fields.Mode.value = '';
            fields.Recursive.checked = false;
            recursiveRow.hidden = !entry.isDirectory;
            advanced.open = false;
            bits.forEach(bit => {
                bit.checked = (mode & Number(bit.dataset.modeBit)) !== 0;
            });
            renderPreview();
        }
    };
}
