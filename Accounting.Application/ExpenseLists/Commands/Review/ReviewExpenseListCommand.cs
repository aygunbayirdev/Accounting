using Accounting.Application.Common.Abstractions;
using Accounting.Application.ExpenseLists.Dto;
using MediatR;

namespace Accounting.Application.ExpenseLists.Commands.Review;

public record ReviewExpenseListCommand(int Id)
    : IRequest<ExpenseListDetailDto>;