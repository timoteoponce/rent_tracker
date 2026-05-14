using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RentTracker.Tests.Integration;

public class AccountTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AccountTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidCredentials_RedirectsToHome()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsPageWithError()
    {
        var client = _factory.CreateClient();

        // Fetch login page to get anti-forgery token
        var loginPage = await client.GetAsync("/Account/Login");
        var loginContent = await loginPage.Content.ReadAsStringAsync();
        var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(loginContent);

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.LoginIdentifier"] = "nonexistent",
            ["Input.Password"] = "wrongpassword",
            ["Input.RememberMe"] = "false"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid login attempt", content);
    }

    [Fact]
    public async Task Unauthenticated_User_CannotAccessDashboard()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }
}
