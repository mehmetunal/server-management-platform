import { on, qs, qsa } from '../../core/dom.js';

/**
 * "Otomatik yedek" alanları (_BackupFields): açma kutusu, günlük / saat aralıklı zamanlama.
 * Kapalı bölümdeki alanlar devre dışı bırakılır; böylece forma gönderilmez.
 */
export function initBackupFields(root) {
    const fields = qs('[data-backup-fields]', root);
    if (!fields) return;

    const toggle = qs('[data-backup-toggle]', fields);
    const body = qs('[data-backup-body]', fields);

    const setDisabled = (container, disabled) => {
        qsa('input, select, textarea', container).forEach(input => { input.disabled = disabled; });
    };

    const sync = () => {
        const enabled = !toggle || toggle.checked;
        body.hidden = !enabled;
        setDisabled(body, !enabled);
        if (!enabled) return;

        const hourly = qsa('[data-backup-schedule]', fields).find(input => input.checked)?.value === 'Hourly';
        const daily = qs('[data-backup-daily]', fields);
        const interval = qs('[data-backup-hourly]', fields);
        daily.hidden = hourly;
        interval.hidden = !hourly;
        setDisabled(daily, hourly);
        setDisabled(interval, !hourly);
    };

    toggle?.addEventListener('change', sync);
    on(fields, 'change', '[data-backup-schedule]', sync);
    sync();
}
