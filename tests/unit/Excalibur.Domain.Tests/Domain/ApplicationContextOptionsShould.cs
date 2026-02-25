namespace Excalibur.Tests.Domain;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class ApplicationContextOptionsShould
{
    [Fact]
    public void DefaultValues_AllEmptyStrings()
    {
        // Act
        var options = new ApplicationContextOptions();

        // Assert
        options.ApplicationName.ShouldBe(string.Empty);
        options.ApplicationSystemName.ShouldBe(string.Empty);
        options.ApplicationDisplayName.ShouldBe(string.Empty);
        options.AuthenticationServiceAudience.ShouldBe(string.Empty);
        options.AuthenticationServiceEndpoint.ShouldBe(string.Empty);
        options.AuthenticationServicePublicKeyPath.ShouldBe(string.Empty);
        options.AuthorizationServiceEndpoint.ShouldBe(string.Empty);
        options.ServiceAccountName.ShouldBe(string.Empty);
        options.ServiceAccountPrivateKeyPath.ShouldBe(string.Empty);
        options.ServiceAccountPrivateKeyPasswordSecure.ShouldBe(string.Empty);
    }

    [Fact]
    public void SetAndGet_AllProperties()
    {
        // Arrange & Act
        var options = new ApplicationContextOptions
        {
            ApplicationName = "MyApp",
            ApplicationSystemName = "myapp-system",
            ApplicationDisplayName = "My Application",
            AuthenticationServiceAudience = "api://default",
            AuthenticationServiceEndpoint = "https://auth.example.com",
            AuthenticationServicePublicKeyPath = "/keys/public.pem",
            AuthorizationServiceEndpoint = "https://authz.example.com",
            ServiceAccountName = "svc-account",
            ServiceAccountPrivateKeyPath = "/keys/private.pfx",
            ServiceAccountPrivateKeyPasswordSecure = "secret123",
        };

        // Assert
        options.ApplicationName.ShouldBe("MyApp");
        options.ApplicationSystemName.ShouldBe("myapp-system");
        options.ApplicationDisplayName.ShouldBe("My Application");
        options.AuthenticationServiceAudience.ShouldBe("api://default");
        options.AuthenticationServiceEndpoint.ShouldBe("https://auth.example.com");
        options.AuthenticationServicePublicKeyPath.ShouldBe("/keys/public.pem");
        options.AuthorizationServiceEndpoint.ShouldBe("https://authz.example.com");
        options.ServiceAccountName.ShouldBe("svc-account");
        options.ServiceAccountPrivateKeyPath.ShouldBe("/keys/private.pfx");
        options.ServiceAccountPrivateKeyPasswordSecure.ShouldBe("secret123");
    }
}
