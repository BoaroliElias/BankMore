using System.Data;
using Dapper;
using MediatR;
using BuildingBlocks.Security;

namespace ContaCorrente.Application.Accounts.Deactivate
{
    public sealed class InativarContaHandler : IRequestHandler<InativarContaCommand, Unit>
    {
        private readonly IDbConnection _conn;
        public InativarContaHandler(IDbConnection conn) => _conn = conn;

        private sealed record AccountRow(Guid Id, string Hash, string Salt, bool Ativo);

        public async Task<Unit> Handle(InativarContaCommand req, CancellationToken ct)
        {
            const string sql = """
                                select
                                        idcontacorrente as Id,
                                        senha           as Hash,
                                        salt            as Salt,
                                        ativo           as Ativo
                                  from contacorrente.contacorrente
                                 where idcontacorrente = @Id
                               """;

            var row = await _conn.QueryFirstOrDefaultAsync<AccountRow>(
                new CommandDefinition(sql, new { Id = req.ContaId }, cancellationToken: ct));

            if (row is null)
            {
                throw new InvalidOperationException("Conta não encontrada.");
            }
                
            if (!PasswordHasher.Verify(req.Senha, row.Salt, row.Hash))
            {
                throw new UnauthorizedAccessException("Senha inválida.");
            }
                

            if (!row.Ativo)
            {
                return Unit.Value; // idempotente
            }
                

            const string upd = """
                                    update contacorrente.contacorrente
                                       set ativo = false
                                     where idcontacorrente = @Id and ativo = true
                               """;

            await _conn.ExecuteAsync(new CommandDefinition(upd, new { Id = req.ContaId }, cancellationToken: ct));
            return Unit.Value;
        }
    }
}
