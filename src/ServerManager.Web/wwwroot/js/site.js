(function () {
    'use strict';

    var THEME_KEY = 'sm-theme';
    var media = window.matchMedia('(prefers-color-scheme: dark)');

    function getTheme() {
        return localStorage.getItem(THEME_KEY) || 'system';
    }

    function applyTheme(theme) {
        var dark = theme === 'dark' || (theme === 'system' && media.matches);
        document.documentElement.classList.toggle('dark', dark);
        document.querySelectorAll('[data-theme-icon]').forEach(function (icon) {
            icon.hidden = icon.getAttribute('data-theme-icon') !== theme;
        });
    }

    function initTheme() {
        applyTheme(getTheme());
        media.addEventListener('change', function () {
            if (getTheme() === 'system') applyTheme('system');
        });
        document.querySelectorAll('[data-theme-value]').forEach(function (button) {
            button.addEventListener('click', function () {
                var theme = button.getAttribute('data-theme-value');
                localStorage.setItem(THEME_KEY, theme);
                applyTheme(theme);
                closeAllDropdowns();
            });
        });
    }

    function closeAllDropdowns(except) {
        document.querySelectorAll('[data-dropdown-menu]').forEach(function (menu) {
            if (menu !== except) menu.hidden = true;
        });
    }

    function initDropdowns() {
        document.querySelectorAll('[data-dropdown]').forEach(function (dropdown) {
            var toggle = dropdown.querySelector('[data-dropdown-toggle]');
            var menu = dropdown.querySelector('[data-dropdown-menu]');
            if (!toggle || !menu) return;
            toggle.addEventListener('click', function (event) {
                event.stopPropagation();
                var willOpen = menu.hidden;
                closeAllDropdowns(menu);
                menu.hidden = !willOpen;
            });
        });
        document.addEventListener('click', function (event) {
            if (!event.target.closest('[data-dropdown]')) closeAllDropdowns();
        });
        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') closeAllDropdowns();
        });
    }

    function initSidebar() {
        var sidebar = document.querySelector('[data-sidebar]');
        var overlay = document.querySelector('[data-sidebar-overlay]');
        if (!sidebar || !overlay) return;
        function setOpen(open) {
            sidebar.classList.toggle('is-open', open);
            overlay.classList.toggle('is-open', open);
        }
        document.querySelectorAll('[data-sidebar-toggle]').forEach(function (button) {
            button.addEventListener('click', function () {
                setOpen(!sidebar.classList.contains('is-open'));
            });
        });
        overlay.addEventListener('click', function () { setOpen(false); });
    }

    function dismissToast(toast) {
        toast.classList.add('is-leaving');
        setTimeout(function () { toast.remove(); }, 300);
    }

    function showToast(type, message) {
        var stack = document.querySelector('[data-toast-stack]');
        if (!stack) return;
        var toast = document.createElement('div');
        toast.className = type === 'success' ? 'toast-success' : 'toast-error';
        toast.setAttribute('role', type === 'success' ? 'status' : 'alert');
        var text = document.createElement('p');
        text.className = 'flex-1';
        text.textContent = message;
        toast.appendChild(text);
        stack.appendChild(toast);
        setTimeout(function () { dismissToast(toast); }, 6000);
    }

    function initToasts() {
        document.querySelectorAll('[data-toast]').forEach(function (toast) {
            var close = toast.querySelector('[data-toast-close]');
            if (close) close.addEventListener('click', function () { dismissToast(toast); });
            setTimeout(function () { dismissToast(toast); }, 6000);
        });
    }

    function antiforgeryToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function postJson(url) {
        return fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Accept': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
                'RequestVerificationToken': antiforgeryToken()
            }
        }).then(function (response) {
            return response.json().catch(function () {
                return { isSuccess: false, message: 'Sunucudan beklenmeyen bir yanıt alındı.' };
            });
        });
    }

    function initConfirmModals() {
        document.querySelectorAll('[data-modal-open]').forEach(function (trigger) {
            trigger.addEventListener('click', function () {
                var modal = document.getElementById(trigger.getAttribute('data-modal-open'));
                if (!modal) return;
                modal.hidden = false;
                var input = modal.querySelector('[data-confirm-input]');
                if (input) { input.value = ''; input.focus(); input.dispatchEvent(new Event('input')); }
            });
        });

        document.querySelectorAll('[data-modal]').forEach(function (modal) {
            modal.querySelectorAll('[data-modal-close]').forEach(function (button) {
                button.addEventListener('click', function () { modal.hidden = true; });
            });
            modal.addEventListener('click', function (event) {
                if (event.target === modal) modal.hidden = true;
            });

            var input = modal.querySelector('[data-confirm-input]');
            var submit = modal.querySelector('[data-confirm-submit]');
            if (input && submit) {
                var expected = input.getAttribute('data-confirm-value');
                input.addEventListener('input', function () {
                    submit.disabled = input.value.trim() !== expected;
                });
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key !== 'Escape') return;
            document.querySelectorAll('[data-modal]').forEach(function (modal) { modal.hidden = true; });
        });
    }

    function initAuthTypeFields() {
        var select = document.querySelector('[data-auth-type]');
        if (!select) return;
        var sudo = document.querySelector('[data-use-sudo]');

        function update() {
            var value = select.value;
            document.querySelectorAll('[data-auth-field]').forEach(function (field) {
                var types = field.getAttribute('data-auth-field').split(',');
                field.hidden = types.indexOf(value) === -1;
            });
            document.querySelectorAll('[data-sudo-field]').forEach(function (field) {
                field.hidden = !(sudo && sudo.checked);
            });
        }

        select.addEventListener('change', update);
        if (sudo) sudo.addEventListener('change', update);
        update();
    }

    function initConnectionTest() {
        document.querySelectorAll('[data-connection-test]').forEach(function (button) {
            var resultBox = document.querySelector(button.getAttribute('data-result-target'));
            button.addEventListener('click', function () {
                var label = button.querySelector('[data-label]');
                var spinner = button.querySelector('[data-spinner]');
                button.disabled = true;
                if (spinner) spinner.hidden = false;
                if (label) label.textContent = 'Test ediliyor…';

                postJson(button.getAttribute('data-url')).then(function (response) {
                    var data = response.data;
                    if (!response.isSuccess || !data) {
                        renderResult(resultBox, false, response.message || 'Bağlantı testi yapılamadı.');
                        showToast('error', response.message || 'Bağlantı testi yapılamadı.');
                        return;
                    }
                    var message = data.message;
                    if (data.fingerprintTrustedNow) message += ' Host key kaydedildi: ' + data.hostKeyFingerprint;
                    renderResult(resultBox, data.isSuccess, message);
                    showToast(data.isSuccess ? 'success' : 'error', data.message);
                    if (!data.fingerprintMismatch) {
                        setTimeout(function () { window.location.reload(); }, 1800);
                    }
                }).catch(function () {
                    renderResult(resultBox, false, 'Ağ hatası: istek gönderilemedi.');
                }).finally(function () {
                    button.disabled = false;
                    if (spinner) spinner.hidden = true;
                    if (label) label.textContent = 'Bağlantıyı Test Et';
                });
            });
        });
    }

    function renderResult(box, success, message) {
        if (!box) return;
        box.hidden = false;
        box.className = success ? 'alert-success' : 'alert-error';
        box.textContent = message;
    }

    function initAutoSubmit() {
        document.querySelectorAll('[data-auto-submit]').forEach(function (element) {
            element.addEventListener('change', function () {
                if (element.form) element.form.submit();
            });
        });
    }

    function initSubmitLoading() {
        document.querySelectorAll('form[data-loading]').forEach(function (form) {
            form.addEventListener('submit', function () {
                var button = form.querySelector('[type="submit"]');
                if (!button) return;
                setTimeout(function () { button.disabled = true; }, 0);
                var spinner = button.querySelector('[data-spinner]');
                if (spinner) spinner.hidden = false;
            });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        initTheme();
        initDropdowns();
        initSidebar();
        initToasts();
        initConfirmModals();
        initAuthTypeFields();
        initConnectionTest();
        initAutoSubmit();
        initSubmitLoading();
    });
})();
