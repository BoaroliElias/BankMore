using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContaCorrente.Application.Auth
{
    public sealed class LoginValidator : AbstractValidator<LoginQuery>
    {
        public LoginValidator()
        {
            RuleFor(x => x.Usuario).NotEmpty().WithMessage("Informe CPF ou número da conta.");
            RuleFor(x => x.Senha).NotEmpty().WithMessage("Senha obrigatória.");
        }
    }
}
