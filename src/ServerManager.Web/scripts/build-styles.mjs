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
const pluginsDir = path.resolve(root, '..', 'Plugins');
const tailwindUtilities = path.join(root, 'node_modules', 'tailwindcss', 'utilities.css');
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

async function listPages(dir) {
    try {
        return (await readdir(dir)).filter(file => file.endsWith('.scss') && !file.startsWith('_'));
    } catch (error) {
        if (error.code === 'ENOENT') return [];
        throw error;
    }
}

async function listPlugins() {
    try {
        const entries = await readdir(pluginsDir, { withFileTypes: true });
        return entries.filter(entry => entry.isDirectory()).map(entry => path.join(pluginsDir, entry.name));
    } catch (error) {
        if (error.code === 'ENOENT') return [];
        throw error;
    }
}

// Eklenti sayfaları çekirdek temayı referans alır; site.css eklenti view'larını taramadığı için o view'larda geçen
// yardımcı sınıflar da sayfa dosyasına üretilir. Yollar mutlak verilir çünkü eklenti klasöründe node_modules yoktur.
async function buildPluginPage(pluginDir, fileName) {
    const pluginPagesDir = path.join(pluginDir, 'Styles', 'pages');
    const source = path.join(pluginPagesDir, fileName);
    const name = path.basename(fileName, '.scss');
    const posix = file => file.split(path.sep).join('/');
    const css = [
        `@reference "${posix(referenceFile)}";`,
        `@import "${posix(tailwindUtilities)}" layer(utilities) source(none);`,
        `@source "${posix(path.join(pluginDir, 'Views'))}/**/*.cshtml";`,
        `@source "${posix(path.join(pluginDir, 'Content', 'js'))}/**/*.js";`,
        compileSass(source)
    ].join('\n');
    await runTailwind(css, source, path.join(pluginDir, 'Content', 'css', 'pages', `${name}.css`));
}

async function buildAll() {
    const started = Date.now();
    await buildSite();
    const pages = await listPages(pagesDir);
    await Promise.all(pages.map(buildPage));

    let pluginPageCount = 0;
    for (const pluginDir of await listPlugins()) {
        const pluginPages = await listPages(path.join(pluginDir, 'Styles', 'pages'));
        await Promise.all(pluginPages.map(file => buildPluginPage(pluginDir, file)));
        pluginPageCount += pluginPages.length;
    }

    console.log(`Stiller derlendi: site + ${pages.length} sayfa + ${pluginPageCount} eklenti sayfası (${Date.now() - started} ms)`);
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
    for (const pluginDir of await listPlugins()) {
        for (const dir of ['Styles', 'Views', path.join('Content', 'js')]) {
            watch(path.join(pluginDir, dir), { recursive: true }, schedule);
        }
    }
    console.log('Stil değişiklikleri izleniyor…');
}
