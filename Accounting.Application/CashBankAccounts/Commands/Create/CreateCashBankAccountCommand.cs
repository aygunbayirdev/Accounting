using Accounting.Application.CashBankAccounts.Queries.Dto;
using Accounting.Application.Common.Abstractions;
using Accounting.Domain.Enums;
using MediatR;

namespace Accounting.Application.CashBankAccounts.Commands.Create;

public record CreateCashBankAccountCommand(
    int BranchId,
    CashBankAccountType Type,
    string Name,
    string? Iban
) : IRequest<CashBankAccountDetailDto>;
