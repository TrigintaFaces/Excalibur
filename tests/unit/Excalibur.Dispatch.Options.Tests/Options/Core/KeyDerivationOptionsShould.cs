// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="KeyDerivationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class KeyDerivationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Password_IsNull()
	{
		// Arrange & Act
		var options = new KeyDerivationOptions();

		// Assert
		options.Password.ShouldBeNull();
	}

	[Fact]
	public void Default_Salt_IsNull()
	{
		// Arrange & Act
		var options = new KeyDerivationOptions();

		// Assert
		options.Salt.ShouldBeNull();
	}

	[Fact]
	public void Default_Iterations_IsOneHundredThousand()
	{
		// Arrange & Act
		var options = new KeyDerivationOptions();

		// Assert
		options.Iterations.ShouldBe(100_000);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Password_CanBeSet()
	{
		// Arrange
		var options = new KeyDerivationOptions();

		// Act
		options.Password = "secret-password";

		// Assert
		options.Password.ShouldBe("secret-password");
	}

	[Fact]
	public void Salt_CanBeSet()
	{
		// Arrange
		var options = new KeyDerivationOptions();
		var salt = new byte[] { 0x01, 0x02, 0x03, 0x04 };

		// Act
		options.Salt = salt;

		// Assert
		options.Salt.ShouldBeSameAs(salt);
	}

	[Fact]
	public void Iterations_CanBeSet()
	{
		// Arrange
		var options = new KeyDerivationOptions();

		// Act
		options.Iterations = 200_000;

		// Assert
		options.Iterations.ShouldBe(200_000);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var salt = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

		// Act
		var options = new KeyDerivationOptions
		{
			Password = "my-secret",
			Salt = salt,
			Iterations = 150_000,
		};

		// Assert
		options.Password.ShouldBe("my-secret");
		options.Salt.ShouldBeSameAs(salt);
		options.Iterations.ShouldBe(150_000);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighSecurity_HasHighIterations()
	{
		// Act
		var options = new KeyDerivationOptions
		{
			Password = "secure-password",
			Salt = new byte[16], // 128-bit salt
			Iterations = 600_000, // OWASP recommended minimum for PBKDF2-SHA256
		};

		// Assert
		options.Iterations.ShouldBeGreaterThanOrEqualTo(600_000);
		options.Salt.Length.ShouldBeGreaterThanOrEqualTo(16);
	}

	[Fact]
	public void Options_ForLowLatency_HasLowerIterations()
	{
		// Act
		var options = new KeyDerivationOptions
		{
			Password = "password",
			Salt = new byte[16],
			Iterations = 50_000,
		};

		// Assert
		options.Iterations.ShouldBeLessThan(100_000);
	}

	[Fact]
	public void Options_WithEmptyPassword_IsAllowed()
	{
		// Act
		var options = new KeyDerivationOptions
		{
			Password = string.Empty,
		};

		// Assert - Empty strings are allowed, validation happens elsewhere
		options.Password.ShouldBe(string.Empty);
	}

	#endregion
}
