// Small progressive enhancements. Every page works without them.

// Overlays (detail modal, delete dialog): clicking the backdrop or pressing Escape closes them,
// by going to the same URL the close button links to.
(function () {
    var overlay = document.querySelector('[data-close-href]');
    if (!overlay) return;
    var href = overlay.getAttribute('data-close-href');
    overlay.addEventListener('click', function (e) { if (e.target === overlay) location.href = href; });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') location.href = href; });
})();

// The close (×) button inside the filters popover closes its <details>.
document.querySelectorAll('[data-close-details]').forEach(function (button) {
    button.addEventListener('click', function () { button.closest('details').open = false; });
});

// Photo carousel in the detail modal: thumbnails and arrows switch the main image.
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
