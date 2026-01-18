using Accounting.Application.Reports.Queries.Dtos;
using MediatR;

namespace Accounting.Application.Reports.Queries.GetIncomeExpense;

public record GetIncomeExpenseQuery(int? BranchId, DateTime? DateFrom, DateTime? DateTo) : IRequest<IncomeExpenseDto>;
