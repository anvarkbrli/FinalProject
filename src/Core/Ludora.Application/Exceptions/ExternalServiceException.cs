namespace Ludora.Application.Exceptions
{
    public sealed class ExternalServiceException : Exception
    {
        public ExternalServiceException(string message)
            : base(message)
        {
        }

        public ExternalServiceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
