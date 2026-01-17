using Accounting.Application.Common.JsonConverters;
using Accounting.Domain.Enums;
using MediatR;
using System.Text.Json.Serialization;

namespace Accounting.Application.Cheques.Commands.Create;

public record CreateChequeCommand(
    int? ContactId,
    ChequeType Type,
    ChequeDirection Direction,
    string ChequeNumber,
    DateTime IssueDate,
    DateTime DueDate,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Amount,

    string Currency,
    string? BankName,
    string? BankBranch,
    string? AccountNumber,
    string? DrawerName,
    string? Description
) : IRequest<int>;
