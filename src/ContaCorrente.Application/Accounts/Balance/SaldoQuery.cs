using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContaCorrente.Application.Accounts.Balance
{
    public sealed record SaldoQuery(Guid ContaId) : IRequest<SaldoResult>;

    public sealed record SaldoResult(string NumeroConta, string Nome, DateTime DataHora, decimal Saldo, bool Ativo);
}
