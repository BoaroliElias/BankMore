using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BuildingBlocks;
using BuildingBlocks.Security;
using Dapper;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ContaCorrente.Application.Auth
{
    public sealed class LoginHandler : IRequestHandler<LoginQuery, LoginResult>
    {
        private readonly IDbConnection _conn;
        private readonly JwtOptions _jwt;

        private sealed record ContaRow(
            Guid Id,        // uuid
            string Numero,    // cast pra text no SQL
            string Nome,
            string Senha,     // hash base64
            string Salt,      // salt base64
            bool Ativo
        );

        public LoginHandler(IDbConnection conn, IOptions<JwtOptions> jwt)
        {
            _conn = conn;
            _jwt = jwt.Value;
        }

        public async Task<LoginResult> Handle(LoginQuery req, CancellationToken ct)
        {
            var login = req.Usuario?.Trim() ?? "";
            var digits = new string(login.Where(char.IsDigit).ToArray());
            var eCpf = digits.Length == 11;

            var sql = @"
                        select
                              idcontacorrente                as Id,
                              numero::text                   as Numero,
                              nome                           as Nome,
                              senha                          as Senha,
                              salt                           as Salt,
                              ativo                          as Ativo
                         from contacorrente.contacorrente
                        where " + (eCpf ? "cpf = @cpf" : "numero::text = @numero") + " limit 1";

            var row = await _conn.QuerySingleOrDefaultAsync<ContaRow>(
                new CommandDefinition(sql, new { cpf = digits, numero = login }, cancellationToken: ct));

            if (row is null)
                throw new UnauthorizedAccessException("Credenciais inválidas.");

            if (!row.Ativo)
                throw new UnauthorizedAccessException("Conta inativa.");

            // Verifica senha (PBKDF2 via helper; com fallback p/ legado)
            if (!PasswordHasher.Verify(req.Senha, row.Salt, row.Senha))
                throw new UnauthorizedAccessException("Credenciais inválidas.");

            // Claims
            var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, row.Id.ToString()), // sub = Guid
            new("acc_number", row.Numero),
            new("name", row.Nome)
        };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(_jwt.ExpiresMinutes);

            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: creds);

            var tokenStr = new JwtSecurityTokenHandler().WriteToken(token);
            return new LoginResult(tokenStr, expires);
        }
    }
}

// Aceitamos CPF(11 dígitos) ou número da conta.
// Senha verificada com o mesmo PBKDF2 usado no cadastro.
// UnauthorizedAccessException vira 401 USER_UNAUTHORIZED no controller.