using Accounting.Application.Orders.Dto;
using MediatR;

namespace Accounting.Application.Orders.Queries.GetById;

public record GetOrderByIdQuery(int Id) : IRequest<OrderDetailDto>;
