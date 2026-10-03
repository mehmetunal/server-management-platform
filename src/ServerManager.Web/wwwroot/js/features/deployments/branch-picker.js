import { qs } from '../../core/dom.js';

const MANUAL = '__manual__';

/**
 * Dal alanı: dallar yüklenene kadar metin kutusu, yüklendikten sonra seçim listesi.
 * Gönderilen değer her zaman metin kutusundadır; liste yalnızca onu doldurur. "Elle yaz" ile metin kutusuna dönülür.
 */
export function createBranchPicker(root) {
    const input = qs('[data-branch-input]', root);
    const select = qs('[data-branch-select]', root);

    function showInput() {
        select.hidden = true;
        input.hidden = false;
    }

    select.addEventListener('change', () => {
        if (select.value === MANUAL) {
            showInput();
            input.focus();
            return;
        }
        input.value = select.value;
    });

    return {
        get value() {
            return input.value.trim();
        },
        set value(branch) {
            input.value = branch;
        },
        setBranches(branches, preferred) {
            const current = input.value.trim();
            select.replaceChildren();
            if (current && !branches.includes(current)) select.append(new Option(`${current} (depoda bulunamadı)`, current));
            branches.forEach(branch => select.append(new Option(branch, branch)));
            select.append(new Option('Elle yaz…', MANUAL));

            const chosen = current || (branches.includes(preferred) ? preferred : branches[0]) || '';
            select.value = chosen;
            input.value = chosen;
            input.hidden = true;
            select.hidden = false;
        },
        reset: showInput
    };
}
