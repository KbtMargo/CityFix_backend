// MapPicker (components/MapPicker/README.md): Leaflet + OpenStreetMap tiles, centred on Khmelnytskyi.
// Clicking the map or using geolocation sets the point: Lat/Lng go into the hidden inputs with a '.'
// decimal separator, and the address is cleared (as the source does). The address stays optional
// once a point is chosen; without JS the address field alone is enough.
(function () {
    var root = document.querySelector('[data-cf-map-picker]');
    if (!root || !window.L) return;

    var latInput = document.getElementById('Lat');
    var lngInput = document.getElementById('Lng');
    var address = document.getElementById('Address');
    var coords = root.querySelector('[data-map-coords]');
    var locate = root.querySelector('[data-map-locate]');
    var template = root.dataset.coordsTemplate || '{0}, {1}';

    var map = L.map('map', { scrollWheelZoom: false }).setView([49.4229, 26.9871], 13);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(map);

    var marker = null;

    function setPoint(lat, lng, clearAddress) {
        var la = lat.toFixed(5), ln = lng.toFixed(5);
        latInput.value = la;
        lngInput.value = ln;
        if (clearAddress && address) address.value = '';
        coords.textContent = template.replace('{0}', la).replace('{1}', ln);
        coords.hidden = false;
        if (marker) marker.setLatLng([lat, lng]); else marker = L.marker([lat, lng]).addTo(map);
        map.flyTo([lat, lng], 15);
    }

    // Editing an appeal that already has a point.
    if (latInput.value && lngInput.value) {
        var lat = parseFloat(latInput.value), lng = parseFloat(lngInput.value);
        marker = L.marker([lat, lng]).addTo(map);
        map.setView([lat, lng], 15);
    }

    map.on('click', function (e) { setPoint(e.latlng.lat, e.latlng.lng, true); });

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
