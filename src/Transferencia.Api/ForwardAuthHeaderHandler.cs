using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Transferencia.Api.Http
{
    public sealed class ForwardAuthHeaderHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ForwardAuthHeaderHandler(IHttpContextAccessor httpContextAccessor)
            => _httpContextAccessor = httpContextAccessor;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var auth = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrWhiteSpace(auth) && !request.Headers.Contains("Authorization"))
            {
                // repassa o token do usuário que chamou a Transferencia.Api
                request.Headers.TryAddWithoutValidation("Authorization", auth);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}