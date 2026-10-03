import { qs } from '@app/core/dom.js';

const root = qs('[data-github-return]');
const target = root?.dataset.target;

if (target && target.startsWith('/') && !target.startsWith('//')) {
    window.location.replace(target);
}
