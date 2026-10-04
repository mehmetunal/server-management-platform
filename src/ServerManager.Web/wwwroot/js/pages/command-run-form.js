import { qs, qsa, setBusy } from '../core/dom.js';
import { confirmAction } from '../core/dialog.js';
import { clearErrors, showErrors } from '../core/forms.js';
import { postForm } from '../core/http.js';
import { navigate } from '../core/navigation.js';

const BULK_CONFIRM_THRESHOLD = 5;

const form = qs('[data-command-form]');
const command = qs('[data-command-input]', form);
const sudo = qs('[data-sudo-input]', form);
const template = qs('[data-template-select]', form);
const search = qs('[data-server-search]', form);
const counter = qs('[data-selected-count]', form);

const checks = () => qsa('[data-server-check]', form);
const selected = () => checks().filter(check => check.checked);

function updateCount() {
    counter.textContent = String(selected().length);
}

template?.addEventListener('change', () => {
    const option = template.selectedOptions[0];
    if (!option?.value) return;
    command.value = option.dataset.content ?? '';
    sudo.checked = option.dataset.sudo === 'true';
});

search?.addEventListener('input', () => {
    const term = search.value.trim().toLowerCase();
    qsa('[data-server-option]', form).forEach(option => {
        option.hidden = term.length > 0 && !option.dataset.search.includes(term);
    });
});

form.addEventListener('change', event => {
    if (event.target.matches('[data-server-check]')) updateCount();
});

form.addEventListener('click', event => {
    const button = event.target.closest('[data-select-all], [data-select-none], [data-select-group]');
    if (!button) return;

    if (button.matches('[data-select-all]')) {
        qsa('[data-server-option]:not([hidden]) [data-server-check]', form).forEach(check => { check.checked = true; });
    } else if (button.matches('[data-select-none]')) {
        checks().forEach(check => { check.checked = false; });
    } else {
        const groupId = button.dataset.selectGroup;
        qsa(`[data-server-option][data-group="${CSS.escape(groupId)}"] [data-server-check]`, form).forEach(check => { check.checked = true; });
    }
    updateCount();
});

function serverNames() {
    return selected().map(check => check.closest('[data-server-option]').querySelector('.check-card-title').textContent.trim());
}

form.addEventListener('submit', async event => {
    event.preventDefault();
    clearErrors(form);

    const count = selected().length;
    if (!command.value.trim()) {
        showErrors(form, { message: 'Komut yazın.', errors: { Command: ['Çalıştırılacak komutu yazın.'] } });
        return;
    }
    if (count === 0) {
        showErrors(form, { message: 'Sunucu seçin.', errors: { ServerIds: ['En az bir sunucu seçin.'] } });
        return;
    }

    const names = serverNames();
    const preview = names.slice(0, 8).join(', ') + (names.length > 8 ? ` ve ${names.length - 8} sunucu daha` : '');
    const submit = qs('[type="submit"]', form);
    setBusy(submit, true);
    try {
        let failed = null;
        const response = await confirmAction({
            title: `Komut ${count} sunucuda çalıştırılsın mı?`,
            message: `Sunucular: ${preview}.${sudo.checked ? ' Komut sudo ile çalışacak.' : ''}`,
            code: command.value.length > 600 ? `${command.value.slice(0, 600)}…` : command.value,
            confirmText: 'Çalıştır',
            danger: sudo.checked || count >= BULK_CONFIRM_THRESHOLD,
            expected: count >= BULK_CONFIRM_THRESHOLD ? String(count) : undefined,
            action: async () => {
                const result = await postForm(form.action, new FormData(form));
                if (!result.isSuccess && Object.keys(result.errors ?? {}).length) failed = result;
                return failed ? { isSuccess: true, failed: true } : result;
            }
        });

        if (failed) {
            showErrors(form, failed);
            return;
        }
        if (response) navigate(response.data, response.message);
    } finally {
        setBusy(submit, false);
    }
});

updateCount();
