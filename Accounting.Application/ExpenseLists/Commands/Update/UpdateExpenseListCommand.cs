using Accounting.Application.Common.Abstractions;
using Accounting.Application.ExpenseLists.Dto;
using MediatR;

namespace Accounting.Application.ExpenseLists.Commands.Update;

public record UpdateExpenseListCommand(
    int Id,
    string? Name,
    List<UpdateExpenseLineDto> Lines,
    string RowVersion
) : IRequest<ExpenseListDetailDto>;

public record UpdateExpenseLineDto(
    int? Id,
    DateTime DateUtc,
    int? SupplierId,
    string Currency,
    string Amount,
    int VatRate,
    string? Category,
    string? Notes
);