namespace CityFix_backend.Services.Interfaces
{
    public interface ISmsSender
    {
        Task SendAsync(string phone, string text, CancellationToken cancellationToken = default);
    }
}
