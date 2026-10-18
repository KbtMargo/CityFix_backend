namespace CityFix_backend.Services
{
    // The three ways a service call can fail. Services throw them; ServiceExceptionFilter turns
    // NotFound into 404 and Forbidden into 403 everywhere, and controllers turn validation failures
    // into a ModelState error on Field (MessageKey is a localization key from SharedResource*.resx).

    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message) : base(message) { }
    }

    public class ServiceValidationException : Exception
    {
        public string Field { get; }
        public string MessageKey { get; }
        public object[] Arguments { get; }

        public ServiceValidationException(string field, string messageKey, params object[] arguments) : base(messageKey)
        {
            Field = field;
            MessageKey = messageKey;
            Arguments = arguments;
        }
    }
}
