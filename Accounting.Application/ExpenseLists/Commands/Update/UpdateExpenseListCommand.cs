using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.JsonConverters;
using Accounting.Application.ExpenseLists.Dto;
using MediatR;
using System.Text.Json.Serialization;

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

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Amount,

    int VatRate,
    string? Category,
    string? Notes
);