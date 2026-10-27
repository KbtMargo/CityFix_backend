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

// Sidebar order: press and hold an item to drag it elsewhere (Alt+↑/↓ from the keyboard). The order is kept per user in
// this browser; items that appear later (a new role, a new page) keep their default place.
(function () {
    var nav = document.querySelector('[data-nav-reorder]');
    if (!nav) return;
    var storageKey = 'cf_nav_order_' + nav.dataset.navReorder;
    var toast = document.querySelector('[data-nav-toast]');
    var HOLD_MS = 450, SLOP_PX = 8;

    function items() { return Array.prototype.slice.call(nav.querySelectorAll('[data-nav-key]')); }
    var defaults = items().map(function (i) { return i.dataset.navKey; });

    function arrange(keys) {
        var byKey = {};
        items().forEach(function (i) { byKey[i.dataset.navKey] = i; });
        keys.forEach(function (k) { if (byKey[k]) { nav.appendChild(byKey[k]); delete byKey[k]; } });
        // Whatever the saved order does not know goes in its default position relative to the rest.
        defaults.forEach(function (k, index) {
            if (!byKey[k]) return;
            var next = defaults.slice(index + 1).map(function (n) { return nav.querySelector('[data-nav-key="' + n + '"]'); }).filter(Boolean)[0];
            nav.insertBefore(byKey[k], next && next.parentNode === nav ? next : null);
        });
    }
    function save() { try { localStorage.setItem(storageKey, JSON.stringify(items().map(function (i) { return i.dataset.navKey; }))); } catch (e) { } }
    try {
        var saved = JSON.parse(localStorage.getItem(storageKey) || 'null');
        if (Array.isArray(saved)) arrange(saved);
    } catch (e) { }

    var hideTimer = null;
    function announce(text, withReset) {
        if (!toast) return;
        toast.textContent = text;
        if (withReset) {
            var reset = document.createElement('button');
            reset.type = 'button';
            reset.className = 'app-nav-toast__reset';
            reset.textContent = nav.dataset.textReset;
            reset.addEventListener('click', function () {
                try { localStorage.removeItem(storageKey); } catch (e) { }
                arrange(defaults);
                toast.hidden = true;
            });
            toast.appendChild(document.createTextNode(' '));
            toast.appendChild(reset);
        }
        toast.hidden = false;
        clearTimeout(hideTimer);
        hideTimer = setTimeout(function () { toast.hidden = true; }, 6000);
    }

    var holdTimer = null, start = null, dragging = null, suppressClick = false;
    function cancelHold() { clearTimeout(holdTimer); holdTimer = null; start = null; nav.classList.remove('is-pressing'); }

    nav.addEventListener('pointerdown', function (e) {
        var item = e.target.closest('[data-nav-key]');
        if (!item || e.button !== 0) return;
        start = { x: e.clientX, y: e.clientY };
        nav.classList.add('is-pressing');
        holdTimer = setTimeout(function () {
            holdTimer = null;
            dragging = item;
            suppressClick = true;
            try { item.setPointerCapture(e.pointerId); } catch (err) { }
            nav.classList.remove('is-pressing');
            nav.classList.add('is-reordering');
            item.classList.add('is-dragging');
            if (navigator.vibrate) navigator.vibrate(15);
            announce(nav.dataset.textHint, false);
        }, HOLD_MS);
    });

    nav.addEventListener('pointermove', function (e) {
        if (dragging) {
            e.preventDefault();
            var others = items().filter(function (i) { return i !== dragging; });
            var before = null;
            for (var i = 0; i < others.length; i++) {
                var r = others[i].getBoundingClientRect();
                if (e.clientY < r.top + r.height / 2) { before = others[i]; break; }
            }
            if (before ? dragging.nextElementSibling !== before : nav.lastElementChild !== dragging) nav.insertBefore(dragging, before);
            return;
        }
        if (start && (Math.abs(e.clientX - start.x) > SLOP_PX || Math.abs(e.clientY - start.y) > SLOP_PX)) cancelHold();
    });

    function finish() {
        if (!dragging) { cancelHold(); return; }
        dragging.classList.remove('is-dragging');
        nav.classList.remove('is-reordering');
        dragging = null;
        start = null;
        save();
        announce(nav.dataset.textSaved, true);
        // The click that ends a long press must not open the page; let it pass, then go back to normal.
        setTimeout(function () { suppressClick = false; }, 400);
    }
    nav.addEventListener('pointerup', finish);
    nav.addEventListener('pointercancel', finish);
    nav.addEventListener('lostpointercapture', function () { if (dragging) finish(); });

    nav.addEventListener('click', function (e) {
        if (suppressClick) { e.preventDefault(); e.stopPropagation(); }
    }, true);
    // Phones open a link menu on long press, and browsers start dragging the link itself; neither should happen here.
    nav.addEventListener('contextmenu', function (e) { if (holdTimer || dragging || suppressClick) e.preventDefault(); });
    nav.addEventListener('dragstart', function (e) { e.preventDefault(); });
    nav.addEventListener('touchmove', function (e) { if (dragging) e.preventDefault(); }, { passive: false });

    nav.addEventListener('keydown', function (e) {
        var item = e.target.closest('[data-nav-key]');
        if (!item || !e.altKey || (e.key !== 'ArrowUp' && e.key !== 'ArrowDown')) return;
        e.preventDefault();
        if (e.key === 'ArrowUp' && item.previousElementSibling) nav.insertBefore(item, item.previousElementSibling);
        else if (e.key === 'ArrowDown' && item.nextElementSibling) nav.insertBefore(item.nextElementSibling, item);
        item.focus();
        save();
        announce(nav.dataset.textMoved.replace('{0}', items().indexOf(item) + 1), true);
    });
})();

// Settings forms that save as soon as a switch is flipped; their save button is only a no-JavaScript fallback.
document.querySelectorAll('form[data-autosave]').forEach(function (form) {
    var fallback = form.querySelector('[data-autosave-button]');
    if (fallback) fallback.hidden = true;
    form.addEventListener('change', function () { form.requestSubmit(); });
});

// Profile section menu: highlights the last section whose top has scrolled past the header (the last one at the very bottom).
(function () {
    var nav = document.querySelector('[data-profile-nav]');
    if (!nav) return;
    var links = Array.prototype.slice.call(nav.querySelectorAll('a[href^="#"]'));
    var sections = links.map(function (a) { return document.getElementById(a.getAttribute('href').slice(1)); });
    // The app shell scrolls its main column, not the window.
    var scroller = nav.closest('.cf-shell__main') || document.scrollingElement || document.documentElement;
    var events = scroller === document.scrollingElement || scroller === document.documentElement ? window : scroller;
    function update() {
        var top = scroller.getBoundingClientRect ? Math.max(0, scroller.getBoundingClientRect().top) : 0;
        var current = 0;
        sections.forEach(function (s, i) { if (s && s.getBoundingClientRect().top - top <= 140) current = i; });
        if (scroller.scrollTop > 0 && Math.ceil(scroller.scrollTop + scroller.clientHeight) >= scroller.scrollHeight) current = sections.length - 1;
        links.forEach(function (a, i) {
            a.classList.toggle('is-active', i === current);
            if (i === current) a.setAttribute('aria-current', 'true'); else a.removeAttribute('aria-current');
        });
    }
    events.addEventListener('scroll', update, { passive: true });
    window.addEventListener('resize', update);
    update();
})();

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
