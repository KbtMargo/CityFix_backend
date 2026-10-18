// PhotoUpload: the design system's dropzone script (components/PhotoUpload/preview.html).
// Adds drag-and-drop, thumbnails and removal by rebuilding input.files with DataTransfer.
// The only changes: the file limit and the remove label come from data attributes (localized).
document.querySelectorAll('[data-cf-dropzone]').forEach(function (zone) {
    var input = document.getElementById(zone.getAttribute('for'));
    var list = document.querySelector(zone.dataset.cfDropzone);
    var max = parseInt(zone.dataset.maxFiles || '5', 10);
    var removeLabel = zone.dataset.removeLabel || 'Remove file';
    if (!input || !list) return;

    ['dragenter', 'dragover'].forEach(function (e) { zone.addEventListener(e, function (ev) { ev.preventDefault(); zone.classList.add('is-dragover'); }); });
    ['dragleave', 'drop'].forEach(function (e) { zone.addEventListener(e, function () { zone.classList.remove('is-dragover'); }); });
    zone.addEventListener('drop', function (ev) {
        ev.preventDefault();
        var dt = new DataTransfer();
        Array.from(input.files).concat(Array.from(ev.dataTransfer.files)).slice(0, max).forEach(function (f) { dt.items.add(f); });
        input.files = dt.files;
        render();
    });

    function render() {
        if (input.files.length > max) {
            var trimmed = new DataTransfer();
            Array.from(input.files).slice(0, max).forEach(function (f) { trimmed.items.add(f); });
            input.files = trimmed.files;
        }
        var files = Array.from(input.files);
        list.innerHTML = '';
        list.hidden = !files.length;
        files.forEach(function (f, i) {
            var d = document.createElement('div');
            d.className = 'cf-preview';
            d.innerHTML = '<img alt=""><button type="button" class="cf-preview__remove"><span class="cf-icon cf-icon--x-lucide cf-icon--3"></span></button>';
            d.querySelector('img').src = URL.createObjectURL(f);
            var remove = d.querySelector('button');
            remove.setAttribute('aria-label', removeLabel + ' ' + f.name);
            remove.onclick = function () {
                var dt = new DataTransfer();
                files.forEach(function (g, j) { if (j !== i) dt.items.add(g); });
                input.files = dt.files;
                render();
            };
            list.appendChild(d);
        });
    }

    input.addEventListener('change', render);
});
