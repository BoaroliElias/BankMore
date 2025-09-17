using FluentValidation;

namespace ContaCorrente.Application.Accounts;

public sealed class CadastrarContaValidator : AbstractValidator<CadastrarContaCommand>
{
    public CadastrarContaValidator()
    {
        RuleFor(x => x.Cpf)
            .NotEmpty().WithMessage("CPF é obrigatório.")
            .Must(CpfUtils.IsValid).WithMessage("CPF inválido.");

        RuleFor(x => x.Senha)
            .NotEmpty().WithMessage("Senha é obrigatória.")
            .MinimumLength(4).WithMessage("Senha deve ter ao menos 8 caracteres.");
    }
}

internal static class CpfUtils
{
    public static bool IsValid(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return false;
        var d = new string(cpf.Where(char.IsDigit).ToArray());
        if (d.Length != 11) return false;
        if (new string(d[0], 11) == d) return false;

        int[] m1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] m2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        int soma = 0;
        for (int i = 0; i < 9; i++) soma += (d[i] - '0') * m1[i];
        int r = soma % 11; int dv1 = r < 2 ? 0 : 11 - r;

        soma = 0;
        for (int i = 0; i < 10; i++) soma += (d[i] - '0') * m2[i];
        r = soma % 11; int dv2 = r < 2 ? 0 : 11 - r;

        return d.EndsWith($"{dv1}{dv2}");
    }
}
