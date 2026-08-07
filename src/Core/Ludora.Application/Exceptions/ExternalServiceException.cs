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

        public ExternalServiceException(
            string message,
            int statusCode,
            int? retryAfterSeconds = null)
            : base(message)
        {
            StatusCode = statusCode;
            RetryAfterSeconds = retryAfterSeconds;
        }

        public ExternalServiceException(
            string message,
            Exception innerException,
            int statusCode,
            int? retryAfterSeconds = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            RetryAfterSeconds = retryAfterSeconds;
        }

        public int? StatusCode { get; }
        public int? RetryAfterSeconds { get; }
    }
}
