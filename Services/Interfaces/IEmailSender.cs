namespace CityFix_backend.Services.Interfaces
{
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string textBody, CancellationToken cancellationToken = default);
    }
}
