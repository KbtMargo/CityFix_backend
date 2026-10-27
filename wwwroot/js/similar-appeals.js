(function () {
    var box = document.querySelector('[data-similar-appeals]');
    var category = document.querySelector('[data-similar-category]');
    if (!box || !category) return;

    var list = box.querySelector('[data-similar-list]');
    var latInput = document.getElementById('Lat');
    var lngInput = document.getElementById('Lng');
    var request = 0;

    function check() {
        var lat = latInput.value, lng = lngInput.value, cat = category.value;
        if (!lat || !lng || !cat) { box.hidden = true; return; }

        var id = ++request;
        var url = box.dataset.url + '?lat=' + lat + '&lng=' + lng + '&categoryId=' + cat + (box.dataset.exclude ? '&excludeId=' + box.dataset.exclude : '');
        fetch(url, { headers: { Accept: 'application/json' } })
            .then(function (r) { return r.ok ? r.json() : []; })
            .then(function (items) {
                if (id !== request) return;
                list.innerHTML = '';
                items.forEach(function (s) {
                    var li = document.createElement('li');
                    var title = document.createElement(s.url ? 'a' : 'span');
                    title.className = 'app-similar__name';
                    title.textContent = s.title || box.dataset.hiddenTitle;
                    if (s.url) { title.href = s.url; title.target = '_blank'; title.rel = 'noopener'; }
                    var meta = document.createElement('span');
                    meta.className = 'app-similar__meta';
                    meta.textContent = s.status + ' · ' + s.date + ' · ' + s.distance;
                    li.append(title, meta);
                    list.appendChild(li);
                });
                box.hidden = items.length === 0;
            })
            .catch(function () { box.hidden = true; });
    }

    document.addEventListener('cf:point', check);
    category.addEventListener('change', check);
    check();
})();
