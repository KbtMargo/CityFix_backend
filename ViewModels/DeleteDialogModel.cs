namespace CityFix_backend.ViewModels
{
    // The panel-modal delete confirmation. When BlockedText is set the delete button is disabled
    // and the red Alert box explains why (e.g. the row still has appeals).
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
