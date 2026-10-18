# PhotoUpload
The "Фото-докази (до 5 файлів)" control from the new-issue form: a 128px dashed dropzone, then a grid of previews. Each preview has a red round remove button that shows on hover.

**Dropzone**
- `label.cf-dropzone` with a 2px dashed `dropzone-line` border and `rounded-md`.
- The upload icon is 48px slate-400.
- Text: "**Натисніть, щоб завантажити** або перетягніть файли" (the link part in blue-600 semibold), and the hint "PNG, JPG, GIF до 10MB".
- On hover the border turns blue-500.
- Because it is a `<label for>`, clicking it opens the hidden `input[type=file]` without JavaScript.

**Previews.** `.cf-previews` has 3 columns, 5 from 640px. Each `.cf-preview` is a 96px-high image (`rounded-md`) with `__remove` (red-500, white ×) in the top-right corner.

**Script.** The preview script, shown in this card's preview, adds drag-and-drop, thumbnails and removal, using `DataTransfer` to edit `input.files`.

**Don't**
- Upload on selection. Files go with the form submit.

```cshtml
<div><label class="cf-label" for="Photos">Фото-докази (до 5 файлів)</label>
  <label class="cf-dropzone" for="Photos" data-cf-dropzone="#photo-previews"><div class="cf-dropzone__inner">
    <span class="cf-icon cf-icon--upload cf-icon--12 cf-dropzone__icon"></span>
    <p class="cf-dropzone__text"><span class="cf-dropzone__link">Натисніть, щоб завантажити</span> або перетягніть файли</p>
    <p class="cf-dropzone__hint">PNG, JPG, GIF до 10MB</p></div></label>
  <input id="Photos" name="Photos" type="file" class="cf-sr-only" multiple accept="image/*" /></div>
<div class="cf-previews" id="photo-previews" hidden></div>
```
