using System.Data;
using System.Security.Cryptography;
using BuildingBlocks.Security;
using Dapper;
using MediatR;

namespace ContaCorrente.Application.Accounts
{
    public sealed class CadastrarContaHandler : IRequestHandler<CadastrarContaCommand, CadastrarContaResult>
    {
        private readonly IDbConnection _conn;
        public CadastrarContaHandler(IDbConnection conn) => _conn = conn;

        public async Task<CadastrarContaResult> Handle(CadastrarContaCommand request, CancellationToken ct)
        {
            var cpf = OnlyDigits(request.Cpf);
            var nome = string.IsNullOrWhiteSpace(request.Nome) ? "Titular" : request.Nome.Trim();

            // CPF único
            var existe = await _conn.ExecuteScalarAsync<bool>(
                "select exists(" +
                "               select 1 " +
                "                 from contacorrente.contacorrente " +
                "                where cpf = @cpf)", new { cpf });
            if (existe) throw new InvalidOperationException("CPF já cadastrado.");

            // Nº conta único (8 dígitos)
            var numero = await GerarNumeroContaUnico();

            // Hash + salt (PBKDF2 via helper)
            var (hashB64, saltB64) = PasswordHasher.Hash(request.Senha);

            const string sql = @" insert into contacorrente.contacorrente
                                              (cpf, numero, nome, ativo, senha, salt)
                                       values 
                                              (@Cpf, @Numero, @Nome, true, @Hash, @Salt)
                                    returning numero::text;";

            var numeroConta = await _conn.ExecuteScalarAsync<string>(sql, new
            {
                Cpf = cpf,
                Numero = numero,
                Nome = nome,
                Hash = hashB64,
                Salt = saltB64
            });

            return new CadastrarContaResult(numeroConta);
        }

        private async Task<string> GerarNumeroContaUnico()
        {
            for (int i = 0; i < 10; i++)
            {
                var n = RandomNumberGenerator.GetInt32(10000000, 99999999).ToString();
                var exists = await _conn.ExecuteScalarAsync<bool>(
                    "select exists( " +
                    "              select 1 " +
                    "                from contacorrente.contacorrente" +
                    "               where numero = @n)", new { n });
                if (!exists) return n;
            }
            throw new Exception("Falha ao gerar número de conta.");
        }
        private static string OnlyDigits(string s) => new(s.Where(char.IsDigit).ToArray());
    }
}