import { qs, qsa, setBusy } from '../../core/dom.js';
import { failureMessage, getJson, postForm } from '../../core/http.js';
import { createBranchPicker } from './branch-picker.js';

const MANUAL_LABEL = 'Depo adresiyle (elle)';

function setHint(hint, text, tone = '') {
    if (!hint) return;
    hint.textContent = text;
    if (tone) hint.dataset.tone = tone;
    else delete hint.dataset.tone;
}

function withQuery(url, params) {
    const query = new URLSearchParams(params);
    return `${url}${url.includes('?') ? '&' : '?'}${query}`;
}

/**
 * Proje formundaki Git kaynağı: depo adresi (elle) veya etkin bir Git entegrasyonunun bağlantısı (GitHub App vb.).
 * Bağlantı seçilince depolar, depo seçilince dallar listelenir; elle girilen adreste dallar hedef sunucudan okunur.
 */
export function initGitSource(form) {
    const panel = qs('[data-git-panel]', form);
    if (!panel) return;

    const sourceSelect = qs('[data-git-source]', panel);
    const sourceHint = qs('[data-git-source-hint]', panel);
    const repositorySelect = qs('[data-git-repository]', panel);
    const repositoryHint = qs('[data-git-repository-hint]', panel);
    const branchHint = qs('[data-branch-hint]', panel);
    const loadButton = qs('[data-branch-load]', panel);
    const picker = createBranchPicker(panel);
    const defaultSourceHint = sourceHint?.textContent ?? '';
    let repositories = [];
    let pendingRepository = panel.dataset.currentRepository || '';
    let lastDefaultBranch = '';

    const usesIntegration = () => Boolean(sourceSelect?.value);

    function applyMode() {
        const mode = usesIntegration() ? 'integration' : 'manual';
        qsa('[data-git-mode]', panel).forEach(field => {
            field.hidden = field.dataset.gitMode !== mode;
        });
    }

    async function loadSources() {
        const response = await getJson(panel.dataset.sourcesUrl);
        if (!response.isSuccess) {
            setHint(sourceHint, failureMessage(response, 'Git bağlantıları okunamadı.'), 'error');
            return;
        }

        const { sources, warnings } = response.data;
        const selected = sourceSelect.value;
        sourceSelect.replaceChildren(new Option(MANUAL_LABEL, ''));
        const groups = new Map();
        sources.forEach(source => {
            if (!groups.has(source.integrationName)) {
                const group = document.createElement('optgroup');
                group.label = source.integrationName;
                groups.set(source.integrationName, group);
                sourceSelect.append(group);
            }
            groups.get(source.integrationName).append(new Option(source.name, source.key));
        });
        if (selected && !sources.some(source => source.key === selected)) {
            sourceSelect.append(new Option('Kayıtlı bağlantı (şu an erişilemiyor)', selected));
        }
        sourceSelect.value = selected;

        if (warnings.length) setHint(sourceHint, warnings.join(' '), 'warning');
        else if (!sources.length) setHint(sourceHint, 'Henüz bağlantı yok. Menüdeki GitHub sayfasından bir GitHub App oluşturup hesabınıza veya kurumunuza kurun.', 'warning');
        else setHint(sourceHint, defaultSourceHint);
    }

    async function loadRepositories() {
        repositories = [];
        repositorySelect.replaceChildren(new Option('Depolar yükleniyor…', ''));
        repositorySelect.disabled = true;
        const response = await getJson(withQuery(panel.dataset.repositoriesUrl, { source: sourceSelect.value }));
        repositorySelect.disabled = false;
        repositorySelect.replaceChildren(new Option('Depo seçin', ''));
        if (!response.isSuccess) {
            setHint(repositoryHint, failureMessage(response, 'Depolar listelenemedi.'), 'error');
            if (pendingRepository) repositorySelect.append(new Option(pendingRepository, pendingRepository));
            repositorySelect.value = pendingRepository;
            return;
        }

        repositories = response.data;
        repositories.forEach(repository => {
            repositorySelect.append(new Option(`${repository.fullName}${repository.isPrivate ? ' (özel)' : ''}`, repository.fullName));
        });
        if (pendingRepository && !repositories.some(repository => repository.fullName === pendingRepository)) {
            repositorySelect.append(new Option(`${pendingRepository} (bağlantıda bulunamadı)`, pendingRepository));
        }
        repositorySelect.value = pendingRepository;
        setHint(repositoryHint, repositories.length
            ? `${repositories.length} depo bulundu.`
            : 'Bu bağlantı hiçbir depoya erişemiyor; GitHub\'da kurulumun depo erişimini düzenleyin.', repositories.length ? '' : 'warning');

        if (repositorySelect.value) await selectRepository({ keepBranch: true });
    }

    async function selectRepository({ keepBranch }) {
        pendingRepository = repositorySelect.value;
        const repository = repositories.find(item => item.fullName === pendingRepository);
        if (!repository) return;

        if (!keepBranch && (!picker.value || picker.value === lastDefaultBranch)) picker.value = repository.defaultBranch;
        lastDefaultBranch = repository.defaultBranch;
        await loadBranches();
    }

    function remoteBranchQuery() {
        const value = name => form.elements[name]?.value ?? '';
        return {
            ServerId: value('ServerId'),
            ProjectId: panel.dataset.projectId || '',
            GitProvider: value('GitProvider'),
            RepositoryUrl: value('RepositoryUrl'),
            GitUsername: value('GitUsername'),
            AccessToken: value('AccessToken'),
            RemoveAccessToken: form.elements.RemoveAccessToken?.checked ? 'true' : 'false'
        };
    }

    async function loadBranches() {
        let request;
        if (usesIntegration()) {
            if (!repositorySelect.value) {
                setHint(branchHint, 'Önce bir depo seçin.', 'warning');
                return;
            }
            request = getJson(withQuery(panel.dataset.branchesUrl, { source: sourceSelect.value, repository: repositorySelect.value }));
        } else {
            const query = remoteBranchQuery();
            if (!query.ServerId) {
                setHint(branchHint, 'Dallar hedef sunucudan okunur; önce sunucuyu seçin.', 'warning');
                return;
            }
            if (!query.RepositoryUrl.trim()) {
                setHint(branchHint, 'Önce depo adresini girin.', 'warning');
                return;
            }
            request = postForm(panel.dataset.remoteBranchesUrl, query);
        }

        setBusy(loadButton, true);
        setHint(branchHint, usesIntegration() ? 'Dallar okunuyor…' : 'Dallar hedef sunucudan okunuyor…');
        const response = await request;
        setBusy(loadButton, false);
        if (!response.isSuccess) {
            setHint(branchHint, failureMessage(response, 'Dallar listelenemedi.'), 'error');
            return;
        }

        const preferred = repositories.find(item => item.fullName === repositorySelect?.value)?.defaultBranch;
        picker.setBranches(response.data, preferred);
        setHint(branchHint, response.data.length ? `${response.data.length} dal bulundu.` : 'Depoda dal yok.', response.data.length ? '' : 'warning');
    }

    sourceSelect?.addEventListener('change', () => {
        applyMode();
        picker.reset();
        pendingRepository = '';
        lastDefaultBranch = '';
        setHint(branchHint, 'Dalları listelemek için yenile düğmesine basın.');
        if (usesIntegration()) loadRepositories();
    });
    repositorySelect?.addEventListener('change', () => selectRepository({ keepBranch: false }));
    loadButton.addEventListener('click', loadBranches);

    applyMode();
    if (sourceSelect) {
        loadSources().then(() => {
            if (usesIntegration()) loadRepositories();
        });
    }
}
