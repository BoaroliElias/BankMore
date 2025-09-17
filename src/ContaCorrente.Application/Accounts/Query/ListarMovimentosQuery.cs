using MediatR;

namespace ContaCorrente.Application.Accounts
{
    public sealed record MovimentoDto(DateTime Data, char Tipo, decimal Valor);

    public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

    public sealed record ListarMovimentosQuery(
        Guid ContaId,
        DateTime? Desde = null,
        DateTime? Ate = null,
        char? Tipo = null,
        int Page = 1,
        int PageSize = 50
    ) : IRequest<PagedResult<MovimentoDto>>;

}