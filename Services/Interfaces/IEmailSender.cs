namespace CityFix_backend.Services.Interfaces
{
    public interface IEmailSender
    {
        // Throws when the letter could not be handed to the mail server.
        Task SendAsync(string to, string subject, string textBody, CancellationToken cancellationToken = default);
    }
}
