using MediatR;

namespace Transferencia.Application.Transfer
{
    public sealed record TransferirCommand(
        string NumeroContaDestino,
        decimal Valor,
        string? IdempotencyKeyFromHeader,
        string NumeroContaOrigem // vem do token (claim)
    ) : IRequest<TransferirResult>;

    public sealed record TransferirResult(string TransferenciaId);
}
