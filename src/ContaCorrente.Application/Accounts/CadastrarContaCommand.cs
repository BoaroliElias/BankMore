using MediatR;

namespace ContaCorrente.Application.Accounts;

public sealed record CadastrarContaCommand(string Cpf, string Senha, string Nome)
    : IRequest<CadastrarContaResult>;

public sealed record CadastrarContaResult(string NumeroConta);
