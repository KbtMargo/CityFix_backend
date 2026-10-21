namespace CityFix_backend.ViewModels
{
    public record DeleteDialogModel(
        int Id,
        string Title,
        string Text,
        string ConfirmLabel,
        string CancelAction,
        object? CancelRoute = null,
        string? BlockedTitle = null,
        string? BlockedText = null)
    {
        public bool Blocked => BlockedText != null;
    }
}
