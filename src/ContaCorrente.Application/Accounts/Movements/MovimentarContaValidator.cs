using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContaCorrente.Application.Accounts.Movements
{
    public sealed class MovimentarContaValidator : AbstractValidator<MovimentarContaCommand>
    {
        public MovimentarContaValidator()
        {
            RuleFor(x => x.IdempotencyKey)
                .NotEmpty().WithMessage("Idempotency-Key é obrigatório.")
                .MaximumLength(120);

            RuleFor(x => x.Tipo)
                .Must(t => t == 'C' || t == 'D')
                .WithMessage("Tipo deve ser 'C' (Crédito) ou 'D' (Débito).");

            RuleFor(x => x.Valor)
                .GreaterThan(0m)
                .WithMessage("Valor deve ser positivo (maior que zero).");

            // NumeroConta é opcional; se vier, checaremos regra de negócio no handler
        }
    }
}
