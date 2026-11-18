using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CityFix_backend.Tests.Unit;

[Trait("Category", "Unit")]
public class FileValidationTests
{
    private static FileService Service(string root, int maxPhotos = 2, long maxBytes = 1024 * 1024) =>
        new(null!, new TestWebHostEnvironment(root), Options.Create(new UploadOptions { MaxPhotos = maxPhotos, MaxBytes = maxBytes }), TimeProvider.System);

    private static readonly FileService Files = Service(Path.GetTempPath());

    private static ServiceValidationException Rejected(params IFormFile[] photos) =>
        Assert.Throws<ServiceValidationException>(() => Files.ValidatePhotos(photos));

    [Theory]
    [InlineData(SkiaSharp.SKEncodedImageFormat.Png)]
    [InlineData(SkiaSharp.SKEncodedImageFormat.Jpeg)]
    [InlineData(SkiaSharp.SKEncodedImageFormat.Webp)]
    public void Real_images_are_accepted(SkiaSharp.SKEncodedImageFormat format)
    {
        Files.ValidatePhotos([TestFiles.FormFile(TestFiles.Image(format: format))]);
    }

    [Fact]
    public void Gif_is_recognised_by_its_signature()
    {
        Files.ValidatePhotos([TestFiles.FormFile([.. "GIF89a"u8.ToArray(), 1, 0, 1, 0, 0, 0], "a.gif")]);
    }

    [Fact]
    public void Type_is_decided_by_content_not_by_name()
    {
        var ex = Rejected(TestFiles.FormFile("%PDF-1.7 fake"u8.ToArray(), "photo.jpg", "image/jpeg"));
        Assert.Equal(("Photos", "Validation.PhotoType"), (ex.Field, ex.MessageKey));
        Rejected(TestFiles.FormFile("<svg onload=alert(1)>"u8.ToArray(), "x.png"));
    }

    [Fact]
    public void Riff_files_that_are_not_webp_are_rejected()
    {
        Assert.Equal("Validation.PhotoType", Rejected(TestFiles.FormFile([.. "RIFF"u8.ToArray(), 0, 0, 0, 0, .. "WAVE"u8.ToArray()], "a.webp")).MessageKey);
    }

    [Fact]
    public void Empty_and_oversized_files_are_rejected()
    {
        Assert.Equal("Validation.PhotoTooLarge", Rejected(TestFiles.FormFile([])).MessageKey);
        var big = Service(Path.GetTempPath(), maxBytes: 100);
        var ex = Assert.Throws<ServiceValidationException>(() => big.ValidatePhotos([TestFiles.FormFile(TestFiles.Image())]));
        Assert.Equal("Validation.PhotoTooLarge", ex.MessageKey);
    }

    [Fact]
    public void Photo_count_includes_already_attached_photos()
    {
        var photo = TestFiles.FormFile(TestFiles.Image());
        Files.ValidatePhotos([photo], alreadyAttached: 1);
        var ex = Assert.Throws<ServiceValidationException>(() => Files.ValidatePhotos([photo], alreadyAttached: 2));
        Assert.Equal(("Validation.PhotosTooMany", 2), (ex.MessageKey, (int)ex.Arguments[0]));
    }

    [Theory]
    [InlineData("../../../Windows/win.ini")]
    [InlineData("uploads/../../secret.txt")]
    [InlineData("C:/Windows/win.ini")]
    public void Stored_paths_cannot_escape_the_upload_folder(string path)
    {
        var file = new AppealFile { Id = 1, FilePath = path, ContentType = "image/png", FileName = "x" };
        Assert.Throws<NotFoundException>(() => Files.PhysicalPathOf(file));
    }

    [Fact]
    public void Stored_paths_resolve_inside_the_configured_folder()
    {
        var root = TestFiles.TempFolder();
        var service = Service(root);
        var file = new AppealFile { FilePath = "uploads/appeals/7/a.png", ThumbnailPath = "uploads/appeals/7/a_thumb.jpg" };

        var paths = service.PhysicalPathsOf(file);

        Assert.Equal([Path.Combine(root, "App_Data", "uploads", "appeals", "7", "a.png"), Path.Combine(root, "App_Data", "uploads", "appeals", "7", "a_thumb.jpg")], paths);
        Assert.Single(service.PhysicalPathsOf(new AppealFile { FilePath = "uploads/appeals/7/a.png" }));
    }
}
