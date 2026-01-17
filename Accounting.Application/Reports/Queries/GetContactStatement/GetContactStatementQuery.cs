using Accounting.Application.Common.Utils;
using Accounting.Application.Reports.Queries.Dtos;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Reports.Queries.GetContactStatement;

public record GetContactStatementQuery(int ContactId, DateTime? DateFrom, DateTime? DateTo) : IRequest<ContactStatementDto>;
