# MapPicker
The Місцезнаходження block of the new-issue form:
- a help line,
- an address input with an outline icon button that geolocates (`locate-fixed`),
- a green "Координати обрано: lat, lng" line,
- a Leaflet map (`patterns/MapPicker.tsx`).

**Map**
- 300px high, full width, 8px radius, 16px top margin.
- OpenStreetMap tiles, the default blue Leaflet marker, and the default zoom control.
- It centres on Khmelnytskyi (49.4229, 26.9871) at zoom 13, and flies to the chosen point at zoom 15.
- Scroll-wheel zoom is off.

**In Razor.** Load Leaflet 1.9 from cdnjs and mount it on `div.cf-map`. On map click, write lat/lng into hidden inputs and clear the address, as the source does. The preview shows a static stand-in, because tiles can't load in this page.

```cshtml
<div class="cf-stack-2"><label class="cf-label" for="Address">Місцезнаходження</label>
  <p class="cf-help">Введіть адресу, використайте геолокацію, або оберіть точку на карті.</p>
  <div class="cf-toolbar" style="gap:8px"><div class="cf-toolbar__grow"><input asp-for="Address" class="cf-input" placeholder="Введіть адресу..." /></div>
    <button type="button" class="cf-btn cf-btn--outline cf-btn--icon" id="locate" aria-label="Визначити поточне місцезнаходження"><span class="cf-icon cf-icon--locate-fixed cf-icon--4"></span></button></div>
  <p class="cf-success-text" id="coords" hidden></p>
  <div class="cf-map" id="map"></div>
  <input type="hidden" asp-for="Lat" /><input type="hidden" asp-for="Lng" /></div>
```
