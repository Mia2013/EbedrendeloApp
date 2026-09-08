using EbedrendeloApp.Common.Behaviors;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace EbedrendeloApp.Tests.Common.Behaviors;

public class AuthorizationBehaviorTests
{
    private sealed record PlainRequest : IRequest<string>;

    private sealed record AdminOnlyRequest : IRequest<string>, IRequireAdmin;

    private sealed record OnBehalfOfRequest(int TargetUserId) : IRequest<string>, IActsOnBehalfOf;

    private static AuthorizationBehavior<TRequest, string> CreateSut<TRequest>(ICurrentUser currentUser)
        where TRequest : notnull
        => new(currentUser, NullLogger<AuthorizationBehavior<TRequest, string>>.Instance);

    private static RequestHandlerDelegate<string> Next(string result = "ok")
        => _ => Task.FromResult(result);

    [Fact]
    public async Task Lets_an_unmarked_request_through_for_anyone()
    {
        var sut = CreateSut<PlainRequest>(new FakeCurrentUser(2, "Dolgozó", isAdmin: false));

        var result = await sut.Handle(new PlainRequest(), Next(), CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Lets_an_admin_through_an_admin_only_request()
    {
        var sut = CreateSut<AdminOnlyRequest>(new FakeCurrentUser(1, "Admin", isAdmin: true));

        var result = await sut.Handle(new AdminOnlyRequest(), Next(), CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Rejects_a_worker_on_an_admin_only_request()
    {
        var sut = CreateSut<AdminOnlyRequest>(new FakeCurrentUser(2, "Dolgozó", isAdmin: false));

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.Handle(new AdminOnlyRequest(), Next(), CancellationToken.None));

        Assert.Contains("adminisztrátori", exception.Message);
    }

    [Fact]
    public async Task Lets_a_worker_act_on_their_own_data()
    {
        var sut = CreateSut<OnBehalfOfRequest>(new FakeCurrentUser(2, "Dolgozó", isAdmin: false));

        var result = await sut.Handle(new OnBehalfOfRequest(2), Next(), CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Rejects_a_worker_acting_on_someone_elses_data()
    {
        // Enélkül bárki rendelhetne, mondhatna le és nézhetne egyenleget bárki nevében.
        var sut = CreateSut<OnBehalfOfRequest>(new FakeCurrentUser(2, "Dolgozó", isAdmin: false));

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.Handle(new OnBehalfOfRequest(3), Next(), CancellationToken.None));

        Assert.Contains("saját nevedben", exception.Message);
    }

    [Fact]
    public async Task Lets_an_admin_act_on_someone_elses_data()
    {
        // Az admin nevében-rendelés és az idegen egyenleg-történet megtekintése szándékos (US-3.1, US-5.2).
        var sut = CreateSut<OnBehalfOfRequest>(new FakeCurrentUser(1, "Admin", isAdmin: true));

        var result = await sut.Handle(new OnBehalfOfRequest(3), Next(), CancellationToken.None);

        Assert.Equal("ok", result);
    }
}
