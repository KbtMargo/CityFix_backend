// MapPicker (components/MapPicker/README.md): Leaflet + OpenStreetMap tiles, centred on Khmelnytskyi.
// Clicking the map or using geolocation sets the point: Lat/Lng go into the hidden inputs with a '.'
// decimal separator, and the address is filled from the point through /Geo/Reverse (Nominatim, via the
// server). An address the user typed is never overwritten. The search button (or Enter in the address
// field) finds the point for a typed address through /Geo/Search. Every new point fires "cf:point".
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

    // The address was filled by us (or is empty), so a new point may replace it.
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
                // A later click wins; a typed address is kept.
                if (id !== lookup || !data || !addressIsAuto()) return;
                address.value = data.address;
                address.dataset.auto = 'true';
            })
            .catch(function () { /* geocoding unavailable: the user types the address */ });
    }

    function findAddress() {
        var q = address.value.trim();
        if (q.length < 3) return;
        search.setAttribute('aria-busy', 'true');
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
            .finally(function () { search.removeAttribute('aria-busy'); });
    }

    // Editing an appeal that already has a point.
    if (latInput.value && lngInput.value) {
        var lat = parseFloat(latInput.value), lng = parseFloat(lngInput.value);
        marker = L.marker([lat, lng]).addTo(map);
        map.setView([lat, lng], 16);
        // The stored address belongs to the stored point: moving the point updates it.
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
