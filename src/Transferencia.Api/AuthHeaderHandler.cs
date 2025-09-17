using System.Net.Http.Headers;

namespace Transferencia.Api
{
    public sealed class AuthHeaderHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _http;

        public AuthHeaderHandler(IHttpContextAccessor http) => _http = http;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var auth = _http.HttpContext?.Request?.Headers["Authorization"].ToString();
            if (!string.IsNullOrWhiteSpace(auth))
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(auth);

            return base.SendAsync(request, ct);
        }
    }
}
