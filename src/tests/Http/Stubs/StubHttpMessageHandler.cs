using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// No namespace on purpose so it's easy to use from tests without extra using
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _content;
    private readonly string _mediaType;

    public StubHttpMessageHandler(string content, HttpStatusCode statusCode = HttpStatusCode.OK, string mediaType = "application/json")
    {
        _statusCode = statusCode;
        _content = content;
        _mediaType = mediaType;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var resp = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content, System.Text.Encoding.UTF8, _mediaType)
        };
        return Task.FromResult(resp);
    }
}
