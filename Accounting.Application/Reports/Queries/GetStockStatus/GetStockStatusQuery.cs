using Accounting.Application.Common.Utils;
using Accounting.Application.Reports.Queries.Dtos;
using MediatR;

namespace Accounting.Application.Reports.Queries.GetStockStatus;

public record GetStockStatusQuery : IRequest<List<StockStatusDto>>;
