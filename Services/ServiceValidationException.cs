namespace CityFix_backend.Services
{
    // A business rule was broken. Controllers turn it into a ModelState error on Field;
    // MessageKey is a localization key from Resources/SharedResource*.resx.
    public class ServiceValidationException : Exception
    {
        public string Field { get; }
        public string MessageKey { get; }

        public ServiceValidationException(string field, string messageKey) : base(messageKey)
        {
            Field = field;
            MessageKey = messageKey;
        }
    }
}
