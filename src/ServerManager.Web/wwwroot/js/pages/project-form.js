import { qs } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';
import { initBuildFields } from '../features/deployments/build-fields.js';
import { initGitSource } from '../features/deployments/git-source.js';

const form = qs('[data-project-form]');

initBuildFields(form);
initGitSource(form);
bindAjaxForm(form, {
    onSuccess: response => navigate(response.data, response.message)
});
