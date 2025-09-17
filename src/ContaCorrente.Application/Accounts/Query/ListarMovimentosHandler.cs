using Dapper;
using MediatR;
using System.Data;

namespace ContaCorrente.Application.Accounts.Query
{
    public sealed class ListarMovimentosHandler
        : IRequestHandler<ListarMovimentosQuery, PagedResult<MovimentoDto>>
    {
        private readonly IDbConnection _conn;
        public ListarMovimentosHandler(IDbConnection conn) => _conn = conn;

        public async Task<PagedResult<MovimentoDto>> Handle(ListarMovimentosQuery q, CancellationToken ct)
        {
            var page = q.Page <= 0 ? 1 : q.Page;
            var size = q.PageSize is <= 0 or > 200 ? 50 : q.PageSize;
            var skip = (page - 1) * size;

            const string baseWhere = @"
            where idcontacorrente = @id
              and (@desde is null or datamovimento >= @desde)
              and (@ate   is null or datamovimento <  (@ate + interval '1 day'))
              and (@tipo  is null or tipomovimento = @tipo)";

            var total = await _conn.ExecuteScalarAsync<int>(
                new CommandDefinition($"""
                select count(1)
                  from contacorrente.movimento
                {baseWhere}
            """, new { id = q.ContaId, desde = q.Desde, ate = q.Ate, tipo = q.Tipo }, cancellationToken: ct));

            var items = (await _conn.QueryAsync<MovimentoDto>(
                new CommandDefinition($"""
                select datamovimento as Data,
                       tipomovimento as Tipo,
                       valor
                  from contacorrente.movimento
                {baseWhere}
                 order by datamovimento desc
                 limit @take offset @skip
            """, new { id = q.ContaId, desde = q.Desde, ate = q.Ate, tipo = q.Tipo, take = size, skip },
                cancellationToken: ct))).ToList();

            return new PagedResult<MovimentoDto>(items, total, page, size);
        }
    }
}
