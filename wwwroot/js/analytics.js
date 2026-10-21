(function () {
    var source = document.getElementById('analytics-data');
    if (!source || !window.Chart) return;
    var data = JSON.parse(source.textContent);

    var css = getComputedStyle(document.documentElement);
    function token(name) { return css.getPropertyValue(name).trim(); }
    var series = {
        blue: token('--series-blue'), emerald: token('--series-emerald'), amber: token('--series-amber'),
        red: token('--series-red'), violet: token('--series-violet'), cyan: token('--series-cyan'), gray: token('--series-gray')
    };
    var categoryColors = [series.blue, series.emerald, series.amber, series.red, series.violet, series.cyan];
    var statusColor = {
        new: series.blue, underreview: series.cyan, needsinfo: series.violet, inprogress: series.amber,
        onhold: series.gray, done: series.emerald, rejected: series.red, duplicate: series.gray
    };
    var statusColors = (data.statusKeys || []).map(function (k) { return statusColor[k] || series.gray; });
    var priorityColors = [series.gray, series.blue, series.red];

    var scales = {
        x: { grid: { display: false }, border: { display: false }, ticks: { color: '#666', font: { size: 12 } } },
        y: { beginAtZero: true, grid: { color: '#ccc', drawTicks: false }, border: { display: false, dash: [3, 3] }, ticks: { color: '#666', font: { size: 12 }, precision: 0 } }
    };
    var base = { maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: scales };

    function bars(id, set, colors) {
        new Chart(document.getElementById(id), {
            type: 'bar',
            data: { labels: set.labels, datasets: [{ data: set.values, backgroundColor: colors, borderRadius: { topLeft: 4, topRight: 4 }, borderSkipped: false }] },
            options: base
        });
    }

    bars('chart-category', data.byCategory, data.byCategory.labels.map(function (_, i) { return categoryColors[i % categoryColors.length]; }));
    bars('chart-status', data.byStatus, statusColors);
    bars('chart-priority', data.byPriority, priorityColors);

    new Chart(document.getElementById('chart-day'), {
        type: 'line',
        data: { labels: data.perDay.labels, datasets: [{ data: data.perDay.values, borderColor: series.blue, backgroundColor: series.blue, borderWidth: 2, tension: 0.4, pointRadius: 3 }] },
        options: base
    });
})();
