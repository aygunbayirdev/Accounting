using Accounting.Application.Reports.Queries.Dtos;
using MediatR;

namespace Accounting.Application.Reports.Queries.GetProfitLoss;

public record GetProfitLossQuery(int? BranchId, DateTime? DateFrom, DateTime? DateTo) : IRequest<ProfitLossDto>;
