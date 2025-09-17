using MediatR;
using System;

namespace ContaCorrente.Application.Accounts.Deactivate
{
    public sealed record InativarContaCommand(Guid ContaId, string Senha) : IRequest<Unit>;

}
