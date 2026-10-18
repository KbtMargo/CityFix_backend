# Analytics
The analytics pages (`admin/analytics`, `osbb/analytics`, `dashboard/analytics`).

**Layout**
- A 30px bold "Аналітика" title, with an outline Експорт button that opens the export Dialog.
- 4 StatCards (see KpiCard).
- Chart cards in `.cf-grid-2`: Card with 24px padding, a semibold `h5` (`.cf-card__h`, 16px bottom margin), and a 300px chart box.

**Charts (Recharts in the source)**
- Grid lines: dashed (3 3) #ccc, horizontal only.
- Ticks: #666 at 12px. Axis lines and tick lines are off.
- Bars: #3B82F6 with the top corners rounded to 4px.
- Line: #3B82F6, 2px, monotone.
- Status colours (`getStatusColor`): Нові #3B82F6, В роботі #F59E0B, Виконані #10B981, Відхилені #EF4444, other #6B7280.
- Category colours, in order: #3B82F6, #10B981, #F59E0B, #EF4444, #8B5CF6, #06B6D4.
- These are the `series-*` tokens. For Razor, Chart.js with the same values reproduces the look.

```cshtml
<div class="cf-grid-2">
  <div class="cf-card cf-card--p6"><h5 class="cf-card__h">Найпоширеніші проблеми (Стовпці)</h5><div class="cf-chart-box"><canvas id="common"></canvas></div></div>
</div>
<script>
new Chart(document.getElementById('common'), { type: 'bar', data: { labels: @Json.Serialize(Model.Labels), datasets: [{ data: @Json.Serialize(Model.Totals), backgroundColor: '#3B82F6', borderRadius: { topLeft: 4, topRight: 4 } }] },
  options: { maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { x: { grid: { display: false }, ticks: { color: '#666', font: { size: 12 } } }, y: { grid: { color: '#ccc' }, border: { dash: [3, 3] }, ticks: { color: '#666', precision: 0 } } } } });
</script>
```
