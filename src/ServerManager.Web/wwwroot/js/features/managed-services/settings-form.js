import { element, on, qs, qsa, setBusy } from '../../core/dom.js';
import { failureMessage, getJson } from '../../core/http.js';
import { notify } from '../../core/notify.js';

/** Ortam değişkeni satırlarının adlarını sıraya göre yeniden numaralar (Environment[0].Key …). */
function renumber(list) {
    qsa('[data-env-row]', list).forEach((row, index) => {
        const inputs = qsa('input', row);
        if (inputs[0]) inputs[0].name = `Environment[${index}].Key`;
        if (inputs[1]) inputs[1].name = `Environment[${index}].Value`;
        const errors = qsa('.service-env-error', row);
        if (errors[0]) errors[0].dataset.valmsgFor = `Environment[${index}].Key`;
        if (errors[1]) errors[1].dataset.valmsgFor = `Environment[${index}].Value`;
    });
}

function syncPorts(form) {
    qsa('[data-port-row]', form).forEach(row => {
        const publish = qs('[data-port-publish]', row);
        const host = qs('[data-port-host]', row);
        if (publish && host) host.disabled = !publish.checked;
    });
}

function syncExpose(form) {
    const toggle = qs('[data-expose-toggle]', form);
    const panel = qs('[data-expose-panel]', form);
    if (toggle && panel) panel.hidden = !toggle.checked;
}

/**
 * Kurulum formu ve Ayarlar sekmesindeki ortak alanlar: port yayını, "Dışarıya aç" paneli, ek ortam değişkeni editörü
 * ve sunucudaki Docker ağlarının listelenmesi. networksUrl(): ağ listesinin adresi (sunucu seçimine göre değişebilir).
 */
export function initSettingsFields(form, { networksUrl } = {}) {
    if (!form) return;
    const list = qs('[data-env-list]', form);
    const template = qs('template[data-env-template]', form);

    syncPorts(form);
    syncExpose(form);
    form.addEventListener('change', event => {
        if (event.target.matches('[data-port-publish]')) syncPorts(form);
        if (event.target.matches('[data-expose-toggle]')) syncExpose(form);
    });

    on(form, 'click', '[data-env-add]', () => {
        const row = template.content.firstElementChild.cloneNode(true);
        list.appendChild(row);
        renumber(list);
        qs('input', row)?.focus();
    });

    on(form, 'click', '[data-env-remove]', (event, button) => {
        const row = button.closest('[data-env-row]');
        if (qsa('[data-env-row]', list).length > 1) row.remove();
        else qsa('input', row).forEach(input => { input.value = ''; });
        renumber(list);
    });

    on(form, 'click', '[data-networks-load]', async (event, button) => {
        const url = networksUrl?.() ?? button.dataset.url;
        if (!url) {
            notify.warning('Önce sunucu seçin.');
            return;
        }
        setBusy(button, true);
        try {
            const response = await getJson(url);
            if (!response.isSuccess) {
                notify.error(failureMessage(response, 'Docker ağları alınamadı.'));
                return;
            }
            const chips = qs('[data-networks-list]', form);
            const input = qs('[data-networks-input]', form);
            const skip = ['bridge', 'host', 'none', 'sm-services', 'sm-proxy'];
            const names = (response.data ?? []).filter(name => !skip.includes(name));
            chips.replaceChildren();
            if (!names.length) chips.appendChild(element('span', 'form-hint', 'Seçilebilecek başka ağ yok.'));
            names.forEach(name => {
                const chip = element('button', 'service-chip', name);
                chip.type = 'button';
                chip.title = 'Listeye ekle';
                chip.addEventListener('click', () => {
                    const current = input.value.split(/[\s,]+/).filter(Boolean);
                    if (!current.includes(name)) current.push(name);
                    input.value = current.join(', ');
                });
                chips.appendChild(chip);
            });
            chips.hidden = false;
        } finally {
            setBusy(button, false);
        }
    });

    // Gönderimden önce boş satırlar dahil adlar sıralı olmalı; devre dışı port alanları gönderilmez.
    form.addEventListener('submit', () => renumber(list), true);
}
