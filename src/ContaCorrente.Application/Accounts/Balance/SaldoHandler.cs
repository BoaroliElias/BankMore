using BuildingBlocks.Errors;
using Dapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContaCorrente.Application.Accounts.Balance
{
    public sealed class SaldoHandler : IRequestHandler<SaldoQuery, SaldoResult>
    {
        private readonly IDbConnection _conn;

        public SaldoHandler(IDbConnection conn)
        {
            _conn = conn;
            if (_conn.State != ConnectionState.Open)
                _conn.Open();
        }

        private sealed record Row(string NumeroConta, string Nome, bool Ativo, decimal TotalCreditos, decimal TotalDebitos);

        public async Task<SaldoResult> Handle(SaldoQuery req, CancellationToken ct)
        {
            const string sql = """ 
                                   select 
                                          c.numero::text as NumeroConta,
                                          c.nome        as Nome,
                                          c.ativo       as Ativo,
                                          coalesce(sum(case when m.tipomovimento = 'C' then m.valor end), 0)::numeric(18,2) as TotalCreditos,
                                          coalesce(sum(case when m.tipomovimento = 'D' then m.valor end), 0)::numeric(18,2) as TotalDebitos
                                     from contacorrente.contacorrente c
                                     left join contacorrente.movimento m
                                          on m.idcontacorrente = c.idcontacorrente
                                    where c.idcontacorrente = @Id
                                    group by c.numero, c.nome, c.ativo
                               """;

            var row = await _conn.QuerySingleOrDefaultAsync<Row>(
                new CommandDefinition(sql, new { Id = req.ContaId }, cancellationToken: ct));

            if (row is null)
                throw new InvalidAccountException("Conta inexistente.");

            if (!row.Ativo)
                throw new InactiveAccountException("Conta inativa.");

            var saldo = row.TotalCreditos - row.TotalDebitos;
            return new SaldoResult(row.NumeroConta, row.Nome, DateTime.UtcNow, decimal.Round(saldo, 2), row.Ativo);
        }
    }
}
