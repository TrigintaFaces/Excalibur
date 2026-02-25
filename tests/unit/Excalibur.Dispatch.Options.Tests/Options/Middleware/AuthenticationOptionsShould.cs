// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="AuthenticationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class AuthenticationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new AuthenticationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_RequireAuthentication_IsTrue()
	{
		// Arrange & Act
		var options = new AuthenticationOptions();

		// Assert
		options.RequireAuthentication.ShouldBeTrue();
	}

	[Fact]
	public void Default_DefaultScheme_IsBearer()
	{
		// Arrange & Act
		var options = new AuthenticationOptions();

		// Assert
		options.DefaultScheme.ShouldBe("Bearer");
	}

	[Fact]
	public void Default_TokenHeader_IsAuthorization()
	{
		// Arrange & Act
		var options = new AuthenticationOptions();

		// Assert
		options.TokenHeader.ShouldBe("Authorization");
	}

	[Fact]
	public void Default_EnableCaching_IsTrue()
	{
		// Arrange & Act
		var options = new AuthenticationOptions();

		// Assert
		options.EnableCaching.ShouldBeTrue();
	}

	[Fact]
	public void Default_CacheDuration_IsFiveMinutes()
	{
		// Arrange & Act
		var options = new AuthenticationOptions();

		// Assert
		options.CacheDuration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_MaxCacheSize_IsOneThousand()
	{
		// Arrange & Act
		var options = new AuthenticationOptions();

		// Assert
		options.MaxCacheSize.ShouldBe(1000);
	}

	[Fact]
	public void Default_ValidApiKeys_IsNull()
	{
		// Arrange & Act
		var options = new AuthenticationOptions();

		// Assert
		options.ValidApiKeys.ShouldBeNull();
	}

	[Fact]
	public void Default_AllowAnonymousForTypes_IsNull()
	{
		// Arrange & Act
		var options = new AuthenticationOptions();

		// Assert
		options.AllowAnonymousForTypes.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new AuthenticationOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void RequireAuthentication_CanBeSet()
	{
		// Arrange
		var options = new AuthenticationOptions();

		// Act
		options.RequireAuthentication = false;

		// Assert
		options.RequireAuthentication.ShouldBeFalse();
	}

	[Fact]
	public void DefaultScheme_CanBeSet()
	{
		// Arrange
		var options = new AuthenticationOptions();

		// Act
		options.DefaultScheme = "ApiKey";

		// Assert
		options.DefaultScheme.ShouldBe("ApiKey");
	}

	[Fact]
	public void TokenHeader_CanBeSet()
	{
		// Arrange
		var options = new AuthenticationOptions();

		// Act
		options.TokenHeader = "X-API-Key";

		// Assert
		options.TokenHeader.ShouldBe("X-API-Key");
	}

	[Fact]
	public void EnableCaching_CanBeSet()
	{
		// Arrange
		var options = new AuthenticationOptions();

		// Act
		options.EnableCaching = false;

		// Assert
		options.EnableCaching.ShouldBeFalse();
	}

	[Fact]
	public void CacheDuration_CanBeSet()
	{
		// Arrange
		var options = new AuthenticationOptions();

		// Act
		options.CacheDuration = TimeSpan.FromMinutes(10);

		// Assert
		options.CacheDuration.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void MaxCacheSize_CanBeSet()
	{
		// Arrange
		var options = new AuthenticationOptions();

		// Act
		options.MaxCacheSize = 5000;

		// Assert
		options.MaxCacheSize.ShouldBe(5000);
	}

	[Fact]
	public void AllowAnonymousForTypes_CanBeSet()
	{
		// Arrange
		var options = new AuthenticationOptions();
		var types = new[] { "HealthCheck", "Ping" };

		// Act
		options.AllowAnonymousForTypes = types;

		// Assert
		options.AllowAnonymousForTypes.ShouldBe(types);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var anonymousTypes = new[] { "HealthCheck" };

		// Act
		var options = new AuthenticationOptions
		{
			Enabled = false,
			RequireAuthentication = false,
			DefaultScheme = "ApiKey",
			TokenHeader = "X-API-Key",
			EnableCaching = false,
			CacheDuration = TimeSpan.FromMinutes(1),
			MaxCacheSize = 500,
			AllowAnonymousForTypes = anonymousTypes,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.RequireAuthentication.ShouldBeFalse();
		options.DefaultScheme.ShouldBe("ApiKey");
		options.TokenHeader.ShouldBe("X-API-Key");
		options.EnableCaching.ShouldBeFalse();
		options.CacheDuration.ShouldBe(TimeSpan.FromMinutes(1));
		options.MaxCacheSize.ShouldBe(500);
		options.AllowAnonymousForTypes.ShouldBe(anonymousTypes);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForApiKeyAuth_HasCorrectScheme()
	{
		// Act
		var options = new AuthenticationOptions
		{
			DefaultScheme = "ApiKey",
			TokenHeader = "X-API-Key",
		};

		// Assert
		options.DefaultScheme.ShouldBe("ApiKey");
		options.TokenHeader.ShouldBe("X-API-Key");
	}

	[Fact]
	public void Options_ForHighSecurity_HasShortCacheDuration()
	{
		// Act
		var options = new AuthenticationOptions
		{
			CacheDuration = TimeSpan.FromSeconds(30),
			RequireAuthentication = true,
		};

		// Assert
		options.CacheDuration.ShouldBeLessThan(TimeSpan.FromMinutes(1));
		options.RequireAuthentication.ShouldBeTrue();
	}

	[Fact]
	public void Options_WithAnonymousEndpoints_AllowsHealthChecks()
	{
		// Act
		var options = new AuthenticationOptions
		{
			AllowAnonymousForTypes = new[] { "HealthCheckCommand", "PingCommand" },
		};

		// Assert
		options.AllowAnonymousForTypes.ShouldContain("HealthCheckCommand");
		options.AllowAnonymousForTypes.ShouldContain("PingCommand");
	}

	#endregion
}
