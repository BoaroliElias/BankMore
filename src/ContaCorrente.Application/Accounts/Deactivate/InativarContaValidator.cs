using FluentValidation;

namespace ContaCorrente.Application.Accounts.Deactivate
{
    public sealed class InativarContaValidator : AbstractValidator<InativarContaCommand>
    {
        public InativarContaValidator()
        {
            RuleFor(x => x.ContaId).NotEmpty();
            RuleFor(x => x.Senha).NotEmpty().MinimumLength(4);
        }
    }
}
