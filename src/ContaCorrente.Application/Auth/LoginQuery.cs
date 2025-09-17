using MediatR;

namespace ContaCorrente.Application.Auth
{
    public sealed record LoginQuery(string Usuario, string Senha) : IRequest<LoginResult>;
    public sealed record LoginResult(string Token, DateTime ExpiresAt);

}
