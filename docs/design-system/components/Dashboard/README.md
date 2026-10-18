# Dashboard
The resident dashboard (`dashboard/page.tsx`), put together from the components:
- Sidebar,
- "Панель" (36px bold, −0.025em) with the subtitle,
- 4 KpiCards,
- the search and Фільтри row,
- segmented Tabs (Усі, Нові, В роботі, Закриті),
- the IssueListItem list, 16px apart.

The main area has 24px padding and 24px vertical rhythm (`.cf-page`). Selecting an issue opens ReportDetailModal (`?reportId=`). My appeals (`my-issues`) is the same page with the title "Мої звернення" in 30px.

This card was checked against a render of the frontend's own markup and compiled CSS. Every measured box (sidebar items, input, button, tabs, list items) is identical to the pixel. The KPI row is the refreshed KpiCard, so it intentionally differs from the source.
