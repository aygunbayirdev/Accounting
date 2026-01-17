using FluentValidation;

namespace Accounting.Application.Contacts.Queries.List;

public class ListContactsValidator : AbstractValidator<ListContactsQuery>
{
    public ListContactsValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
