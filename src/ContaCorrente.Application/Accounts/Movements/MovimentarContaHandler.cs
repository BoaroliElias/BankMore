using BuildingBlocks.Errors;
using Dapper;
using MediatR;
using Npgsql;
using System.Data;
using System.Text.Json;

namespace ContaCorrente.Application.Accounts.Movements
{
    public sealed class MovimentarContaHandler : IRequestHandler<MovimentarContaCommand, Unit>
    {
        private readonly IDbConnection _conn;
        public MovimentarContaHandler(IDbConnection conn) => _conn = conn;

        private sealed record ContaRow(Guid Id, string Numero, bool Ativo);

        public async Task<Unit> Handle(MovimentarContaCommand req, CancellationToken ct)
        {
            // Normaliza tipo
            var tipo = char.ToUpperInvariant(req.Tipo);
            {
                if (tipo != 'C' && tipo != 'D')
                {
                    throw new InvalidTypeException("Tipo inválido. Use 'C' ou 'D'.");
                }


                if (req.Valor <= 0m)
                {
                    throw new InvalidValueException("Valor deve ser positivo.");
                }


                // Abrir conexão e iniciar transação
                if (_conn.State != ConnectionState.Open)
                {
                    await ((NpgsqlConnection)_conn).OpenAsync(ct);
                }

                using var tx = await ((NpgsqlConnection)_conn).BeginTransactionAsync(ct);

                // 1) Idempotência: tenta registrar a chave; se já existir, curto-circuita
                var reqJson = JsonSerializer.Serialize(new
                {
                    tipo,
                    valor = req.Valor,
                    numeroConta = req.NumeroConta
                });

                var inserted = await _conn.ExecuteAsync(
                    new CommandDefinition(
                        """
                        insert into contacorrente.idempotencia (chave_idempotencia, requisicao, resultado)
                        values (@k, @req::jsonb, null)
                        on conflict (chave_idempotencia) do nothing
                     """,
                        new { k = req.IdempotencyKey, req = reqJson },
                        transaction: tx,
                        cancellationToken: ct));

                if (inserted == 0)
                {
                    // Já processado anteriormente → idempotente: nada a fazer
                    await tx.CommitAsync(ct);
                    return Unit.Value;
                }

                // 2) Carrega conta do token (origem)
                var contaOrigem = await _conn.QuerySingleOrDefaultAsync<ContaRow>(
                    new CommandDefinition(
                        """
                        select idcontacorrente as Id, numero::text as Numero, ativo as Ativo
                          from contacorrente.contacorrente
                         where idcontacorrente = @id
                         limit 1
                    """,
                        new { id = req.ContaIdDoToken }, transaction: tx, cancellationToken: ct));

                if (contaOrigem is null)
                {
                    throw new InvalidAccountException("Conta do token inexistente.");
                }


                if (!contaOrigem.Ativo)
                {
                    throw new InactiveAccountException("Conta do token inativa.");
                }

                // 3) Resolve conta alvo (se NumeroConta não informado, usa a própria)
                ContaRow contaAlvo = contaOrigem;

                if (!string.IsNullOrWhiteSpace(req.NumeroConta))
                {
                    contaAlvo = await _conn.QuerySingleOrDefaultAsync<ContaRow>(
                        new CommandDefinition(
                            """
                            select idcontacorrente as Id, numero::text as Numero, ativo as Ativo
                              from contacorrente.contacorrente
                             where numero = @numero
                             limit 1
                        """,
                            new { numero = req.NumeroConta!.Trim() }, transaction: tx, cancellationToken: ct))
                        ?? throw new InvalidAccountException("Conta informada não existe.");

                    if (!contaAlvo.Ativo)
                    {
                        throw new InactiveAccountException("Conta informada está inativa.");
                    }

                    // Regra: se a conta alvo é diferente do usuário logado, só permite CRÉDITO
                    if (contaAlvo.Id != contaOrigem.Id && tipo == 'D')
                    {
                        throw new InvalidTypeException("Débito não permitido em conta de terceiros.");
                    }

                }

                // 4) Persiste movimento (id novo por requisição)
                var movimentoId = Guid.NewGuid();

                await _conn.ExecuteAsync(
                    new CommandDefinition(
                        """
                        insert into contacorrente.movimento
                            (idmovimento, idcontacorrente, datamovimento, tipomovimento, valor)
                        values
                            (@id, @conta, now(), @tipo, @valor)
                    """,
                        new { id = movimentoId, conta = contaAlvo.Id, tipo, valor = decimal.Round(req.Valor, 2) },
                        transaction: tx, cancellationToken: ct));

                // 5) Atualiza resultado da idempotência (opcional)
                await _conn.ExecuteAsync(
                    new CommandDefinition(
                        "update contacorrente.idempotencia set resultado = '{\"status\":\"no-content\"}'::jsonb where chave_idempotencia = @k",
                        new { k = req.IdempotencyKey }, transaction: tx, cancellationToken: ct));

                await tx.CommitAsync(ct);
                return Unit.Value;
            }
        }
    }
}
