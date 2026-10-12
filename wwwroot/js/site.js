// Appeals index: let the user switch between cards, list and table; the choice is remembered.
(function () {
    var root = document.getElementById('appeals');
    if (!root) return;

    var storageKey = 'cityfix.appeals.view';
    var modes = ['cards', 'list', 'table'];
    var buttons = document.querySelectorAll('[data-view-mode]');

    function apply(mode) {
        if (modes.indexOf(mode) === -1) mode = 'cards';
        root.setAttribute('data-view', mode);
        buttons.forEach(function (b) {
            var active = b.getAttribute('data-view-mode') === mode;
            b.classList.toggle('active', active);
            b.setAttribute('aria-pressed', active ? 'true' : 'false');
        });
        try { localStorage.setItem(storageKey, mode); } catch (e) { }
    }

    buttons.forEach(function (b) {
        b.addEventListener('click', function () { apply(b.getAttribute('data-view-mode')); });
    });

    var saved = null;
    try { saved = localStorage.getItem(storageKey); } catch (e) { }
    apply(saved);
})();
