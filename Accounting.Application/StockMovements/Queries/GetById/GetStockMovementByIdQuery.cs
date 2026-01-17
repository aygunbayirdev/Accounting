using Accounting.Application.StockMovements.Queries.Dto;
using MediatR;

namespace Accounting.Application.StockMovements.Queries.GetById;

public record GetStockMovementByIdQuery(int Id) : IRequest<StockMovementDetailDto>;
