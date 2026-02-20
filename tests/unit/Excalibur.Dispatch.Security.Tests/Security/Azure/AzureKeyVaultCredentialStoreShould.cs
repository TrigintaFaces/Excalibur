// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Azure;

using Azure;
using Azure.Security.KeyVault.Secrets;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;

namespace Excalibur.Dispatch.Security.Tests.Azure;

/// <summary>
/// Unit tests for <see cref="AzureKeyVaultCredentialStore"/>.
/// Verifies Sprint 390 implementation: Azure credential store moved to dedicated package.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AzureKeyVaultCredentialStoreShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var configuration = CreateConfigurationWithVaultUri();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => new AzureKeyVaultCredentialStore(configuration, null!));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var logger = A.Fake<ILogger<AzureKeyVaultCredentialStore>>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => new AzureKeyVaultCredentialStore(null!, logger));
		exception.ParamName.ShouldBe("configuration");
	}

	[Fact]
	public void Constructor_ThrowsInvalidOperationException_WhenVaultUriNotConfigured()
	{
		// Arrange
		var logger = A.Fake<ILogger<AzureKeyVaultCredentialStore>>();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>())
			.Build();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(
			() => new AzureKeyVaultCredentialStore(configuration, logger));
		exception.Message.ShouldContain("Azure Key Vault URI not configured");
	}

	[Fact]
	public void Constructor_ThrowsInvalidOperationException_WhenVaultUriIsEmpty()
	{
		// Arrange
		var logger = A.Fake<ILogger<AzureKeyVaultCredentialStore>>();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AzureKeyVault:VaultUri"] = ""
			})
			.Build();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(
			() => new AzureKeyVaultCredentialStore(configuration, logger));
		exception.Message.ShouldContain("Azure Key Vault URI not configured");
	}

	[Fact]
	public void Constructor_UsesDefaultKeyPrefix_WhenNotConfigured()
	{
		// Arrange
		var logger = A.Fake<ILogger<AzureKeyVaultCredentialStore>>();
		var configuration = CreateConfigurationWithVaultUri();

		// Act
		var exception = Record.Exception(() => new AzureKeyVaultCredentialStore(configuration, logger));

		// Assert
		exception.ShouldBeNull();
	}

	[Fact]
	public void Constructor_UsesCustomKeyPrefix_WhenConfigured()
	{
		// Arrange
		var logger = A.Fake<ILogger<AzureKeyVaultCredentialStore>>();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AzureKeyVault:VaultUri"] = "https://test-vault.vault.azure.net/",
				["AzureKeyVault:KeyPrefix"] = "custom-prefix-"
			})
			.Build();

		// Act
		var exception = Record.Exception(() => new AzureKeyVaultCredentialStore(configuration, logger));

		// Assert
		exception.ShouldBeNull();
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
	public async Task StoreCredentialAsync_ThrowsArgumentNullException_WhenCredentialIsNull()
	{
		// Arrange
		var store = CreateCredentialStore();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await store.StoreCredentialAsync("test-key", null!, CancellationToken.None));
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
	public async Task GetCredentialAsync_ReturnsSecureString_WhenSecretExists()
	{
		// Arrange
		var store = CreateCredentialStore();
		var secretClient = A.Fake<SecretClient>();
		var secret = new KeyVaultSecret("dispatch-test-key", "super-secret");

		A.CallTo(() => secretClient.GetSecretAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(secret, A.Fake<Response>())));

		SetSecretClient(store, secretClient);

		// Act
		var result = await store.GetCredentialAsync("test key", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result!.Length.ShouldBe("super-secret".Length);
		SecureStringToPlainText(result).ShouldBe("super-secret");
	}

	[Fact]
	public async Task GetCredentialAsync_ReturnsNull_WhenSecretClientReturns404()
	{
		// Arrange
		var store = CreateCredentialStore();
		var secretClient = A.Fake<SecretClient>();
		A.CallTo(() => secretClient.GetSecretAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "not found"));
		SetSecretClient(store, secretClient);

		// Act
		var result = await store.GetCredentialAsync("missing-key", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetCredentialAsync_Rethrows_WhenSecretClientThrowsNon404()
	{
		// Arrange
		var store = CreateCredentialStore();
		var secretClient = A.Fake<SecretClient>();
		A.CallTo(() => secretClient.GetSecretAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(500, "boom"));
		SetSecretClient(store, secretClient);

		// Act / Assert
		_ = await Should.ThrowAsync<RequestFailedException>(() => store.GetCredentialAsync("failing-key", CancellationToken.None));
	}

	[Fact]
	public async Task StoreCredentialAsync_SetsSecret_WithNormalizedNameAndExpectedTags()
	{
		// Arrange
		var logger = A.Fake<ILogger<AzureKeyVaultCredentialStore>>();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AzureKeyVault:VaultUri"] = "https://test-vault.vault.azure.net/",
				["AzureKeyVault:KeyPrefix"] = "custom-prefix-",
			})
			.Build();
		var store = new AzureKeyVaultCredentialStore(configuration, logger);

		var secretClient = A.Fake<SecretClient>();
		KeyVaultSecret? captured = null;
		A.CallTo(() => secretClient.SetSecretAsync(A<KeyVaultSecret>._, A<CancellationToken>._))
			.Invokes((KeyVaultSecret secret, CancellationToken _) => captured = secret)
			.ReturnsLazily((KeyVaultSecret secret, CancellationToken _) =>
				Task.FromResult(Response.FromValue(secret, A.Fake<Response>())));
		SetSecretClient(store, secretClient);

		// Act
		await store.StoreCredentialAsync("abc_123/DEF", CreateSecureString("very-secret"), CancellationToken.None);

		// Assert
		captured.ShouldNotBeNull();
		captured!.Name.ShouldBe("custom-prefix-abc-123-DEF");
		captured.Value.ShouldBe(new string('\0', "very-secret".Length));
		captured.Properties.ExpiresOn.ShouldNotBeNull();
		captured.Properties.Tags["ManagedBy"].ShouldBe("Excalibur.Dispatch");
		captured.Properties.Tags["Purpose"].ShouldBe("Credential");
	}

	[Fact]
	public async Task StoreCredentialAsync_TruncatesOverlongSecretName()
	{
		// Arrange
		var store = CreateCredentialStore();
		var secretClient = A.Fake<SecretClient>();
		KeyVaultSecret? captured = null;
		A.CallTo(() => secretClient.SetSecretAsync(A<KeyVaultSecret>._, A<CancellationToken>._))
			.Invokes((KeyVaultSecret secret, CancellationToken _) => captured = secret)
			.ReturnsLazily((KeyVaultSecret secret, CancellationToken _) =>
				Task.FromResult(Response.FromValue(secret, A.Fake<Response>())));
		SetSecretClient(store, secretClient);

		var veryLongKey = new string('x', 300);

		// Act
		await store.StoreCredentialAsync(veryLongKey, CreateSecureString("v"), CancellationToken.None);

		// Assert
		captured.ShouldNotBeNull();
		captured!.Name.Length.ShouldBeLessThanOrEqualTo(127);
	}

	[Fact]
	public async Task StoreCredentialAsync_Rethrows_WhenSecretClientThrows()
	{
		// Arrange
		var store = CreateCredentialStore();
		var secretClient = A.Fake<SecretClient>();
		A.CallTo(() => secretClient.SetSecretAsync(A<KeyVaultSecret>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(500, "write failed"));
		SetSecretClient(store, secretClient);

		// Act / Assert
		_ = await Should.ThrowAsync<RequestFailedException>(() =>
			store.StoreCredentialAsync("key", CreateSecureString("value"), CancellationToken.None));
	}

	private static IConfiguration CreateConfigurationWithVaultUri()
	{
		return new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AzureKeyVault:VaultUri"] = "https://test-vault.vault.azure.net/"
			})
			.Build();
	}

	private static AzureKeyVaultCredentialStore CreateCredentialStore()
	{
		var logger = A.Fake<ILogger<AzureKeyVaultCredentialStore>>();
		var configuration = CreateConfigurationWithVaultUri();
		return new AzureKeyVaultCredentialStore(configuration, logger);
	}

	private static SecureString CreateSecureString(string value)
	{
		var secureString = new SecureString();
		foreach (var c in value)
		{
			secureString.AppendChar(c);
		}
		secureString.MakeReadOnly();
		return secureString;
	}

	private static string SecureStringToPlainText(SecureString secureString)
	{
		var ptr = IntPtr.Zero;
		try
		{
			ptr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
			return Marshal.PtrToStringUni(ptr) ?? string.Empty;
		}
		finally
		{
			if (ptr != IntPtr.Zero)
			{
				Marshal.ZeroFreeGlobalAllocUnicode(ptr);
			}
		}
	}

	private static void SetSecretClient(AzureKeyVaultCredentialStore store, SecretClient secretClient)
	{
		var field = typeof(AzureKeyVaultCredentialStore).GetField("_secretClient", BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(store, secretClient);
	}
}
