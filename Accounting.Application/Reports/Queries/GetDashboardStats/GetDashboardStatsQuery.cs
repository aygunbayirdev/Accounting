using Accounting.Application.Common.Utils;
using Accounting.Application.Reports.Queries.Dtos;
using MediatR;

namespace Accounting.Application.Reports.Queries.GetDashboardStats;

public record GetDashboardStatsQuery(int BranchId) : IRequest<DashboardStatsDto>;
