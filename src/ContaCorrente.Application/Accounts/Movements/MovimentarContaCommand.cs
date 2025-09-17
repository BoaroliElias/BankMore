using MediatR;

namespace ContaCorrente.Application.Accounts.Movements
{
    public sealed record MovimentarContaCommand(
        Guid ContaIdDoToken,    // id da conta do usuário autenticado (sub)
        char Tipo,              // 'C' ou 'D'
        decimal Valor,            // > 0
        string? NumeroConta,      // opcional: se informado e != da conta do token, só 'C' é permitido
        string IdempotencyKey     // chave única enviada pelo cliente
    ) : IRequest<Unit>;
}
