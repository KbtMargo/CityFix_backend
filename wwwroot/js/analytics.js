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

    // Staff section: inflow vs outflow, residents' ratings, resolution time by category.
    var flow = document.getElementById('chart-flow');
    if (flow && data.flow) {
        new Chart(flow, {
            type: 'bar',
            data: {
                labels: data.flow.labels,
                datasets: [
                    { label: data.labels.created, data: data.flow.created, backgroundColor: series.blue, borderRadius: 4, borderSkipped: false },
                    { label: data.labels.closed, data: data.flow.closed, backgroundColor: series.emerald, borderRadius: 4, borderSkipped: false }
                ]
            },
            options: Object.assign({}, base, {
                plugins: { legend: { display: true, position: 'top', align: 'end', labels: { boxWidth: 10, boxHeight: 10, useBorderRadius: true, borderRadius: 2 } } },
                interaction: { mode: 'index', intersect: false }
            })
        });
    }

    var ratings = document.getElementById('chart-ratings');
    if (ratings && data.ratings) {
        bars('chart-ratings', data.ratings, [series.red, series.amber, series.gray, series.cyan, series.emerald]);
    }

    var resolution = document.getElementById('chart-resolution');
    if (resolution && data.resolution) {
        // One row per category; the height follows the number of rows so long lists stay readable.
        var box = resolution.parentElement;
        box.style.height = Math.max(160, Number(box.dataset.rows || 1) * 36 + 40) + 'px';
        new Chart(resolution, {
            type: 'bar',
            data: { labels: data.resolution.labels, datasets: [{ data: data.resolution.values, backgroundColor: series.violet, borderRadius: 4, borderSkipped: false, barThickness: 18 }] },
            options: Object.assign({}, base, {
                indexAxis: 'y',
                scales: {
                    x: { beginAtZero: true, grid: { color: '#ccc', drawTicks: false }, border: { display: false }, ticks: { color: '#666', font: { size: 12 } } },
                    y: { grid: { display: false }, border: { display: false }, ticks: { color: '#666', font: { size: 12 } } }
                },
                plugins: { legend: { display: false }, tooltip: { callbacks: { label: function (c) { return c.parsed.x + ' ' + data.labels.days; } } } }
            })
        });
    }
})();
