using System.Net;

namespace SystemOneSharp;

public sealed class SystemOneApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ResponseBody { get; }
    public int Attempts { get; }

    public SystemOneApiException(HttpStatusCode statusCode, string responseBody, int attempts)
        : base($"System One API returned HTTP {(int)statusCode} after {attempts} attempt(s).")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        Attempts = attempts;
    }
}

public sealed class SystemOneTransportException : Exception
{
    public SystemOneTransportException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class SystemOneProtocolException : Exception
{
    public SystemOneProtocolException(string message) : base(message) { }
    public SystemOneProtocolException(string message, Exception innerException) : base(message, innerException) { }
}
