import { qs, setBusy } from '../core/dom.js';
import { confirmAction, showSecretList } from '../core/dialog.js';
import { clearErrors, showErrors } from '../core/forms.js';
import { failureMessage, getJson, postForm } from '../core/http.js';
import { navigate } from '../core/navigation.js';

const form = qs('[data-provision-form]');

if (form) {
    const account = qs('[data-account-select]', form);
    const region = qs('[data-region-select]', form);
    const size = qs('[data-size-select]', form);
    const image = qs('[data-image-select]', form);
    const priceHint = qs('[data-price-hint]', form);
    const catalogError = qs('[data-catalog-error]', form);
    const name = qs('[data-name-input]', form);
    let catalog = null;

    const money = (value, currency) =>
        value == null ? '' : new Intl.NumberFormat('tr-TR', { style: 'currency', currency }).format(value);

    function fill(select, options, placeholder) {
        select.replaceChildren(new Option(placeholder, ''));
        options.forEach(option => select.add(new Option(option.label, option.id)));
        select.disabled = options.length === 0;
    }

    function renderSizes() {
        const regionId = region.value;
        const sizes = (catalog?.sizes ?? []).filter(s => !regionId || !s.regions?.length || s.regions.includes(regionId));
        const current = size.value;
        fill(size, sizes.map(s => ({
            id: s.id,
            label: s.monthlyPrice != null ? `${s.name} — ${money(s.monthlyPrice, catalog.currency)}/ay` : s.name
        })), sizes.length ? 'Sunucu tipi seçin' : 'Bu bölgede uygun tip yok');
        if (sizes.some(s => s.id === current)) size.value = current;
        renderPrice();
    }

    function renderPrice() {
        const selected = catalog?.sizes.find(s => s.id === size.value);
        priceHint.textContent = selected?.monthlyPrice != null
            ? `Yaklaşık aylık ücret: ${money(selected.monthlyPrice, catalog.currency)} (vergiler hariç, sağlayıcı fiyatı).`
            : '';
    }

    async function loadCatalog() {
        catalog = null;
        catalogError.hidden = true;
        [region, size, image].forEach(select => {
            select.disabled = true;
            select.replaceChildren(new Option('Yükleniyor…', ''));
        });
        const response = await getJson(`${form.dataset.catalogUrl}/${encodeURIComponent(account.value)}`);
        if (!response.isSuccess) {
            catalogError.textContent = failureMessage(response, 'Sağlayıcı seçenekleri alınamadı.');
            catalogError.hidden = false;
            [region, size, image].forEach(select => fill(select, [], '—'));
            return;
        }
        catalog = response.data;
        fill(region, catalog.regions.map(r => ({ id: r.id, label: r.name })), 'Bölge seçin');
        fill(image, catalog.images.map(i => ({ id: i.id, label: i.description ? `${i.name} (${i.description})` : i.name })), 'İmaj seçin');
        renderSizes();
    }

    account.addEventListener('change', loadCatalog);
    region.addEventListener('change', renderSizes);
    size.addEventListener('change', renderPrice);

    form.addEventListener('submit', async event => {
        event.preventDefault();
        clearErrors(form);
        const serverName = name.value.trim();
        const missing = {};
        if (!serverName) missing.Name = ['Sunucu adı zorunludur.'];
        if (!region.value) missing.Region = ['Bölge seçin.'];
        if (!image.value) missing.Image = ['İşletim sistemi imajı seçin.'];
        if (!size.value) missing.Size = ['Sunucu tipi seçin.'];
        if (Object.keys(missing).length) {
            showErrors(form, { message: 'Lütfen formdaki hataları düzeltin.', errors: missing });
            return;
        }

        const submit = qs('[type="submit"]', form);
        setBusy(submit, true);
        try {
            let failed = null;
            const sizeLabel = size.selectedOptions[0]?.textContent ?? size.value;
            const response = await confirmAction({
                title: 'Sunucu oluşturulsun mu?',
                message: `${account.selectedOptions[0]?.textContent} hesabında ${region.selectedOptions[0]?.textContent} bölgesinde oluşturulacak ve ücretlendirme başlayacak: ${sizeLabel}.`,
                confirmText: 'Oluştur',
                expected: serverName,
                action: async () => {
                    const result = await postForm(form.action, new FormData(form));
                    if (!result.isSuccess && Object.keys(result.errors ?? {}).length) failed = result;
                    return failed ? { isSuccess: true } : result;
                }
            });

            if (failed) {
                showErrors(form, failed);
                return;
            }
            if (!response) return;

            if (response.data.rootPassword) {
                await showSecretList({
                    title: 'Tek seferlik root parolası',
                    message: 'Sağlayıcı bu parolayı yalnızca şimdi gösterir; panelde saklanmaz. Güvenli bir yere kaydedin ve ilk girişte değiştirin.',
                    items: [response.data.rootPassword]
                });
            }
            navigate(response.data.redirectUrl, response.message);
        } finally {
            setBusy(submit, false);
        }
    });

    loadCatalog();
}
