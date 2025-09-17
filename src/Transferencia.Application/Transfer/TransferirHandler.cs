using System.Data;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Transferencia.Application.Transfer
{
    public sealed class TransferirHandler : IRequestHandler<TransferirCommand, TransferirResult>
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IHttpContextAccessor _httpContext;
        private readonly ILogger<TransferirHandler> _logger;
        private readonly IDbConnection _conn;

        public TransferirHandler(
            IHttpClientFactory httpFactory,
            IHttpContextAccessor httpContext,
            ILogger<TransferirHandler> logger,
            IDbConnection conn)
        {
            _httpFactory = httpFactory;
            _httpContext = httpContext;
            _logger = logger;
            _conn = conn;
        }

        public async Task<TransferirResult> Handle(TransferirCommand req, CancellationToken ct)
        {
            var transferKey = string.IsNullOrWhiteSpace(req.IdempotencyKeyFromHeader)
                ? $"tx-{Guid.NewGuid():N}"
                : req.IdempotencyKeyFromHeader!.Trim();

            var cachedJson = await _conn.ExecuteScalarAsync<string?>(
                """
                    select resultado::text
                      from transferencia.idempotencia
                     where chave_idempotencia = @k
                """,
                new { k = transferKey });

            if (!string.IsNullOrWhiteSpace(cachedJson))
            {
                // já processado antes — devolve o mesmo resultado
                var cached = JsonSerializer.Deserialize<TransferirResult>(cachedJson!)!;
                return cached;
            }

            // ===== 1) HttpClient nomeado p/ ContaCorrente.Api (Program.cs) =====
            var http = _httpFactory.CreateClient("contacorrente");

            // repassa o Authorization do usuário que chamou a Transferencia.Api
            var authHeader = _httpContext.HttpContext?.Request?.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authHeader))
                http.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authHeader);

            // ===== 2) Valida conta de origem (saldo/ativo) na ContaCorrente.Api =====
            using (var saldoResp = await http.GetAsync("/api/contas/saldo", ct))
            {
                saldoResp.EnsureSuccessStatusCode();
                var saldo = await saldoResp.Content.ReadFromJsonAsync<SaldoDto>(cancellationToken: ct)
                            ?? throw new InvalidOperationException("Falha ao ler saldo.");

                if (!saldo.Ativo)
                    throw new InvalidOperationException("Conta de origem inativa.");

                if (saldo.Saldo < req.Valor)
                    throw new InvalidOperationException("Saldo insuficiente.");
            }

            // ===== 3) Débito na ORIGEM (idempotente) =====
            await PostMovimentoAsync(http, new MovReq
            {
                tipo = "D",
                valor = req.Valor,
                numeroConta = req.NumeroContaOrigem
            }, $"{transferKey}-D", ct);

            // ===== 4) Crédito no DESTINO (idempotente) =====
            try
            {
                await PostMovimentoAsync(http, new MovReq
                {
                    tipo = "C",
                    valor = req.Valor,
                    numeroConta = req.NumeroContaDestino
                }, $"{transferKey}-C", ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Falha ao creditar destino. Tentando compensar débito... transferKey={Key}",
                    transferKey);

                // 4.1) Compensa o débito (crédito de volta na origem, idempotente)
                try
                {
                    await PostMovimentoAsync(http, new MovReq
                    {
                        tipo = "C",
                        valor = req.Valor,
                        numeroConta = req.NumeroContaOrigem
                    }, $"{transferKey}-R", ct);
                }
                catch (Exception cex)
                {
                    _logger.LogError(cex,
                        "Falha ao compensar débito. Ação manual pode ser necessária. transferKey={Key}",
                        transferKey);
                    throw;
                }

                throw;
            }

            // ===== 5) Persistência local: transferencia.transferencia =====
            // id_origem = 'sub' do token (GUID da conta)
            var user = _httpContext.HttpContext?.User;
            string? sub = user?.FindFirst("sub")?.Value
                       ?? user?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(sub, out var idOrigem))
                throw new InvalidOperationException("Token sem 'sub' válido (id da conta de origem).");

            // id_destino: por nº da conta (tabela contacorrente.contacorrente)
            var idDestino = await _conn.ExecuteScalarAsync<Guid?>(
                """
                    select idcontacorrente
                      from contacorrente.contacorrente
                     where numero = @n
                     limit 1
                """,
                new { n = req.NumeroContaDestino });

            if (idDestino is null)
                throw new InvalidOperationException("Conta destino inexistente.");

            var transferRowId = Guid.NewGuid();

            // INSERT da transferência
            await _conn.ExecuteAsync(
                """
                insert into transferencia.transferencia (idtransferencia, idcontacorrente_origem, idcontacorrente_destino, datamovimento, valor)
                values (@id, @orig, @dest, now(), @val)
                """,
                new { id = transferRowId, orig = idOrigem, dest = idDestino.Value, val = req.Valor });

            // ===== 6) Grava idempotência da TRANSFERÊNCIA =====
            var reqJson = JsonSerializer.Serialize(new
            {
                req.NumeroContaOrigem,
                req.NumeroContaDestino,
                req.Valor
            });

            var res = new TransferirResult(transferKey);
            var resJson = JsonSerializer.Serialize(res);

            await _conn.ExecuteAsync(
                """
                insert into transferencia.idempotencia (chave_idempotencia, requisicao, resultado, created_at)
                values (@key, @req::jsonb, @res::jsonb, now())
                on conflict (chave_idempotencia) do nothing
                """,
                new { key = transferKey, req = reqJson, res = resJson });

            return res;
        }

        // =============== Helpers ===============
        private static async Task PostMovimentoAsync(
            HttpClient http, MovReq body, string idemKey, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/contas/movimentar")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.TryAddWithoutValidation("Idempotency-Key", idemKey);

            using var resp = await http.SendAsync(req, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var details = await resp.Content.ReadAsStringAsync(ct);
                // a API de ContaCorrente devolve problem+json ou um json simples; logue o conteúdo
                throw new InvalidOperationException(
                    $"Falha ao movimentar (tipo={body.tipo}, conta={body.numeroConta}, valor={body.valor}). " +
                    $"Status {(int)resp.StatusCode}. Corpo: {details}");
            }
        }

        // DTOs locais só para (de)serialização
        private sealed record MovReq
        {
            public string tipo { get; init; } = "";
            public decimal valor { get; init; }
            public string numeroConta { get; init; } = "";
        }

        private sealed record SaldoDto
        {
            public string NumeroConta { get; init; } = "";
            public string Nome { get; init; } = "";
            public bool Ativo { get; init; }
            public decimal Saldo { get; init; }
        }
    }
}
