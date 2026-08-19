using System.Net;

namespace Osdu.Client;

/// <summary>
/// Exception thrown when an OSDU API request fails with a non-success status code.
/// </summary>
public class OsduApiException : HttpRequestException
{
    /// <summary>
    /// Gets the response body returned by the API.
    /// </summary>
    public string ResponseBody { get; }

    /// <summary>
    /// Gets the request URL that caused the exception.
    /// </summary>
    public string RequestUrl { get; }

    public OsduApiException(HttpStatusCode statusCode, string responseBody, string requestUrl)
        : base($"HTTP {(int)statusCode} from {requestUrl}: {responseBody}", null, statusCode)
    {
        ResponseBody = responseBody;
        RequestUrl = requestUrl;
    }
}
