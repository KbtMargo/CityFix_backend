namespace CityFix_backend.Services.Interfaces
{
    // Machine translation between the two UI languages ("uk" / "en").
    public interface ITranslator
    {
        // Throws when the service is unreachable, refuses the request or its quota is used up.
        Task<string> TranslateAsync(string text, string from, string to, CancellationToken cancellationToken = default);
    }
}
