# StatusTimeline
"Часова шкала статусу" from ReportDetailModal: a 2px `line` rule on the left, with 24px dots each ringed by 8px of the surface colour (`timeline-ring`). Next to each dot are a semibold title and a 12px slate-500 timestamp (`toLocaleString("uk-UA")`, so `dd.MM.yyyy, HH:mm:ss`).

**Entries**
- The first entry is always "Звернення подано": a slate-500 dot with a white file-text icon.
- Each update follows in order, oldest first, as in the source.

**Dots**
- slate-500 by default.
- `--inprogress`: orange-500.
- `--done`: green-500. This is the source's `getStatusDot` mapping.
- `__comment` adds the update note, if there is one.

```cshtml
<div class="cf-timeline">
  <div class="cf-timeline__item"><div class="cf-timeline__dot"><span class="cf-icon cf-icon--file-text cf-icon--3"></span></div>
    <div class="cf-timeline__content"><p class="cf-timeline__title">Звернення подано</p><p class="cf-timeline__time">@r.CreatedAt.ToString("dd.MM.yyyy, HH:mm:ss")</p></div></div>
  @foreach (var u in r.Updates) {
    <div class="cf-timeline__item"><div class="cf-timeline__dot cf-timeline__dot--@u.Status.ToString().Replace("_","").ToLowerInvariant()"></div>
      <div class="cf-timeline__content"><p class="cf-timeline__title">@u.Description</p><p class="cf-timeline__time">@u.CreatedAt.ToString("dd.MM.yyyy, HH:mm:ss")</p></div></div>
  }
</div>
```
