namespace CityFix_backend.Services.Interfaces
{
    public record EmailMessage(string Subject, string Text, string Html);

    public interface IEmailSender
    {
        Task SendAsync(string to, EmailMessage message, CancellationToken cancellationToken = default);
    }
}
