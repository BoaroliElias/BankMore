using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transferencia.Application.Transfer
{
    public sealed class TransferirValidator : AbstractValidator<TransferirCommand>
    {
        public TransferirValidator()
        {
            RuleFor(x => x.Valor)
                .GreaterThan(0).WithMessage("Valor deve ser maior que zero.");

            RuleFor(x => x.NumeroContaDestino)
                .NotEmpty().WithMessage("Informe o número da conta destino.")
                .Length(8).WithMessage("Número da conta deve ter 8 dígitos.")
                .Matches(@"^\d{8}$").WithMessage("Número da conta deve conter apenas dígitos.");

            RuleFor(x => x)
                .Must(x => x.NumeroContaDestino != x.NumeroContaOrigem)
                .WithMessage("Conta de destino não pode ser igual à de origem.");
        }
    }
}
