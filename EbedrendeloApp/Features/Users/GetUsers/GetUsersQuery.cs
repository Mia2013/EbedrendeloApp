using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Users.GetUsers;

/// <summary>AC 9.4.1 — általános célú felhasználó-lista. Az Igazgatosag/Osztaly kiegészítő mező nem
/// része az eredeti AC-nek, de a User entitáson már létezik, és a névazonosításhoz (kézi jóváírás
/// autocomplete) szükséges. A dev-váltó (StubCurrentUser.GetUsersAsync) és a "más nevében rendelek"
/// választó (UserCalendar.razor, colleagues) továbbra is saját, korábbi implementációt használ — ezt a
/// query-t nem vezetjük át rájuk ebben a körben.</summary>
public sealed record GetUsersQuery : IRequest<Result<IReadOnlyList<UserOptionDto>>>;

public sealed record UserOptionDto(
    int Id,
    string UserName,
    int UserId,
    string DisplayName,
    string RoleName,
    string? Igazgatosag,
    string? Osztaly);
