(function () {
    var root = document.querySelector('[data-cf-map-picker]');
    if (!root || !window.L) return;

    var latInput = document.getElementById('Lat');
    var lngInput = document.getElementById('Lng');
    var address = document.getElementById('Address');
    var coords = root.querySelector('[data-map-coords]');
    var locate = root.querySelector('[data-map-locate]');
    var search = root.querySelector('[data-map-search]');
    var template = root.dataset.coordsTemplate || '{0}, {1}';

    var map = L.map('map', { scrollWheelZoom: false }).setView([49.4229, 26.9871], 13);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(map);

    var marker = null;
    var lookup = 0;

    function addressIsAuto() { return !address.value.trim() || address.dataset.auto === 'true'; }
    address.addEventListener('input', function () { address.dataset.auto = 'false'; });

    function setPoint(lat, lng, fillAddress) {
        var la = lat.toFixed(5), ln = lng.toFixed(5);
        latInput.value = la;
        lngInput.value = ln;
        coords.textContent = template.replace('{0}', la).replace('{1}', ln);
        coords.hidden = false;
        if (marker) marker.setLatLng([lat, lng]); else marker = L.marker([lat, lng]).addTo(map);
        map.flyTo([lat, lng], 16);
        root.dispatchEvent(new CustomEvent('cf:point', { bubbles: true, detail: { lat: la, lng: ln } }));
        if (fillAddress && addressIsAuto()) reverse(la, ln);
    }

    function reverse(lat, lng) {
        var id = ++lookup;
        fetch(root.dataset.reverseUrl + '?lat=' + lat + '&lng=' + lng, { headers: { Accept: 'application/json' } })
            .then(function (r) { return r.status === 200 ? r.json() : null; })
            .then(function (data) {
                if (id !== lookup || !data || !addressIsAuto()) return;
                address.value = data.address;
                address.dataset.auto = 'true';
            })
            .catch(function () { });
    }

    function findAddress() {
        var q = address.value.trim();
        if (q.length < 3) return;
        if (search.getAttribute('aria-busy') === 'true') return;
        search.setAttribute('aria-busy', 'true');
        var spinner = document.createElement('span');
        spinner.className = 'cf-spinner app-btn-spinner';
        spinner.setAttribute('aria-hidden', 'true');
        search.prepend(spinner);
        fetch(root.dataset.searchUrl + '?q=' + encodeURIComponent(q), { headers: { Accept: 'application/json' } })
            .then(function (r) { return r.status === 200 ? r.json() : null; })
            .then(function (place) {
                if (!place) {
                    coords.textContent = root.dataset.searchFailed || '';
                    coords.hidden = false;
                    return;
                }
                setPoint(place.lat, place.lng, false);
                address.value = place.address;
                address.dataset.auto = 'true';
            })
            .catch(function () { coords.textContent = root.dataset.searchFailed || ''; coords.hidden = false; })
            .finally(function () { search.removeAttribute('aria-busy'); spinner.remove(); });
    }

    if (latInput.value && lngInput.value) {
        var lat = parseFloat(latInput.value), lng = parseFloat(lngInput.value);
        marker = L.marker([lat, lng]).addTo(map);
        map.setView([lat, lng], 16);
        address.dataset.auto = 'true';
    }

    map.on('click', function (e) { setPoint(e.latlng.lat, e.latlng.lng, true); });

    search.hidden = false;
    search.addEventListener('click', findAddress);
    address.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); findAddress(); }
    });

    if (!navigator.geolocation) {
        locate.hidden = true;
    } else {
        locate.addEventListener('click', function () {
            navigator.geolocation.getCurrentPosition(
                function (pos) { setPoint(pos.coords.latitude, pos.coords.longitude, true); },
                function () { coords.textContent = root.dataset.locateError || ''; coords.hidden = false; });
        });
    }
})();
