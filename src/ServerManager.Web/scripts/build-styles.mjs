import { mkdir, readdir, writeFile } from 'node:fs/promises';
import { watch } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as sass from 'sass';
import postcss from 'postcss';
import tailwindcss from '@tailwindcss/postcss';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const stylesDir = path.join(root, 'Styles');
const pagesDir = path.join(stylesDir, 'pages');
const outputDir = path.join(root, 'wwwroot', 'css');
const referenceFile = path.join(root, 'obj', 'styles', 'site.reference.css');
const watchMode = process.argv.includes('--watch');

const sassOptions = {
    style: 'expanded',
    loadPaths: [stylesDir, path.join(root, 'node_modules')]
};

function compileSass(file) {
    return sass.compile(file, sassOptions).css;
}

async function runTailwind(css, from, to) {
    const result = await postcss([tailwindcss({ base: root, optimize: { minify: !watchMode } })])
        .process(css, { from, to, map: false });
    await mkdir(path.dirname(to), { recursive: true });
    await writeFile(to, result.css);
}

async function buildSite() {
    const css = compileSass(path.join(stylesDir, 'site.scss'));
    await runTailwind(css, path.join(stylesDir, 'site.scss'), path.join(outputDir, 'site.css'));

    // Sayfa dosyaları @apply için temayı ve bileşenleri bu dosyadan referans alır; @source satırları
    // Styles/ klasörüne göreli olduğu için referans kopyasından çıkarılır.
    await mkdir(path.dirname(referenceFile), { recursive: true });
    await writeFile(referenceFile, css.replace(/^@source[^;]*;\s*$/gm, ''));
}

async function buildPage(fileName) {
    const source = path.join(pagesDir, fileName);
    const name = path.basename(fileName, '.scss');
    const reference = path.relative(pagesDir, referenceFile).split(path.sep).join('/');
    const css = `@reference "${reference}";\n${compileSass(source)}`;
    await runTailwind(css, source, path.join(outputDir, 'pages', `${name}.css`));
}

async function buildAll() {
    const started = Date.now();
    await buildSite();
    const pages = (await readdir(pagesDir)).filter(file => file.endsWith('.scss') && !file.startsWith('_'));
    await Promise.all(pages.map(buildPage));
    console.log(`Stiller derlendi: site + ${pages.length} sayfa (${Date.now() - started} ms)`);
}

async function safeBuild() {
    try {
        await buildAll();
    } catch (error) {
        console.error(error.formatted ?? error.message ?? error);
        if (!watchMode) process.exitCode = 1;
    }
}

await safeBuild();

if (watchMode) {
    let timer = null;
    const schedule = () => {
        clearTimeout(timer);
        timer = setTimeout(safeBuild, 150);
    };
    for (const dir of ['Styles', 'Views', path.join('wwwroot', 'js')]) {
        watch(path.join(root, dir), { recursive: true }, schedule);
    }
    console.log('Stil değişiklikleri izleniyor…');
}
