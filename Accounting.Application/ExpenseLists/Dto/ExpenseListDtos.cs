using Accounting.Application.Common.JsonConverters;
using System.Text.Json.Serialization;

namespace Accounting.Application.ExpenseLists.Dto;

// List için basit DTO
public record ExpenseListListItemDto(
    int Id,
    int BranchId,
    string Name,
    string Status,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalAmount,

    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

// Detail DTO (Lines dahil)
public record ExpenseListDetailDto(
    int Id,
    int BranchId,
    string Name,
    string Status,
    IReadOnlyList<ExpenseLineDto> Lines,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalAmount,


    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

// Line DTO
public record ExpenseLineDto(
    int Id,
    int ExpenseListId,
    DateTime DateUtc,
    int? SupplierId,
    string Currency,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Amount,

    int VatRate,
    string? Category,
    string? Notes
);