import { on, qs, qsa } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';
import { initBackupFields } from '../features/managed-services/backup-fields.js';
import { bindPasswordField } from '../features/managed-services/password.js';
import { loadProbe } from '../features/managed-services/probe.js';
import { initSettingsFields } from '../features/managed-services/settings-form.js';

const form = qs('[data-service-form]');

if (form) {
    const serverSelect = qs('[data-server-select]', form);
    const probeSlot = qs('[data-service-probe-slot]', form);
    const selectedServer = () => serverSelect?.value || '';

    initSettingsFields(form, {
        networksUrl: () => (selectedServer() ? `${form.dataset.networksUrl}?serverId=${encodeURIComponent(selectedServer())}` : null)
    });
    bindPasswordField(form);
    initBackupFields(form);

    const syncVolume = () => {
        const hostPath = qs('[data-host-path]', form);
        const selected = qsa('[data-volume-mode]', form).find(input => input.checked);
        if (hostPath) hostPath.hidden = selected?.value !== 'HostPath';
    };
    on(form, 'change', '[data-volume-mode]', syncVolume);
    syncVolume();

    const probe = () => {
        const serverId = selectedServer();
        if (!serverId) {
            probeSlot.replaceChildren();
            return;
        }
        loadProbe(probeSlot, {
            url: `${form.dataset.probeUrl}?serverId=${encodeURIComponent(serverId)}`,
            dockerUrl: form.dataset.dockerUrl.replace('__id__', encodeURIComponent(serverId)),
            requiresX86: form.dataset.requiresX86 === 'true'
        });
    };
    serverSelect?.addEventListener('change', probe);
    probe();

    bindAjaxForm(form, {
        onSuccess: response => navigate(response.data, response.message || 'Kurulum başlatıldı.')
    });
}
