// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

using Excalibur.Security.Aws;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security.Tests.Aws;

/// <summary>
/// Unit tests for <see cref="AwsSecretsManagerCredentialStore"/>.
/// bd-ts66sh (S841, ADR-336): the store now reads/writes through the REAL AWS Secrets Manager SDK
/// (via an injectable <see cref="IAmazonSecretsManager"/> seam) — no longer a silent config-fallback
/// placeholder. Behavior tests drive a faked client (never real AWS / committed credentials); the
/// round-trip lock is the independent engage-test (author≠impl, AC-2).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
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
		// Arrange — the SDK signals a missing secret with ResourceNotFoundException (a normal, non-error outcome).
		var client = A.Fake<IAmazonSecretsManager>();
		A.CallTo(() => client.GetSecretValueAsync(A<GetSecretValueRequest>._, A<CancellationToken>._))
			.Throws(new ResourceNotFoundException("not found"));
		var store = CreateCredentialStoreWithClient(client);

		// Act
		var result = await store.GetCredentialAsync("non-existent-key", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetCredentialAsync_ReturnsSecureString_WhenSecretExists()
	{
		// Arrange — the value comes from the AWS port, NOT IConfiguration.
		var client = A.Fake<IAmazonSecretsManager>();
		A.CallTo(() => client.GetSecretValueAsync(A<GetSecretValueRequest>._, A<CancellationToken>._))
			.Returns(new GetSecretValueResponse { SecretString = "secret-value" });
		var store = CreateCredentialStoreWithClient(client);

		// Act
		var result = await store.GetCredentialAsync("my-secret", CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		SecureStringToString(result).ShouldBe("secret-value");
	}

	[Fact]
	public async Task GetCredentialAsync_ThrowsInvalidOperationException_WhenBackendThrows()
	{
		// Arrange — a backend/transport failure (not a missing secret) must surface as an error,
		// never logged-as-success (EC-2).
		var client = A.Fake<IAmazonSecretsManager>();
		A.CallTo(() => client.GetSecretValueAsync(A<GetSecretValueRequest>._, A<CancellationToken>._))
			.Throws(new AmazonSecretsManagerException("Simulated AWS failure"));
		var store = CreateCredentialStoreWithClient(client);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.GetCredentialAsync("test-key", CancellationToken.None));
		exception.Message.ShouldContain("Failed to retrieve secret");
		exception.Message.ShouldContain("test-key");
		_ = exception.InnerException!.ShouldNotBeNull();
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
		// Arrange — the write goes to the AWS port (PutSecretValue), not silently discarded.
		var client = A.Fake<IAmazonSecretsManager>();
		A.CallTo(() => client.PutSecretValueAsync(A<PutSecretValueRequest>._, A<CancellationToken>._))
			.Returns(new PutSecretValueResponse());
		var store = CreateCredentialStoreWithClient(client);
		var credential = CreateSecureString("valid-credential");

		// Act
		var exception = await Record.ExceptionAsync(
			async () => await store.StoreCredentialAsync("test-key", credential, CancellationToken.None));

		// Assert
		exception.ShouldBeNull();
		A.CallTo(() => client.PutSecretValueAsync(A<PutSecretValueRequest>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StoreCredentialAsync_ThrowsArgumentException_WhenCredentialIsEmpty()
	{
		// Arrange — empty SecureString has length 0, which trips the length guard. The store's
		// `catch (ArgumentException) throw;` re-throws it unwrapped (a caller error, not a backend failure).
		var client = A.Fake<IAmazonSecretsManager>();
		var store = CreateCredentialStoreWithClient(client);
		var emptyCredential = new System.Security.SecureString();
		emptyCredential.MakeReadOnly();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await store.StoreCredentialAsync("test-key", emptyCredential, CancellationToken.None));
	}

	[Fact]
	public async Task RoundTripCredentialThroughTheAwsPort()
	{
		// AC-2 engage-test — a store→get round-trip persists + retrieves THROUGH THE AWS port (RED on the
		// pre-fix stub which discarded on store + read IConfiguration on get). No committed secrets.
		var backing = new Dictionary<string, string>(StringComparer.Ordinal);
		var client = A.Fake<IAmazonSecretsManager>();
		A.CallTo(() => client.PutSecretValueAsync(A<PutSecretValueRequest>._, A<CancellationToken>._))
			.ReturnsLazily((PutSecretValueRequest r, CancellationToken _) =>
			{
				backing[r.SecretId] = r.SecretString;
				return new PutSecretValueResponse();
			});
		A.CallTo(() => client.GetSecretValueAsync(A<GetSecretValueRequest>._, A<CancellationToken>._))
			.ReturnsLazily((GetSecretValueRequest r, CancellationToken _) =>
				backing.TryGetValue(r.SecretId, out var v)
					? new GetSecretValueResponse { SecretString = v }
					: throw new ResourceNotFoundException("not found"));

		var store = CreateCredentialStoreWithClient(client);

		// Act
		await store.StoreCredentialAsync("api-key", CreateSecureString("s3cr3t-value"), CancellationToken.None);
		var result = await store.GetCredentialAsync("api-key", CancellationToken.None);

		// Assert — value round-trips through the port.
		_ = result.ShouldNotBeNull();
		SecureStringToString(result).ShouldBe("s3cr3t-value");
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
		_ = services.AddDispatchSecurityAws(aws => aws.Region("us-east-1"));

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
		_ = services.AddDispatchSecurityAws(aws => aws.Region("us-east-1"));

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

	private static AwsSecretsManagerCredentialStore CreateCredentialStore()
	{
		var logger = A.Fake<ILogger<AwsSecretsManagerCredentialStore>>();
		var configuration = CreateConfigurationWithRegion();
		return new AwsSecretsManagerCredentialStore(logger, configuration);
	}

	private static AwsSecretsManagerCredentialStore CreateCredentialStoreWithClient(IAmazonSecretsManager client)
	{
		var logger = A.Fake<ILogger<AwsSecretsManagerCredentialStore>>();
		var configuration = CreateConfigurationWithRegion();
		return new AwsSecretsManagerCredentialStore(logger, configuration, client);
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
