namespace CityFix_backend.Services.Interfaces
{
    public interface ISmsSender
    {
        // Throws when the provider refused the message.
        Task SendAsync(string phone, string text, CancellationToken cancellationToken = default);
    }
}
