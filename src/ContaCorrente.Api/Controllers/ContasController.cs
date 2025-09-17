using BuildingBlocks;
using ContaCorrente.Application.Accounts;
using ContaCorrente.Application.Accounts.Deactivate;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ContaCorrente.Application.Accounts.Balance;
using ContaCorrente.Application.Accounts.Movements;
using BuildingBlocks.Errors;
using System.Data;
using Dapper;


namespace ContaCorrente.Api.Controllers
{
    [ApiController]
    [Route("api/contas")]
    [Produces("application/json")]
    public class ContasController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IValidator<CadastrarContaCommand> _cadastrarValidator;

        public ContasController(
               IMediator mediator,
               IValidator<CadastrarContaCommand> cadastrarValidator)
        {
            _mediator = mediator;
            _cadastrarValidator = cadastrarValidator;
        }

        // ====== DTOs (requests) ======
        public sealed record CadastrarContaRequest(string Cpf, string Senha, string Nome);
        public sealed record InativarContaRequest(string Senha);
        public sealed record MovimentarRequest(char Tipo, decimal Valor, string? NumeroConta);


        /// <summary>Cadastra uma conta corrente.</summary>
        /// <response code="201">Número da conta criada</response>
        /// <response code="400">CPF inválido ou já cadastrado</response>
        [HttpPost("cadastrar")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Cadastrar([FromBody] CadastrarContaRequest body, CancellationToken ct)
        {
            try
            {
                var cmd = new CadastrarContaCommand(body.Cpf, body.Senha, body.Nome);

                // valida com FluentValidation (CPF, senha mínima, etc.)
                await _cadastrarValidator.ValidateAndThrowAsync(cmd, ct);

                var result = await _mediator.Send(cmd, ct);
                return Created($"/api/contas/{result.NumeroConta}", new { numeroConta = result.NumeroConta });
            }
            catch (ValidationException ex)
            {
                var msg = ex.Errors?.FirstOrDefault()?.ErrorMessage ?? "Dados inválidos.";
                return BadRequest(new ApiError("INVALID_DOCUMENT", msg));
            }
            catch (InvalidOperationException ex) // ex.: CPF já cadastrado
            {
                return BadRequest(new ApiError("DUPLICATE_CPF", ex.Message));
            }
        }

        /// <summary>Inativa a conta corrente do usuário autenticado.</summary>
        /// <remarks>Requer token válido e a senha da conta.</remarks>
        /// <response code="204">Conta inativada (idempotente)</response>
        /// <response code="400">Conta inválida</response>
        /// <response code="401">Senha inválida ou token inválido</response>
        [Authorize]
        [HttpPatch("inativar")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Inativar([FromBody] InativarContaRequest body, CancellationToken ct)
        {
            var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!Guid.TryParse(sub, out var contaId))
                return Unauthorized(new ApiError("USER_UNAUTHORIZED", "Token inválido."));

            try
            {
                await _mediator.Send(new InativarContaCommand(contaId, body.Senha), ct);
                return NoContent(); // 204
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiError("INVALID_ACCOUNT", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiError("USER_UNAUTHORIZED", ex.Message));
            }
        }

        /// <summary>Consulta o saldo da conta do usuário autenticado</summary>
        /// <response code="200">Saldo atual</response>
        /// <response code="400">Conta inválida/inativa</response>
        /// <response code="401">Token inválido</response>
        [Authorize]
        [HttpGet("saldo")]
        [ProducesResponseType(typeof(object), StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Saldo(CancellationToken ct)
        {
            var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!Guid.TryParse(sub, out var contaId))
                return Unauthorized(new ApiError("USER_UNAUTHORIZED", "Token inválido."));

            try
            {
                var r = await _mediator.Send(new SaldoQuery(contaId), ct);
                return Ok(new
                {
                    numeroConta = r.NumeroConta,
                    nome = r.Nome,
                    ativo = r.Ativo,
                    dataHora = r.DataHora,
                    saldo = r.Saldo
                });
            }
            catch (InactiveAccountException ex)
            {
                return BadRequest(new ApiError("INACTIVE_ACCOUNT", ex.Message));
            }
            catch (InvalidAccountException ex)
            {
                return BadRequest(new ApiError("INVALID_ACCOUNT", ex.Message));
            }
        }

        /// <summary>Cria movimento de crédito ou débito (idempotente)</summary>
        /// <remarks>
        /// - Token obrigatório.
        /// - Header <b>Idempotency-Key</b> obrigatório.
        /// - Se <i>NumeroConta</i> não for enviado, movimenta a conta do token.
        /// - Se <i>NumeroConta</i> for diferente da conta do token, somente <b>crédito</b> é permitido.
        /// </remarks>
        /// <response code="204">Movimento registrado</response>
        /// <response code="400">Dados inválidos / regra de negócio</response>
        /// <response code="401">Token inválido</response>
        [Authorize]
        [HttpPost("movimentar")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Movimentar(
            [FromBody] MovimentarRequest body,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromServices] IValidator<MovimentarContaCommand> validator,
            CancellationToken ct)
        {
            var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!Guid.TryParse(sub, out var contaId))
                return Unauthorized(new ApiError("USER_UNAUTHORIZED", "Token inválido."));

            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return BadRequest(new ApiError("INVALID_VALUE", "Header 'Idempotency-Key' é obrigatório."));

            var cmd = new MovimentarContaCommand(
                ContaIdDoToken: contaId,
                Tipo: char.ToUpperInvariant(body.Tipo),
                Valor: body.Valor,
                NumeroConta: body.NumeroConta,
                IdempotencyKey: idempotencyKey.Trim());

            try
            {
                await validator.ValidateAndThrowAsync(cmd, ct);
                await _mediator.Send(cmd, ct);
                return NoContent();
            }
            catch (FluentValidation.ValidationException ex)
            {
                var msg = ex.Errors?.FirstOrDefault()?.ErrorMessage ?? "Dados inválidos.";
                return BadRequest(new ApiError("INVALID_VALUE", msg));
            }
            catch (InvalidTypeException ex)
            {
                return BadRequest(new ApiError("INVALID_TYPE", ex.Message));
            }
            catch (InactiveAccountException ex)
            {
                return BadRequest(new ApiError("INACTIVE_ACCOUNT", ex.Message));
            }
            catch (InvalidAccountException ex)
            {
                return BadRequest(new ApiError("INVALID_ACCOUNT", ex.Message));
            }
        }

        /// <summary>Lista os movimentos da conta do usuário autenticado.</summary>
        /// <remarks>
        /// Filtros opcionais (query string):
        /// - <c>desde</c>: data inicial (inclusiva), formato <c>yyyy-MM-dd</c>.
        /// - <c>ate</c>: data final (inclusiva), formato <c>yyyy-MM-dd</c>.
        /// - <c>tipo</c>: <c>C</c> (crédito) ou <c>D</c> (débito).
        /// - <c>page</c>, <c>pageSize</c>: paginação (máx. recomendado: 200).
        [Authorize]
        [HttpGet("ListarMovimentos")]
        [Produces("application/json")]
        public async Task<IActionResult> ListarMovimentos([FromQuery] DateTime? desde, [FromQuery] DateTime? ate, [FromQuery] char? tipo, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(sub, out var contaId))
                return Unauthorized(new { type = "USER_UNAUTHORIZED", message = "Token inválido." });

            var result = await _mediator.Send(
                new ListarMovimentosQuery(contaId, desde, ate, tipo, page, pageSize), ct);

            return Ok(result);
        }
    }
}
