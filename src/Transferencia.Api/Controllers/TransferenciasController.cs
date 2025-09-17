using MediatR;
using Microsoft.AspNet.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Transferencia.Application.Transfer;

namespace Transferencia.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/transferencias")]
    public sealed class TransferenciasController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TransferenciasController(IMediator mediator) => _mediator = mediator;

        public sealed record TransferirRequest(string NumeroContaDestino, decimal Valor);

        /// <summary>Transfere valores da conta do usuário autenticado para a conta destino.</summary>
        /// <remarks>
        /// Envie o header <c>Idempotency-Key</c> (opcional, mas recomendado).  
        /// Ex.: <c>Idempotency-Key: tx-3b7c... </c>
        /// </remarks>
        /// /// </remarks>
        /// <response code="204">Transferência concluída.</response>
        /// <response code="400">Regra de negócio violada (tipo em <c>type</c>).</response>
        /// <response code="403">Token inválido/expirado.</response>
        [Authorize]
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Transferir([FromBody] TransferirRequest body, CancellationToken ct)
        {
            var origem = User.FindFirstValue("acc_number");
            if (string.IsNullOrWhiteSpace(origem))
                return Unauthorized(new { type = "USER_UNAUTHORIZED", message = "Token sem conta." });

            var idemKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

            try
            {
                var cmd = new TransferirCommand(
                    NumeroContaDestino: body.NumeroContaDestino,
                    Valor: body.Valor,
                    IdempotencyKeyFromHeader: idemKey,
                    NumeroContaOrigem: origem);

                var result = await _mediator.Send(cmd, ct);
                return Accepted(new { transferenciaId = result.TransferenciaId });
            }
            catch (FluentValidation.ValidationException ex)
            {
                var msg = ex.Errors.FirstOrDefault()?.ErrorMessage ?? "Dados inválidos.";
                return BadRequest(new { type = "INVALID_VALUE", message = msg });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { type = "BUSINESS_RULE", message = ex.Message });
            }
        }
    }
}
