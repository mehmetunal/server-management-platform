import { bindAjaxActions } from '../components/ajax-actions.js';
import { createTabs } from '../components/tabs.js';
import { confirmAction } from '../core/dialog.js';
import { on, qs, setBusy } from '../core/dom.js';
import { bindAjaxForm, clearErrors, showErrors } from '../core/forms.js';
import { formatTimestamp } from '../core/format.js';
import { failureMessage, getJson, postForm } from '../core/http.js';
import { navigate } from '../core/navigation.js';
import { notify } from '../core/notify.js';
import { createLogViewer } from '../features/docker/logs.js';
import { createTerminal } from '../features/docker/terminal.js';
import { copyText } from '../features/managed-services/password.js';
import { initBackupFields } from '../features/managed-services/backup-fields.js';
import { initLinkProject } from '../features/managed-services/link-project.js';
import { initSettingsFields } from '../features/managed-services/settings-form.js';

const STATE_BADGES = {
    running: ['badge-success', 'Çalışıyor'],
    restarting: ['badge-warning', 'Yeniden başlıyor'],
    paused: ['badge-warning', 'Duraklatıldı'],
    created: ['badge-neutral', 'Oluşturuldu'],
    exited: ['badge-danger', 'Durdu'],
    dead: ['badge-danger', 'Ölü'],
    missing: ['badge-danger', 'Container yok']
};
const HEALTH_TEXT = { healthy: 'Sağlıklı', unhealthy: 'Sağlıksız', starting: 'Başlıyor' };

const logs = createLogViewer(qs('[data-logs]'));
const terminal = createTerminal(qs('[data-terminal]'));

bindAjaxActions(document, { onSuccess: () => loadRuntime() });

createTabs({
    defaultTab: 'general',
    activators: {
        logs: () => logs?.activate(),
        console: () => terminal?.activate()
    }
});

// ---- Durum

const runtime = qs('[data-service-runtime]');

async function loadRuntime() {
    if (!runtime) return;
    const response = await getJson(runtime.dataset.url);
    const error = qs('[data-runtime-error]', runtime);
    if (!response.isSuccess) {
        error.textContent = failureMessage(response, 'Container durumu alınamadı.');
        error.hidden = false;
        return;
    }
    error.hidden = true;

    const data = response.data;
    const key = data.exists ? data.state : 'missing';
    const [className, text] = STATE_BADGES[key] ?? ['badge-neutral', key ?? '—'];
    const badge = qs('[data-runtime-state]', runtime);
    badge.className = className;
    qs('[data-runtime-state-text]', badge).textContent = text;
    qs('[data-runtime-health]', runtime).textContent = data.health ? (HEALTH_TEXT[data.health] ?? data.health) : 'Kontrol yok';
    qs('[data-runtime-started]', runtime).textContent = data.startedAt ? formatTimestamp(data.startedAt) : '—';
    qs('[data-runtime-restarts]', runtime).textContent = String(data.restartCount ?? 0);
    if (data.networks?.length) qs('[data-runtime-networks]', runtime).textContent = data.networks.join(', ');

    const warning = qs('[data-firewall-warning]', runtime);
    if (warning) warning.hidden = !(runtime.dataset.expectsFirewall === 'true' && data.exists && data.firewallRuleCount === 0);
}

on(document, 'click', '[data-runtime-refresh]', () => loadRuntime());
loadRuntime();

// ---- Kopyalama ve parola gösterme

on(document, 'click', '[data-copy]', (event, button) => copyText(button.dataset.copy));
on(document, 'click', '[data-copy-target]', (event, button) => {
    const target = qs(button.dataset.copyTarget);
    if (target?.textContent) copyText(target.textContent.trim());
});

on(document, 'click', '[data-reveal]', async (event, button) => {
    const response = await confirmAction({
        title: 'Parola gösterilsin mi?',
        message: 'Parola ve parolalı bağlantı adresleri ekranda gösterilecek. Bu işlem audit log\'a kullanıcı adınızla yazılır.',
        confirmText: 'Göster',
        danger: false,
        action: () => postForm(button.dataset.url)
    });
    if (!response?.data) return;

    const secrets = response.data;
    const set = (selector, value) => {
        const target = qs(selector);
        if (target && value) target.textContent = value;
    };
    set('[data-secret-password]', secrets.password ?? secrets.encryptionKey);
    set('[data-secret-internal]', secrets.internalConnectionString);
    set('[data-secret-external]', secrets.externalConnectionString);

    const env = Object.entries(secrets.suggestedEnvironment ?? {});
    const panel = qs('[data-secret-panel]');
    if (panel && env.length) {
        qs('[data-secret-env]', panel).textContent = env.map(([key, value]) => `${key}=${value}`).join('\n');
        panel.hidden = false;
    }
    button.hidden = true;
    notify.info(response.message || 'Kimlik bilgileri gösterildi.');
});

// ---- Ayarlar

const settingsForm = qs('[data-service-settings]');
if (settingsForm) {
    initSettingsFields(settingsForm);
    bindAjaxForm(settingsForm, {
        onSuccess: response => navigate(response.data, response.message || 'Servis yeniden oluşturuluyor.')
    });
}

// ---- Sürüm

const upgradeForm = qs('[data-service-upgrade]');
if (upgradeForm) {
    const confirmField = qs('[data-confirm-major]', upgradeForm);
    const submit = qs('[type="submit"]', upgradeForm);

    upgradeForm.addEventListener('submit', async event => {
        event.preventDefault();
        if (upgradeForm.dataset.submitting === 'true') return;
        clearErrors(upgradeForm);
        upgradeForm.dataset.submitting = 'true';
        setBusy(submit, true);
        try {
            confirmField.value = 'false';
            let response = await postForm(upgradeForm.action, new FormData(upgradeForm));
            const warning = response.errors?.ConfirmMajorUpgrade?.join(' ');
            if (!response.isSuccess && warning) {
                const confirmed = await confirmAction({
                    title: 'Ana sürüm değişiyor',
                    message: warning,
                    confirmText: 'Yine de yükselt',
                    action: () => {
                        confirmField.value = 'true';
                        return postForm(upgradeForm.action, new FormData(upgradeForm));
                    }
                });
                if (!confirmed) return;
                response = confirmed;
            }
            if (!response.isSuccess) {
                showErrors(upgradeForm, response);
                return;
            }
            navigate(response.data, response.message || 'Sürüm yükseltme başlatıldı.');
        } finally {
            setBusy(submit, false);
            delete upgradeForm.dataset.submitting;
        }
    });
}

// ---- Projeye bağla ve yedekleme

initLinkProject(qs('[data-service-links]'));

const backupSection = qs('[data-service-backups]');
const backupForm = qs('[data-backup-form]', backupSection ?? document);
if (backupSection && backupForm) {
    initBackupFields(backupForm);
    on(backupSection, 'click', '[data-backup-open]', () => {
        backupForm.hidden = false;
        qs('select', backupForm)?.focus();
    });
    on(backupSection, 'click', '[data-backup-cancel]', () => { backupForm.hidden = true; });
    bindAjaxForm(backupForm, {
        onSuccess: response => navigate(window.location.href, response.message || 'Yedekleme işi oluşturuldu.')
    });
}
