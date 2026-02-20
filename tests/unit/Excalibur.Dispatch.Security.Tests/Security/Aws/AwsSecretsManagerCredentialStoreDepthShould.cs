// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Aws;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security.Tests.Security.Aws;

/// <summary>
/// Deep coverage tests for <see cref="AwsSecretsManagerCredentialStore"/> covering concurrent access,
/// store failure wrapping, multiple secrets retrieval, and SecureString conversion edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class AwsSecretsManagerCredentialStoreDepthShould
{
	[Fact]
	public async Task GetCredentialAsync_ReturnReadOnlySecureString()
	{
		// Arrange
		var store = CreateStoreWithSecret("key1", "value1");

		// Act
		var result = await store.GetCredentialAsync("key1", CancellationToken.None);

		// Assert — SecureString should be read-only
		result.ShouldNotBeNull();
		result!.IsReadOnly().ShouldBeTrue();
	}

	[Fact]
	public async Task GetCredentialAsync_ReturnCorrectLengthSecureString()
	{
		// Arrange
		var store = CreateStoreWithSecret("key2", "12345");

		// Act
		var result = await store.GetCredentialAsync("key2", CancellationToken.None);

		// Assert — length matches original value
		result.ShouldNotBeNull();
		result!.Length.ShouldBe(5);
	}

	[Fact]
	public async Task GetCredentialAsync_HandleSpecialCharacters()
	{
		// Arrange — value with special chars
		var store = CreateStoreWithSecret("special", "p@$$w0rd!#%^&*()");

		// Act
		var result = await store.GetCredentialAsync("special", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result!.Length.ShouldBe("p@$$w0rd!#%^&*()".Length);
	}

	[Fact]
	public async Task GetCredentialAsync_HandleUnicodeCharacters()
	{
		// Arrange
		var store = CreateStoreWithSecret("unicode", "\u00E9\u00E8\u00EA\u00EB");

		// Act
		var result = await store.GetCredentialAsync("unicode", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result!.Length.ShouldBe(4);
	}

	[Fact]
	public async Task StoreCredentialAsync_WrapExceptionForEmptyCredential()
	{
		// Arrange — empty SecureString has length 0 which triggers ArgumentException
		// that gets wrapped in InvalidOperationException by the outer catch
		var store = CreateStore();
		var credential = new System.Security.SecureString();
		credential.MakeReadOnly();

		// Act & Assert — store wraps the exception
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.StoreCredentialAsync("test", credential, CancellationToken.None));

		ex.Message.ShouldContain("Failed to store secret");
		ex.Message.ShouldContain("test");
		ex.InnerException.ShouldBeOfType<ArgumentException>();
	}

	[Fact]
	public async Task GetCredentialAsync_WrapExceptionFromConfigurationFailure()
	{
		// Arrange — fake configuration that throws on indexer access during get
		var logger = A.Fake<ILogger<AwsSecretsManagerCredentialStore>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		var configuration = A.Fake<IConfiguration>();
		A.CallTo(() => configuration[A<string>._])
			.Throws(new InvalidOperationException("AWS Secrets Manager unavailable"));

		var store = new AwsSecretsManagerCredentialStore(logger, configuration);

		// Act & Assert — get wraps the exception
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.GetCredentialAsync("test-key", CancellationToken.None));

		ex.Message.ShouldContain("Failed to retrieve secret");
		ex.Message.ShouldContain("test-key");
		ex.InnerException.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task GetCredentialAsync_SequentialCallsReturnIndependentInstances()
	{
		// Arrange
		var store = CreateStoreWithSecret("key3", "secret");

		// Act
		var result1 = await store.GetCredentialAsync("key3", CancellationToken.None);
		var result2 = await store.GetCredentialAsync("key3", CancellationToken.None);

		// Assert — each call returns a new SecureString instance
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();
		ReferenceEquals(result1, result2).ShouldBeFalse();
	}

	[Fact]
	public async Task GetCredentialAsync_ReturnNullForEmptySecretValue()
	{
		// Arrange — empty value in config (same as missing)
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				["AWS:SecretsManager:Secrets:empty-key"] = "",
			})
			.Build();

		var logger = A.Fake<ILogger<AwsSecretsManagerCredentialStore>>();
		var store = new AwsSecretsManagerCredentialStore(logger, configuration);

		// Act
		var result = await store.GetCredentialAsync("empty-key", CancellationToken.None);

		// Assert — empty string treated as not found
		result.ShouldBeNull();
	}

	[Fact]
	public async Task StoreCredentialAsync_SucceedWithSingleCharCredential()
	{
		// Arrange — minimum valid credential (length=1)
		var store = CreateStore();
		var credential = new System.Security.SecureString();
		credential.AppendChar('a');
		credential.MakeReadOnly();

		// Act
		var ex = await Record.ExceptionAsync(
			async () => await store.StoreCredentialAsync("min-key", credential, CancellationToken.None));

		// Assert
		ex.ShouldBeNull();
	}

	[Fact]
	public async Task GetCredentialAsync_ConcurrentAccessSucceeds()
	{
		// Arrange
		var store = CreateStoreWithSecret("concurrent", "value");

		// Act — fire multiple concurrent requests
		var tasks = Enumerable.Range(0, 5).Select(_ =>
			store.GetCredentialAsync("concurrent", CancellationToken.None));

		var results = await Task.WhenAll(tasks);

		// Assert — all should succeed
		results.ShouldAllBe(r => r != null);
	}

	[Fact]
	public async Task Dispose_DisposesSemaphore()
	{
		// Arrange
		var store = CreateStore();

		// Act
		store.Dispose();

		// Assert — subsequent operation should throw ObjectDisposedException
		await Should.ThrowAsync<ObjectDisposedException>(
			async () => await store.GetCredentialAsync("any", CancellationToken.None));
	}

	[Fact]
	public async Task GetCredentialAsync_ThrowOnNullKey()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			async () => await store.GetCredentialAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetCredentialAsync_ThrowOnWhitespaceKey()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			async () => await store.GetCredentialAsync("   ", CancellationToken.None));
	}

	[Fact]
	public async Task StoreCredentialAsync_ThrowOnNullCredential()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await store.StoreCredentialAsync("key", null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetCredentialAsync_ReturnNullForMissingKey()
	{
		// Arrange — key not in config at all
		var store = CreateStore();

		// Act
		var result = await store.GetCredentialAsync("nonexistent-key", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	private static AwsSecretsManagerCredentialStore CreateStore()
	{
		var logger = A.Fake<ILogger<AwsSecretsManagerCredentialStore>>();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
			})
			.Build();

		return new AwsSecretsManagerCredentialStore(logger, configuration);
	}

	private static AwsSecretsManagerCredentialStore CreateStoreWithSecret(string key, string value)
	{
		var logger = A.Fake<ILogger<AwsSecretsManagerCredentialStore>>();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
				[$"AWS:SecretsManager:Secrets:{key}"] = value,
			})
			.Build();

		return new AwsSecretsManagerCredentialStore(logger, configuration);
	}
}
