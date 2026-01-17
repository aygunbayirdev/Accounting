using Accounting.Application.Contacts.Queries.Dto;
using MediatR;

namespace Accounting.Application.Contacts.Queries.GetById;

public record GetContactByIdQuery(int Id) : IRequest<ContactDetailDto>;
