using CityFix_backend.Models;
using CityFix_backend.Services;
using SkiaSharp;

namespace CityFix_backend.Tests;

public class FeedbackAndDeadlineTests
{
    private static readonly AppealOptions Options = new();
    private static readonly DateTime Now = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly User Author = new() { Id = 4, Role = UserRole.Resident, AcabId = 10 };
    private static readonly User Neighbour = new() { Id = 5, Role = UserRole.Resident, AcabId = 10 };

    private static Appeal Done(int daysAgo, int reopens = 0, int? rating = null) => new()
    {
        Id = 100, AuthorId = Author.Id, AcabId = 10, Status = AppealStatus.Done,
        ClosedAt = Now.AddDays(-daysAgo), ReopenCount = reopens, Rating = rating
    };

    [Fact]
    public void Only_the_author_rates_a_done_appeal_once_and_in_time()
    {
        Assert.True(AppealPolicy.CanRate(Author, Done(1), Now, Options));
        Assert.False(AppealPolicy.CanRate(Neighbour, Done(1), Now, Options));
        Assert.False(AppealPolicy.CanRate(Author, Done(1, rating: 5), Now, Options));
        Assert.False(AppealPolicy.CanRate(Author, Done(Options.FeedbackDays + 1), Now, Options));
        Assert.False(AppealPolicy.CanRate(Author, new Appeal { AuthorId = Author.Id, Status = AppealStatus.Rejected, ClosedAt = Now }, Now, Options));
    }

    [Fact]
    public void Reopening_is_limited()
    {
        Assert.True(AppealPolicy.CanReopen(Author, Done(1, reopens: Options.MaxReopens - 1), Now, Options));
        Assert.False(AppealPolicy.CanReopen(Author, Done(1, reopens: Options.MaxReopens), Now, Options));
    }

    [Fact]
    public void Neighbours_support_open_appeals_but_not_their_own()
    {
        var open = new Appeal { AuthorId = Author.Id, Status = AppealStatus.InProgress };
        Assert.True(AppealPolicy.CanSupport(Neighbour, open));
        Assert.False(AppealPolicy.CanSupport(Author, open));
        Assert.False(AppealPolicy.CanSupport(Neighbour, Done(1)));
        Assert.False(AppealPolicy.CanSupport(new User { Id = 1, Role = UserRole.Administrator }, open));
        Assert.False(AppealPolicy.CanSupport(new User { Id = 2, Role = UserRole.AcabAdmin, AcabId = 10 }, open));
    }

    [Theory]
    [InlineData(AppealPriority.Urgent, 2)]
    [InlineData(AppealPriority.Normal, 7)]
    [InlineData(AppealPriority.Low, 14)]
    public void Deadline_follows_the_priority(AppealPriority priority, int days)
    {
        Assert.Equal(TimeSpan.FromDays(days), Options.DueIn(priority));
    }

    [Fact]
    public void Only_open_appeals_past_the_deadline_are_overdue()
    {
        Assert.True(new Appeal { Status = AppealStatus.InProgress, DueAt = Now.AddHours(-1) }.IsOverdue(Now));
        Assert.False(new Appeal { Status = AppealStatus.InProgress, DueAt = Now.AddHours(1) }.IsOverdue(Now));
        Assert.False(new Appeal { Status = AppealStatus.Done, DueAt = Now.AddHours(-1) }.IsOverdue(Now));
        Assert.False(new Appeal { Status = AppealStatus.New }.IsOverdue(Now));
    }
}

public class ThumbnailTests
{
    private static byte[] Image(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.SteelBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private static (int Width, int Height) SizeOf(byte[] jpeg)
    {
        using var bitmap = SKBitmap.Decode(jpeg);
        return (bitmap.Width, bitmap.Height);
    }

    [Fact]
    public void Large_photos_shrink_to_the_maximum_side_keeping_proportions()
    {
        var thumbnail = Thumbnails.Create(Image(1200, 600, SKEncodedImageFormat.Png));
        Assert.NotNull(thumbnail);
        Assert.Equal((Thumbnails.MaxSide, Thumbnails.MaxSide / 2), SizeOf(thumbnail));
        Assert.Equal([0xFF, 0xD8], thumbnail[..2]);
    }

    [Fact]
    public void Small_photos_are_not_enlarged()
    {
        Assert.Equal((200, 100), SizeOf(Thumbnails.Create(Image(200, 100, SKEncodedImageFormat.Jpeg))!));
    }

    [Fact]
    public void Exif_orientation_is_applied()
    {
        // APP1 with a little-endian TIFF block holding only Orientation = 6 (rotate 90° clockwise).
        byte[] tiff = [(byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        byte[] payload = [.. "Exif\0\0"u8.ToArray(), .. tiff];
        byte[] app1 = [0xFF, 0xE1, (byte)((payload.Length + 2) >> 8), (byte)(payload.Length + 2), .. payload];
        var jpeg = Image(200, 100, SKEncodedImageFormat.Jpeg);
        byte[] rotated = [.. jpeg[..2], .. app1, .. jpeg[2..]];

        Assert.Equal((100, 200), SizeOf(Thumbnails.Create(rotated)!));
    }

    [Fact]
    public void Undecodable_data_gives_no_thumbnail()
    {
        Assert.Null(Thumbnails.Create("not an image"u8.ToArray()));
    }
}
