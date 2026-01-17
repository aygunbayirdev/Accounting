using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.JsonConverters;
using Accounting.Application.ExpenseLists.Dto;
using MediatR;
using System.Text.Json.Serialization;

namespace Accounting.Application.ExpenseLists.Commands.Create;

public record CreateExpenseListCommand(
    int BranchId,
    string? Name,
    List<CreateExpenseLineDto> Lines
) : IRequest<ExpenseListDetailDto>;

public record CreateExpenseLineDto(
    DateTime DateUtc,
    int? SupplierId,
    string Currency,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Amount,

    int VatRate,
    string? Category,
    string? Notes
);