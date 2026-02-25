// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

using FakeItEasy;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security.Tests.Security.Configuration;

/// <summary>
/// Unit tests for <see cref="EnvironmentVariableCredentialStore"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Configuration")]
public sealed class EnvironmentVariableCredentialStoreShould : IDisposable
{
	private const string TestEnvVarPrefix = "TEST_DISPATCH_";
	private const string TestKey = "test.credential";
	private const string TestValue = "secret-value-123";

	private readonly ILogger<EnvironmentVariableCredentialStore> _logger;
	private readonly EnvironmentVariableCredentialStore _sut;
	private readonly List<string> _envVarsToClean = new();

	public EnvironmentVariableCredentialStoreShould()
	{
		_logger = A.Fake<ILogger<EnvironmentVariableCredentialStore>>();
		_sut = new EnvironmentVariableCredentialStore(_logger, TestEnvVarPrefix);
	}

	public void Dispose()
	{
		// Clean up any environment variables set during tests
		foreach (var envVar in _envVarsToClean)
		{
			Environment.SetEnvironmentVariable(envVar, null);
		}
	}

	[Fact]
	public void ImplementICredentialStore()
	{
		// Assert
		_sut.ShouldBeAssignableTo<ICredentialStore>();
	}

	[Fact]
	public void BePublicAndSealed()
	{
		// Assert
		typeof(EnvironmentVariableCredentialStore).IsPublic.ShouldBeTrue();
		typeof(EnvironmentVariableCredentialStore).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new EnvironmentVariableCredentialStore(null!, "PREFIX_"));
	}

	[Fact]
	public async Task ReturnNullForMissingCredential()
	{
		// Act
		var result = await _sut.GetCredentialAsync("non.existent.key", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task RetrieveCredentialWithPrefix()
	{
		// Arrange
		var envVarName = $"{TestEnvVarPrefix}TEST_CREDENTIAL";
		Environment.SetEnvironmentVariable(envVarName, TestValue);
		_envVarsToClean.Add(envVarName);

		// Act
		var result = await _sut.GetCredentialAsync(TestKey, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task ReturnSecureString()
	{
		// Arrange
		var envVarName = $"{TestEnvVarPrefix}SECURE_TEST";
		Environment.SetEnvironmentVariable(envVarName, "secure-value");
		_envVarsToClean.Add(envVarName);

		// Act
		var result = await _sut.GetCredentialAsync("secure.test", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.IsReadOnly().ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowWhenKeyIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetCredentialAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenKeyIsEmpty()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetCredentialAsync(string.Empty, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenKeyIsWhitespace()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetCredentialAsync("   ", CancellationToken.None));
	}

	[Fact]
	public async Task ConvertDotsToUnderscores()
	{
		// Arrange - Key with dots should be converted to underscores
		var envVarName = $"{TestEnvVarPrefix}MY_KEY_WITH_DOTS";
		Environment.SetEnvironmentVariable(envVarName, "dot-value");
		_envVarsToClean.Add(envVarName);

		// Act
		var result = await _sut.GetCredentialAsync("my.key.with.dots", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task ConvertColonsToUnderscores()
	{
		// Arrange - Key with colons should be converted to underscores
		var envVarName = $"{TestEnvVarPrefix}MY_KEY_WITH_COLONS";
		Environment.SetEnvironmentVariable(envVarName, "colon-value");
		_envVarsToClean.Add(envVarName);

		// Act
		var result = await _sut.GetCredentialAsync("my:key:with:colons", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task ConvertToUpperCase()
	{
		// Arrange - Key should be converted to uppercase
		var envVarName = $"{TestEnvVarPrefix}LOWERCASE_KEY";
		Environment.SetEnvironmentVariable(envVarName, "upper-value");
		_envVarsToClean.Add(envVarName);

		// Act
		var result = await _sut.GetCredentialAsync("lowercase.key", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void AllowEmptyPrefix()
	{
		// Act
		var store = new EnvironmentVariableCredentialStore(_logger, string.Empty);

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public void UseDefaultPrefixWhenNotSpecified()
	{
		// Act
		var store = new EnvironmentVariableCredentialStore(_logger);

		// Assert - Default prefix is "DISPATCH_"
		store.ShouldNotBeNull();
	}
}
