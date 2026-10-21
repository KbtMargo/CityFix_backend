namespace CityFix_backend.Services
{
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
