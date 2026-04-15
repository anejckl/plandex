using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Plandex.Api.DTOs;
using Xunit;

namespace Plandex.Api.Tests;

public class AuthTests : IClassFixture<PlandexAppFactory>, IAsyncLifetime
{
    private readonly PlandexAppFactory _factory;
    public AuthTests(PlandexAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_returns_access_token_and_user()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterDto("a@test.com", "password123", "Alice"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await resp.Content.ReadJsonAsync<AuthResponseDto>();
        auth!.AccessToken.Should().NotBeNullOrEmpty();
        auth.User.Email.Should().Be("a@test.com");
        auth.User.Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Duplicate_email_returns_409()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterDto("dup@test.com", "password123", "A"));
        var dup = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterDto("dup@test.com", "password123", "B"));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterDto("bad@test.com", "password123", "A"));

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto("bad@test.com", "wrong"));
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_requires_auth_and_returns_user()
    {
        var client = _factory.CreateClient();

        var anon = await client.GetAsync("/api/auth/me");
        anon.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (_, auth) = await client.RegisterAsync("me@test.com");
        var me = await client.GetJsonAsync<UserDto>("/api/auth/me");
        me!.Email.Should().Be("me@test.com");
        me.Id.Should().Be(auth.User.Id);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_old_refresh_is_revoked()
    {
        // Disable automatic cookie handling so we can replay the original cookie deliberately
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        var regResp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterDto("rot@test.com", "password123", "A"));
        var cookies = regResp.Headers.GetValues("Set-Cookie").ToList();
        cookies.Should().ContainSingle(c => c.StartsWith("plandex_refresh="));

        var refreshCookie = cookies.First(c => c.StartsWith("plandex_refresh=")).Split(';')[0];

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req.Headers.Add("Cookie", refreshCookie);
        var refresh1 = await client.SendAsync(req);
        refresh1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Replaying the ORIGINAL cookie should fail — first refresh rotated/revoked it
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req2.Headers.Add("Cookie", refreshCookie);
        var refresh2 = await client.SendAsync(req2);
        refresh2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
