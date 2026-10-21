namespace CityFix_backend.Services.Interfaces
{
    public interface ITranslator
    {
        Task<string> TranslateAsync(string text, string from, string to, CancellationToken cancellationToken = default);
    }
}
