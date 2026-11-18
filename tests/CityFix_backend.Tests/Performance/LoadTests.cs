using System.Diagnostics;
using System.Net;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using Xunit.Abstractions;

namespace CityFix_backend.Tests.Performance;

/// <summary>A city-sized data set: 2 500 appeals across two associations, 40 residents.</summary>
public sealed class LoadedCity : IAsyncLifetime
{
    public const int AppealCount = 2_500;

    public CityFixApp App { get; } = new();
    public Neighbourhood Main { get; private set; } = null!;
    public List<User> Residents { get; } = new();

    public async Task InitializeAsync()
    {
        await App.InitializeAsync();
        Main = await App.DataAsync(d => d.NeighbourhoodAsync());
        for (var i = 0; i < 40; i++) Residents.Add(await App.DataAsync(d => d.UserAsync(acab: Main.Acab)));
        var other = await App.DataAsync(d => d.NeighbourhoodAsync());

        await App.DbAsync(async (db, _) =>
        {
            var random = new Random(42);
            var now = DateTime.UtcNow;
            var statuses = Enum.GetValues<AppealStatus>();
            for (var i = 0; i < AppealCount; i++)
            {
                var author = i % 5 == 0 ? other.Resident : Residents[i % Residents.Count];
                var status = statuses[random.Next(statuses.Length)];
                var created = now.AddHours(-random.Next(24 * 180));
                db.Appeals.Add(new Appeal
                {
                    Title = $"Звернення №{i} про яму", Description = "Опис проблеми для навантажувального тесту",
                    Lat = (decimal)Math.Round(49.38 + random.NextDouble() * 0.08, 5), Lng = (decimal)Math.Round(26.93 + random.NextDouble() * 0.12, 5),
                    Address = $"вул. Тестова, {random.Next(1, 60)}", Status = status, Priority = (AppealPriority)random.Next(3),
                    AuthorId = author.Id, AcabId = author.AcabId, CategoryId = i % 2 == 0 ? Main.Category.Id : other.Category.Id,
                    ResponsibleKind = ResponsibleKind.Association, CreatedAt = created, UpdatedAt = created, DueAt = created.AddDays(7),
                    ClosedAt = AppealWorkflow.Closed.Contains(status) ? created.AddDays(random.Next(1, 10)) : null,
                    Rating = status == AppealStatus.Done && random.Next(2) == 0 ? random.Next(1, 6) : null
                });
            }
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("ANALYZE \"Appeals\"");
            return 0;
        });
    }

    public Task DisposeAsync() => App.DisposeAsync();
}

[Trait("Category", "Performance")]
public class LoadTests(LoadedCity city, ITestOutputHelper output) : IClassFixture<LoadedCity>
{
    private static async Task<TimeSpan> MedianAsync(Func<Task> run, int times = 5)
    {
        await run();
        var samples = new List<TimeSpan>();
        for (var i = 0; i < times; i++)
        {
            var watch = Stopwatch.StartNew();
            await run();
            samples.Add(watch.Elapsed);
        }
        return samples.Order().ElementAt(times / 2);
    }

    [Fact]
    public async Task Appeal_lists_stay_fast_with_thousands_of_appeals()
    {
        var head = await city.App.SignInAsync(city.Main.Head.Email);
        foreach (var url in new[] { "/Appeals", "/Appeals?tab=closed&page=50", "/Appeals?q=%D1%8F%D0%BC%D0%B0", "/Dashboard" })
        {
            var median = await MedianAsync(async () => Assert.Equal(HttpStatusCode.OK, (await head.GetAsync(url)).StatusCode));
            output.WriteLine($"{url}: median {median.TotalMilliseconds:0} ms");
            Assert.True(median < TimeSpan.FromSeconds(2), $"{url} took {median.TotalMilliseconds:0} ms");
        }
    }

    [Fact]
    public async Task Analytics_for_the_whole_city_render_quickly()
    {
        var admin = await city.App.SignInAsync((await city.App.DataAsync(d => d.AdminAsync())).Email);
        var median = await MedianAsync(async () => Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Analytics")).StatusCode), 3);
        output.WriteLine($"/Analytics (admin, {LoadedCity.AppealCount} appeals): {median.TotalMilliseconds:0} ms");
        Assert.True(median < TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Fifty_parallel_users_are_all_served()
    {
        var clients = new List<HttpClient>();
        foreach (var resident in city.Residents.Take(10)) clients.Add(await city.App.SignInAsync(resident.Email));

        var watch = Stopwatch.StartNew();
        var timings = await Task.WhenAll(Enumerable.Range(0, 50).Select(async i =>
        {
            var started = Stopwatch.StartNew();
            var response = await clients[i % clients.Count].GetAsync(i % 2 == 0 ? "/Dashboard" : "/Appeals");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return started.Elapsed;
        }));

        var p95 = timings.Order().ElementAt((int)(timings.Length * 0.95) - 1);
        output.WriteLine($"50 parallel requests: total {watch.ElapsedMilliseconds} ms, p95 {p95.TotalMilliseconds:0} ms");
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30));
        Assert.True(p95 < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Full_export_of_the_association_finishes_in_seconds()
    {
        var head = await city.App.SignInAsync(city.Main.Head.Email);
        foreach (var format in new[] { "xlsx", "csv", "pdf" })
        {
            var watch = Stopwatch.StartNew();
            var response = await head.GetAsync($"/Appeals/Export?format={format}");
            var size = (await response.Content.ReadAsByteArrayAsync()).Length;
            output.WriteLine($"export {format}: {watch.ElapsedMilliseconds} ms, {size / 1024} KB");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20));
        }
    }

    [Fact]
    public async Task Nearby_search_is_fast_and_can_use_the_spatial_index()
    {
        await using var app = new ServiceHarness(city.App.Database);
        var appeals = app.Get<IAppealService>();
        var resident = city.Residents[0];

        var median = await MedianAsync(() => appeals.FindSimilarAsync(49.42, 26.99, city.Main.Category.Id, null, resident), 10);
        output.WriteLine($"similar-appeal search: median {median.TotalMilliseconds:0} ms");
        Assert.True(median < TimeSpan.FromMilliseconds(500));

        var point = new Point(26.99, 49.42) { SRID = 4326 };
        var sql = app.Db.Appeals.Where(a => a.Location!.IsWithinDistance(point, 500)).Select(a => a.Id).ToQueryString();
        await using var connection = new Npgsql.NpgsqlConnection(city.App.Database.ConnectionString);
        await connection.OpenAsync();
        await using (var off = new Npgsql.NpgsqlCommand("SET enable_seqscan = off", connection)) await off.ExecuteNonQueryAsync();
        var plan = new List<string>();
        await using (var explain = new Npgsql.NpgsqlCommand("EXPLAIN " + System.Text.RegularExpressions.Regex.Replace(sql, @"@\w*point\w*","ST_GeomFromText('POINT(26.99 49.42)', 4326)::geography"), connection))
        await using (var reader = await explain.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) plan.Add(reader.GetString(0));
        }
        output.WriteLine(string.Join("\n", plan));
        Assert.Contains(plan, line => line.Contains("IX_Appeals_Location"));
    }

    [Fact]
    public async Task Overdue_scan_handles_a_backlog_in_batches()
    {
        await using var app = new ServiceHarness(city.App.Database, now: DateTimeOffset.UtcNow.AddYears(1));
        var appeals = app.Get<IAppealService>();
        var watch = Stopwatch.StartNew();
        var first = await appeals.NotifyOverdueAsync();
        output.WriteLine($"first overdue batch: {first} appeals in {watch.ElapsedMilliseconds} ms");
        Assert.Equal(100, first);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }
}
