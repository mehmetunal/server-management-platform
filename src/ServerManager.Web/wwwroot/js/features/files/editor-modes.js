const PLAIN_TEXT = { mime: 'text/plain', label: 'Düz metin' };

/** Ada göre tanınamayan veya CodeMirror'ın meta listesinde farklı eşlenen yaygın sunucu dosyaları. */
const SPECIAL_FILES = [
    { test: (name) => /^dockerfile([.-].*)?$/i.test(name) || /\.dockerfile$/i.test(name), mime: 'text/x-dockerfile', label: 'Dockerfile' },
    { test: (name) => /^\.env(\..*)?$/i.test(name) || /\.env$/i.test(name), mime: 'text/x-properties', label: 'ENV' },
    { test: (name, path) => /^nginx\.conf$/i.test(name) || /\.nginx$/i.test(name) || (/\/nginx\//i.test(path) && (/\.conf$/i.test(name) || !name.includes('.'))), mime: 'text/x-nginx-conf', label: 'Nginx' },
    { test: (name) => /\.(md|markdown|mkd)$/i.test(name), mime: 'text/x-markdown', label: 'Markdown' },
    { test: (name) => /\.(cs|csx)$/i.test(name), mime: 'text/x-csharp', label: 'C#' },
    { test: (name) => /\.(ts|tsx|mts|cts)$/i.test(name), mime: 'text/typescript', label: 'TypeScript' },
    { test: (name) => /\.(json|jsonc)$/i.test(name) || /^\.(babelrc|eslintrc|prettierrc)$/i.test(name), mime: 'application/json', label: 'JSON' },
    { test: (name) => /\.(ya?ml)$/i.test(name), mime: 'text/x-yaml', label: 'YAML' },
    { test: (name) => /\.(sh|bash|zsh|ksh)$/i.test(name) || /^\.(bashrc|bash_profile|profile|zshrc|bash_aliases)$/i.test(name), mime: 'text/x-sh', label: 'Shell' },
    { test: (name) => /\.(csproj|props|targets|config|xml|svg)$/i.test(name), mime: 'application/xml', label: 'XML' },
    { test: (name) => /\.(conf|cnf|cfg|ini)$/i.test(name), mime: 'text/x-properties', label: 'Yapılandırma' }
];

/** Mod seçicisinde listelenen modlar. */
export const SELECTABLE_MODES = [
    PLAIN_TEXT,
    { mime: 'text/x-csharp', label: 'C#' },
    { mime: 'text/css', label: 'CSS' },
    { mime: 'text/x-dockerfile', label: 'Dockerfile' },
    { mime: 'text/x-properties', label: 'ENV / INI' },
    { mime: 'text/x-go', label: 'Go' },
    { mime: 'text/html', label: 'HTML' },
    { mime: 'text/javascript', label: 'JavaScript' },
    { mime: 'application/json', label: 'JSON' },
    { mime: 'text/x-markdown', label: 'Markdown' },
    { mime: 'text/x-nginx-conf', label: 'Nginx' },
    { mime: 'application/x-httpd-php', label: 'PHP' },
    { mime: 'text/x-python', label: 'Python' },
    { mime: 'text/x-sh', label: 'Shell' },
    { mime: 'text/x-sql', label: 'SQL' },
    { mime: 'text/x-toml', label: 'TOML' },
    { mime: 'text/typescript', label: 'TypeScript' },
    { mime: 'application/xml', label: 'XML' },
    { mime: 'text/x-yaml', label: 'YAML' },
    { mime: 'text/x-diff', label: 'Diff' }
];

export function resolveMode(path) {
    const name = path.split('/').pop() ?? '';
    const special = SPECIAL_FILES.find(entry => entry.test(name, path));
    if (special) return { mime: special.mime, label: special.label };

    const info = window.CodeMirror?.findModeByFileName?.(name);
    if (info?.mime && window.CodeMirror.modes[info.mode]) return { mime: info.mime, label: info.name };
    return PLAIN_TEXT;
}
