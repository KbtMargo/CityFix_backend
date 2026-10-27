(function () {
    var shell = document.querySelector('[data-app-shell]');
    var toggle = document.querySelector('[data-sidebar-toggle]');
    var opener = document.querySelector('[data-sidebar-open]');
    if (!shell || !toggle) return;
    var sidebar = document.getElementById(toggle.getAttribute('aria-controls'));
    var mobile = matchMedia('(max-width: 767px)');

    function label(key) {
        var text = toggle.getAttribute('data-label-' + key);
        toggle.setAttribute('aria-label', text);
        toggle.title = mobile.matches ? text : text + ' (Ctrl+B)';
    }

    function sync() {
        if (mobile.matches) {
            var open = shell.classList.contains('is-open');
            toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
            if (opener) opener.setAttribute('aria-expanded', open ? 'true' : 'false');
            label('close');
        } else {
            var collapsed = shell.classList.contains('is-collapsed');
            toggle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
            label(collapsed ? 'expand' : 'collapse');
        }
    }

    function setDrawer(open) {
        shell.classList.toggle('is-open', open);
        sync();
        if (open) {
            var first = sidebar.querySelector('[aria-current="page"], a');
            if (first) first.focus();
        } else if (opener) {
            opener.focus();
        }
    }

    if (opener) opener.addEventListener('click', function () { setDrawer(true); });

    function toggleRail() {
        var collapsed = shell.classList.toggle('is-collapsed');
        document.cookie = 'cf_sidebar=' + (collapsed ? 'collapsed' : 'expanded') + '; path=/; max-age=31536000; samesite=lax';
        hideTip();
        sync();
    }

    toggle.addEventListener('click', function () {
        if (mobile.matches) setDrawer(false);
        else toggleRail();
    });

    document.querySelectorAll('[data-sidebar-close]').forEach(function (el) {
        el.addEventListener('click', function () { setDrawer(false); });
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && shell.classList.contains('is-open')) {
            e.stopImmediatePropagation();
            setDrawer(false);
        } else if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key.toLowerCase() === 'b' && !mobile.matches) {
            if (e.target.closest('input, textarea, select, [contenteditable]')) return;
            e.preventDefault();
            toggleRail();
        }
    }, true);

    mobile.addEventListener('change', function () { shell.classList.remove('is-open'); hideTip(); sync(); });

    var tip = document.createElement('div');
    tip.className = 'app-tip';
    tip.setAttribute('aria-hidden', 'true');
    tip.hidden = true;
    document.body.appendChild(tip);

    function hideTip() { tip.hidden = true; }

    function showTip(target) {
        if (mobile.matches || !shell.classList.contains('is-collapsed')) return;
        var r = target.getBoundingClientRect();
        tip.textContent = target.getAttribute('data-tip');
        tip.style.left = (r.right + 8) + 'px';
        tip.style.top = (r.top + r.height / 2) + 'px';
        tip.hidden = false;
    }

    sidebar.querySelectorAll('[data-tip]').forEach(function (el) {
        el.addEventListener('mouseenter', function () { showTip(el); });
        el.addEventListener('mouseleave', hideTip);
        el.addEventListener('focus', function () { showTip(el); });
        el.addEventListener('blur', hideTip);
    });
    sidebar.addEventListener('scroll', hideTip);

    sync();
})();

document.querySelectorAll('[data-theme-toggle]').forEach(function (button) {
    function sync() {
        var dark = document.documentElement.dataset.theme === 'dark';
        var text = button.getAttribute(dark ? 'data-label-light' : 'data-label-dark');
        button.setAttribute('aria-label', text);
        button.title = text;
    }
    button.hidden = false;
    sync();
    button.addEventListener('click', function () {
        var next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
        document.documentElement.dataset.theme = next;
        try { localStorage.setItem('cf-theme', next); } catch (e) { }
        document.querySelectorAll('[data-theme-toggle]').forEach(function (b) { b.dispatchEvent(new Event('cf:theme')); });
    });
    button.addEventListener('cf:theme', sync);
});

(function () {
    var overlay = document.querySelector('[data-close-href]');
    if (!overlay) return;
    var href = overlay.getAttribute('data-close-href');
    var dialog = overlay.querySelector('[role="dialog"]');
    if (dialog) {
        dialog.setAttribute('tabindex', '-1');
        dialog.focus();
    }
    overlay.addEventListener('click', function (e) { if (e.target === overlay) location.href = href; });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') location.href = href; });
})();

document.querySelectorAll('[data-flash]').forEach(function (flash) {
    var close = flash.querySelector('[data-flash-close]');
    if (!close) return;
    close.hidden = false;
    close.addEventListener('click', function () { flash.remove(); });
});

document.addEventListener('submit', function (e) {
    var form = e.target;
    if (e.defaultPrevented || (form.method || '').toLowerCase() !== 'post') return;
    if (form.dataset.submitting) { e.preventDefault(); return; }
    form.dataset.submitting = 'true';
    var button = e.submitter;
    if (button && !button.name && (button.classList.contains('cf-btn') || button.classList.contains('cf-auth__submit'))) {
        button.setAttribute('aria-disabled', 'true');
        button.setAttribute('aria-busy', 'true');
        var spinner = document.createElement('span');
        spinner.className = 'cf-spinner app-btn-spinner';
        spinner.setAttribute('aria-hidden', 'true');
        button.prepend(spinner);
    }
});

window.addEventListener('pageshow', function (e) {
    if (!e.persisted) return;
    document.querySelectorAll('form[data-submitting]').forEach(function (form) { delete form.dataset.submitting; });
    document.querySelectorAll('[aria-busy="true"]').forEach(function (button) {
        button.removeAttribute('aria-busy');
        button.removeAttribute('aria-disabled');
        var spinner = button.querySelector('.app-btn-spinner');
        if (spinner) spinner.remove();
    });
});

document.querySelectorAll('form.cf-form-card').forEach(function (form) {
    var dirty = false;
    form.addEventListener('input', function () { dirty = true; });
    form.addEventListener('change', function () { dirty = true; });
    window.addEventListener('beforeunload', function (e) {
        if (dirty && !form.dataset.submitting) { e.preventDefault(); e.returnValue = ''; }
    });
});

document.querySelectorAll('[data-close-details]').forEach(function (button) {
    button.addEventListener('click', function () { button.closest('details').open = false; });
});

document.querySelectorAll('[data-cf-gallery]').forEach(function (gallery) {
    var main = gallery.querySelector('[data-gallery-main]');
    var thumbs = Array.from(gallery.querySelectorAll('[data-gallery-src]'));
    if (!main || thumbs.length < 2) return;
    var current = 0;

    function show(index) {
        current = (index + thumbs.length) % thumbs.length;
        main.src = thumbs[current].getAttribute('data-gallery-src');
        thumbs.forEach(function (t, i) { t.classList.toggle('is-active', i === current); });
    }

    thumbs.forEach(function (t, i) { t.addEventListener('click', function () { show(i); }); });
    gallery.querySelectorAll('[data-gallery-step]').forEach(function (button) {
        button.addEventListener('click', function () { show(current + parseInt(button.getAttribute('data-gallery-step'), 10)); });
    });
});

(function () {
    var label = document.body.getAttribute('data-col-resize-label') || '';
    var MIN = 60;

    document.querySelectorAll('table.cf-table').forEach(function (table, index) {
        var headers = Array.from(table.querySelectorAll('thead th'));
        if (headers.length < 2) return;
        var key = 'cf_cols:' + location.pathname.toLowerCase() + ':' + index;

        function apply(widths) {
            headers.forEach(function (th, i) { th.style.width = widths[i] + 'px'; });
            table.style.width = widths.reduce(function (sum, w) { return sum + w; }, 0) + 'px';
            table.classList.add('is-resized');
        }

        function current() {
            return headers.map(function (th) { return Math.round(th.getBoundingClientRect().width); });
        }

        function save() {
            try { localStorage.setItem(key, JSON.stringify(current())); } catch (e) { }
        }

        function reset() {
            headers.forEach(function (th) { th.style.width = ''; });
            table.style.width = '';
            table.classList.remove('is-resized');
            try { localStorage.removeItem(key); } catch (e) { }
        }

        function resize(i, delta) {
            var widths = current();
            widths[i] = Math.max(MIN, widths[i] + delta);
            apply(widths);
        }

        try {
            var saved = JSON.parse(localStorage.getItem(key) || 'null');
            if (Array.isArray(saved) && saved.length === headers.length) apply(saved);
        } catch (e) { }

        headers.slice(0, -1).forEach(function (th, i) {
            var handle = document.createElement('span');
            handle.className = 'app-col-resize';
            handle.tabIndex = 0;
            handle.setAttribute('role', 'separator');
            handle.setAttribute('aria-orientation', 'vertical');
            handle.setAttribute('aria-label', label + (th.textContent.trim() ? ': ' + th.textContent.trim() : ''));
            th.appendChild(handle);

            var startX = 0, startWidths = null;
            handle.addEventListener('pointerdown', function (e) {
                if (e.button !== 0) return;
                e.preventDefault();
                startX = e.clientX;
                startWidths = current();
                apply(startWidths);
                handle.setPointerCapture(e.pointerId);
                handle.classList.add('is-active');
            });
            handle.addEventListener('pointermove', function (e) {
                if (!startWidths) return;
                var widths = startWidths.slice();
                widths[i] = Math.max(MIN, startWidths[i] + e.clientX - startX);
                apply(widths);
            });
            function stop() {
                if (!startWidths) return;
                startWidths = null;
                handle.classList.remove('is-active');
                save();
            }
            handle.addEventListener('pointerup', stop);
            handle.addEventListener('pointercancel', stop);
            handle.addEventListener('dblclick', reset);
            handle.addEventListener('keydown', function (e) {
                var step = e.shiftKey ? 50 : 10;
                if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
                    e.preventDefault();
                    resize(i, e.key === 'ArrowRight' ? step : -step);
                    save();
                } else if (e.key === 'Escape') {
                    reset();
                }
            });
        });
    });
})();

document.querySelectorAll('details[data-menu]').forEach(function (menu) {
    document.addEventListener('click', function (e) { if (menu.open && !menu.contains(e.target)) menu.open = false; });
    menu.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && menu.open) { menu.open = false; menu.querySelector('summary').focus(); }
    });
    menu.querySelectorAll('a').forEach(function (a) { a.addEventListener('click', function () { menu.open = false; }); });
});

// "Affects me too" buttons on appeal cards and in the appeal dialog.
document.querySelectorAll('[data-support]').forEach(function (button) {
    var box = button.parentElement;
    var label = button.querySelector('[data-support-label]');
    var count = box.querySelector('[data-support-count]');
    var template = count ? count.textContent.replace(/\d+/, '{0}') : '';
    button.addEventListener('click', function (e) {
        e.stopPropagation();
        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        button.disabled = true;
        fetch(button.dataset.url, { method: 'POST', headers: { Accept: 'application/json', RequestVerificationToken: token ? token.value : '' } })
            .then(function (r) { return r.ok ? r.json() : Promise.reject(); })
            .then(function (result) {
                button.setAttribute('aria-pressed', result.supported ? 'true' : 'false');
                button.classList.toggle('cf-btn--outline', !result.supported);
                button.title = result.supported ? button.dataset.titleOn : '';
                label.textContent = result.supported ? button.dataset.labelOn : button.dataset.labelOff;
                if (count) {
                    count.textContent = template.replace('{0}', result.count);
                    count.hidden = result.count === 0;
                }
            })
            .catch(function () { })
            .finally(function () { button.disabled = false; });
    });
});

// Password fields: a show/hide button, a Caps Lock warning, and on new-password forms a live length and match check.
(function () {
    document.querySelectorAll('[data-password-toggle]').forEach(function (button) {
        var input = button.parentElement.querySelector('input');
        if (!input) return;
        var icon = button.querySelector('.cf-icon');
        button.hidden = false;
        button.addEventListener('click', function () {
            var show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            var label = show ? button.dataset.labelHide : button.dataset.labelShow;
            button.setAttribute('aria-pressed', show ? 'true' : 'false');
            button.setAttribute('aria-label', label);
            button.title = label;
            icon.classList.toggle('cf-icon--eye', !show);
            icon.classList.toggle('cf-icon--eye-off', show);
            // Keep the caret where it was, so the user can carry on typing.
            var at = input.selectionStart;
            input.focus();
            if (at != null) input.setSelectionRange(at, at);
        });
        // A visible password must not end up in the browser's form history or be submitted as text in a back-forward page.
        input.form && input.form.addEventListener('submit', function () { input.type = 'password'; });
    });

    document.querySelectorAll('input[type="password"]').forEach(function (input) {
        var field = input.closest('.app-auth-field');
        var hint = field && field.querySelector('[data-capslock]');
        if (!hint) return;
        function check(e) { if (e.getModifierState) hint.hidden = !e.getModifierState('CapsLock'); }
        input.addEventListener('keydown', check);
        input.addEventListener('keyup', check);
        input.addEventListener('blur', function () { hint.hidden = true; });
    });

    var password = document.querySelector('[data-password-new]');
    if (!password) return;
    var rule = document.querySelector('[data-password-rule]');
    var confirm = document.querySelector('[data-password-confirm]');
    var match = document.querySelector('[data-password-match]');
    function update() {
        if (rule) {
            var ok = password.value.length >= Number(rule.dataset.min || 8);
            rule.classList.toggle('is-ok', ok);
        }
        if (confirm && match) {
            match.hidden = confirm.value.length === 0;
            var same = confirm.value === password.value;
            match.textContent = same ? match.dataset.textOk : match.dataset.textBad;
            match.classList.toggle('is-ok', same);
            match.classList.toggle('is-bad', !same);
        }
    }
    password.addEventListener('input', update);
    if (confirm) confirm.addEventListener('input', update);
    update();
})();
