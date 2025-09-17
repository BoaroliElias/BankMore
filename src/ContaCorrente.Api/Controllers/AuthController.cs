using BuildingBlocks;
using ContaCorrente.Application.Auth;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ContaCorrente.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IValidator<LoginQuery> _validator;

        public AuthController(IMediator mediator, IValidator<LoginQuery> validator)
        {
            _mediator = mediator;
            _validator = validator;
        }

        public sealed record LoginRequest(string Cpf, string Senha);

        /// <summary>Login com CPF ou nº da conta + senha (retorna JWT)</summary>
        /// <response code="200">Token emitido</response>
        /// <response code="401">USER_UNAUTHORIZED</response>
        [AllowAnonymous]
        [HttpPost("login")]
        [Consumes("application/json")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken ct)
        {
            try
            {
                var q = new LoginQuery(body.Cpf, body.Senha);
                await _validator.ValidateAndThrowAsync(q, ct);

                var r = await _mediator.Send(q, ct);
                return Ok(new { token = r.Token, expiresAt = r.ExpiresAt });
            }
            catch (ValidationException)
            {
                return Unauthorized(new ApiError("USER_UNAUTHORIZED", "Credenciais inválidas."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiError("USER_UNAUTHORIZED", ex.Message));
            }
        }

        /// <summary>Exemplo protegido: dados do usuário do token</summary>
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var acc = User.FindFirst("acc_number")?.Value;
            var name = User.FindFirst("name")?.Value;
            return Ok(new { id, numeroConta = acc, name });
        }
    }
}
