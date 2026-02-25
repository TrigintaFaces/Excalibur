// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Aws;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security.Tests.Aws;

/// <summary>
/// Unit tests for <see cref="AwsSecretsManagerCredentialStore"/>.
/// Verifies Sprint 390 implementation: AWS credential store moved to dedicated package.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AwsSecretsManagerCredentialStoreShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var configuration = CreateConfigurationWithRegion();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => new AwsSecretsManagerCredentialStore(null!, configuration));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var logger = A.Fake<ILogger<AwsSecretsManagerCredentialStore>>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => new AwsSecretsManagerCredentialStore(logger, null!));
		exception.ParamName.ShouldBe("configuration");
	}

	[Fact]
	public async Task GetCredentialAsync_ThrowsArgumentException_WhenKeyIsNull()
	{
		// Arrange
		var store = CreateCredentialStore();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await store.GetCredentialAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetCredentialAsync_ThrowsArgumentException_WhenKeyIsEmpty()
	{
		// Arrange
		var store = CreateCredentialStore();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await store.GetCredentialAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task GetCredentialAsync_ThrowsArgumentException_WhenKeyIsWhitespace()
	{
		// Arrange
		var store = CreateCredentialStore();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await store.GetCredentialAsync("   ", CancellationToken.None));
	}

	[Fact]
	public async Task GetCredentialAsync_ReturnsNull_WhenSecretNotFound()
	{
		// Arrange
		var store = CreateCredentialStore();

		// Act
		var result = await store.GetCredentialAsync("non-existent-key", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetCredentialAsync_ReturnsSecureString_WhenSecretExists()
	{
		// Arrange
		var store = CreateCredentialStoreWithSecret("my-secret", "secret-value");

		// Act
		var result = await store.GetCredentialAsync("my-secret", CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		SecureStringToString(result).ShouldBe("secret-value");
	}

	[Fact]
	public async Task GetCredentialAsync_ThrowsInvalidOperationException_WhenConfigurationThrows()
	{
		// Arrange - use a faked IConfiguration that throws when indexer is accessed
		var logger = A.Fake<ILogger<AwsSecretsManagerCredentialStore>>();
		var configuration = A.Fake<IConfiguration>();
		A.CallTo(() => configuration[A<string>.Ignored])
			.Throws(new InvalidOperationException("Simulated AWS failure"));

		var store = new AwsSecretsManagerCredentialStore(logger, configuration);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.GetCredentialAsync("test-key", CancellationToken.None));
		exception.Message.ShouldContain("Failed to retrieve secret");
		exception.Message.ShouldContain("test-key");
		_ = exception.InnerException.ShouldNotBeNull();
		exception.InnerException.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task StoreCredentialAsync_ThrowsArgumentException_WhenKeyIsNull()
	{
		// Arrange
		var store = CreateCredentialStore();
		var credential = CreateSecureString("test-value");

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await store.StoreCredentialAsync(null!, credential, CancellationToken.None));
	}

	[Fact]
	public async Task StoreCredentialAsync_ThrowsArgumentException_WhenKeyIsEmpty()
	{
		// Arrange
		var store = CreateCredentialStore();
		var credential = CreateSecureString("test-value");

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await store.StoreCredentialAsync("", credential, CancellationToken.None));
	}

	[Fact]
	public async Task StoreCredentialAsync_ThrowsArgumentException_WhenKeyIsWhitespace()
	{
		// Arrange
		var store = CreateCredentialStore();
		var credential = CreateSecureString("test-value");

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await store.StoreCredentialAsync("   ", credential, CancellationToken.None));
	}

	[Fact]
	public async Task StoreCredentialAsync_ThrowsArgumentNullException_WhenCredentialIsNull()
	{
		// Arrange
		var store = CreateCredentialStore();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await store.StoreCredentialAsync("test-key", null!, CancellationToken.None));
	}

	[Fact]
	public async Task StoreCredentialAsync_CompletesSuccessfully_WhenCredentialIsValid()
	{
		// Arrange
		var store = CreateCredentialStore();
		var credential = CreateSecureString("valid-credential");

		// Act
		var exception = await Record.ExceptionAsync(
			async () => await store.StoreCredentialAsync("test-key", credential, CancellationToken.None));

		// Assert
		exception.ShouldBeNull();
	}

	[Fact]
	public async Task StoreCredentialAsync_ThrowsInvalidOperationException_WhenCredentialIsEmpty()
	{
		// Arrange - empty SecureString has length 0 which triggers the < 1 validation
		var store = CreateCredentialStore();
		var emptyCredential = new System.Security.SecureString();
		emptyCredential.MakeReadOnly();

		// Act & Assert - The ArgumentException from length validation is caught by the outer
		// catch block and wrapped as InvalidOperationException
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.StoreCredentialAsync("test-key", emptyCredential, CancellationToken.None));
		exception.Message.ShouldContain("Failed to store secret");
		exception.Message.ShouldContain("test-key");
		_ = exception.InnerException.ShouldNotBeNull();
	}

	[Fact]
	public void Dispose_DoesNotThrow()
	{
		// Arrange
		var store = CreateCredentialStore();

		// Act & Assert
		var exception = Record.Exception(() => store.Dispose());
		exception.ShouldBeNull();
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var store = CreateCredentialStore();

		// Act & Assert
		var exception = Record.Exception(() =>
		{
			store.Dispose();
			store.Dispose();
			store.Dispose();
		});
		exception.ShouldBeNull();
	}

	[Fact]
	public async Task Dispose_ReleasesSemaphore_PreventingFurtherOperations()
	{
		// Arrange
		var store = CreateCredentialStore();

		// Act - dispose the store, then attempt an operation
		store.Dispose();

		// Assert - after dispose, the semaphore is disposed so WaitAsync should throw
		_ = await Should.ThrowAsync<ObjectDisposedException>(
			async () => await store.GetCredentialAsync("any-key", CancellationToken.None));
	}

	[Fact]
	public void ImplementsICredentialStore()
	{
		// Arrange
		var store = CreateCredentialStore();

		// Assert
		_ = store.ShouldBeAssignableTo<ICredentialStore>();
	}

	[Fact]
	public void ImplementsIWritableCredentialStore()
	{
		// Arrange
		var store = CreateCredentialStore();

		// Assert
		_ = store.ShouldBeAssignableTo<IWritableCredentialStore>();
	}

	[Fact]
	public void ImplementsIDisposable()
	{
		// Arrange
		var store = CreateCredentialStore();

		// Assert
		_ = store.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void CanBeResolvedFromDI_WhenRegionConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateConfigurationWithRegion();
		_ = services.AddSingleton(configuration);
		_ = services.AddLogging();
		_ = services.AddAwsSecretsManagerCredentialStore(configuration);

		var provider = services.BuildServiceProvider();

		// Act
		var credentialStore = provider.GetService<ICredentialStore>();

		// Assert
		_ = credentialStore.ShouldNotBeNull();
		_ = credentialStore.ShouldBeOfType<AwsSecretsManagerCredentialStore>();
	}

	[Fact]
	public void CanBeResolvedAsWritableCredentialStoreFromDI_WhenRegionConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateConfigurationWithRegion();
		_ = services.AddSingleton(configuration);
		_ = services.AddLogging();
		_ = services.AddAwsSecretsManagerCredentialStore(configuration);

		var provider = services.BuildServiceProvider();

		// Act
		var writableCredentialStore = provider.GetService<IWritableCredentialStore>();

		// Assert
		_ = writableCredentialStore.ShouldNotBeNull();
		_ = writableCredentialStore.ShouldBeOfType<AwsSecretsManagerCredentialStore>();
	}

	private static IConfiguration CreateConfigurationWithRegion()
	{
		return new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1"
			})
			.Build();
	}

	private static IConfiguration CreateConfigurationWithSecret(string key, string value)
	{
		return new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				[$"AWS:SecretsManager:Secrets:{key}"] = value
			})
			.Build();
	}

	private static AwsSecretsManagerCredentialStore CreateCredentialStore()
	{
		var logger = A.Fake<ILogger<AwsSecretsManagerCredentialStore>>();
		var configuration = CreateConfigurationWithRegion();
		return new AwsSecretsManagerCredentialStore(logger, configuration);
	}

	private static AwsSecretsManagerCredentialStore CreateCredentialStoreWithSecret(string key, string value)
	{
		var logger = A.Fake<ILogger<AwsSecretsManagerCredentialStore>>();
		var configuration = CreateConfigurationWithSecret(key, value);
		return new AwsSecretsManagerCredentialStore(logger, configuration);
	}

	private static System.Security.SecureString CreateSecureString(string value)
	{
		var secureString = new System.Security.SecureString();
		foreach (var c in value)
		{
			secureString.AppendChar(c);
		}
		secureString.MakeReadOnly();
		return secureString;
	}

	private static string SecureStringToString(System.Security.SecureString secureString)
	{
		var ptr = IntPtr.Zero;
		try
		{
			ptr = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(secureString);
			return System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr) ?? string.Empty;
		}
		finally
		{
			if (ptr != IntPtr.Zero)
			{
				System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(ptr);
			}
		}
	}
}
