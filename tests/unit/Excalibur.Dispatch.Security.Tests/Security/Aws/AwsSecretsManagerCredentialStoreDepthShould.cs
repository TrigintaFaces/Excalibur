// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

using Excalibur.Security.Aws;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security.Tests.Security.Aws;

/// <summary>
/// Deep coverage tests for <see cref="AwsSecretsManagerCredentialStore"/> covering store failure wrapping,
/// multiple secrets retrieval, and SecureString conversion edge cases. bd-ts66sh (S841, ADR-336): the store
/// now reads/writes through the REAL AWS SDK via an injectable <see cref="IAmazonSecretsManager"/> seam —
/// these drive a faked client (never real AWS / committed credentials).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class AwsSecretsManagerCredentialStoreDepthShould
{
	[Fact]
	public async Task GetCredentialAsync_ReturnReadOnlySecureString()
	{
		var store = CreateStoreWithSecret("key1", "value1");

		var result = await store.GetCredentialAsync("key1", CancellationToken.None);

		result.ShouldNotBeNull();
		result!.IsReadOnly().ShouldBeTrue();
	}

	[Fact]
	public async Task GetCredentialAsync_ReturnCorrectLengthSecureString()
	{
		var store = CreateStoreWithSecret("key2", "12345");

		var result = await store.GetCredentialAsync("key2", CancellationToken.None);

		result.ShouldNotBeNull();
		result!.Length.ShouldBe(5);
	}

	[Fact]
	public async Task GetCredentialAsync_HandleSpecialCharacters()
	{
		var store = CreateStoreWithSecret("special", "p@$$w0rd!#%^&*()");

		var result = await store.GetCredentialAsync("special", CancellationToken.None);

		result.ShouldNotBeNull();
		result!.Length.ShouldBe("p@$$w0rd!#%^&*()".Length);
	}

	[Fact]
	public async Task GetCredentialAsync_HandleUnicodeCharacters()
	{
		var store = CreateStoreWithSecret("unicode", "éèêë");

		var result = await store.GetCredentialAsync("unicode", CancellationToken.None);

		result.ShouldNotBeNull();
		result!.Length.ShouldBe(4);
	}

	[Fact]
	public async Task StoreCredentialAsync_ThrowsArgumentExceptionForEmptyCredential()
	{
		// Empty SecureString trips the length guard → ArgumentException, re-thrown unwrapped by the store's
		// `catch (ArgumentException) throw;` (a caller error, not a backend failure).
		var store = CreateStore();
		var credential = new System.Security.SecureString();
		credential.MakeReadOnly();

		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await store.StoreCredentialAsync("test", credential, CancellationToken.None));
	}

	[Fact]
	public async Task GetCredentialAsync_WrapBackendFailure()
	{
		// A backend/transport failure surfaces as InvalidOperationException (never logged-as-success).
		var client = A.Fake<IAmazonSecretsManager>();
		A.CallTo(() => client.GetSecretValueAsync(A<GetSecretValueRequest>._, A<CancellationToken>._))
			.Throws(new AmazonSecretsManagerException("AWS Secrets Manager unavailable"));
		var store = CreateStoreWithClient(client);

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.GetCredentialAsync("test-key", CancellationToken.None));

		ex.Message.ShouldContain("Failed to retrieve secret");
		ex.Message.ShouldContain("test-key");
		ex.InnerException.ShouldNotBeNull();
	}

	[Fact]
	public async Task GetCredentialAsync_SequentialCallsReturnIndependentInstances()
	{
		var store = CreateStoreWithSecret("key3", "secret");

		var result1 = await store.GetCredentialAsync("key3", CancellationToken.None);
		var result2 = await store.GetCredentialAsync("key3", CancellationToken.None);

		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();
		ReferenceEquals(result1, result2).ShouldBeFalse();
	}

	[Fact]
	public async Task GetCredentialAsync_ReturnNullForEmptySecretValue()
	{
		// An empty SecretString from the backend is treated as not found.
		var client = A.Fake<IAmazonSecretsManager>();
		A.CallTo(() => client.GetSecretValueAsync(A<GetSecretValueRequest>._, A<CancellationToken>._))
			.Returns(new GetSecretValueResponse { SecretString = "" });
		var store = CreateStoreWithClient(client);

		var result = await store.GetCredentialAsync("empty-key", CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task StoreCredentialAsync_SucceedWithSingleCharCredential()
	{
		var client = A.Fake<IAmazonSecretsManager>();
		A.CallTo(() => client.PutSecretValueAsync(A<PutSecretValueRequest>._, A<CancellationToken>._))
			.Returns(new PutSecretValueResponse());
		var store = CreateStoreWithClient(client);
		var credential = new System.Security.SecureString();
		credential.AppendChar('a');
		credential.MakeReadOnly();

		var ex = await Record.ExceptionAsync(
			async () => await store.StoreCredentialAsync("min-key", credential, CancellationToken.None));

		ex.ShouldBeNull();
	}

	[Fact]
	public async Task GetCredentialAsync_ConcurrentAccessSucceeds()
	{
		var store = CreateStoreWithSecret("concurrent", "value");

		var tasks = Enumerable.Range(0, 5).Select(_ =>
			store.GetCredentialAsync("concurrent", CancellationToken.None));

		var results = await Task.WhenAll(tasks);

		results.ShouldAllBe(r => r != null);
	}

	[Fact]
	public async Task GetCredentialAsync_ThrowOnNullKey()
	{
		var store = CreateStore();

		await Should.ThrowAsync<ArgumentException>(
			async () => await store.GetCredentialAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetCredentialAsync_ThrowOnWhitespaceKey()
	{
		var store = CreateStore();

		await Should.ThrowAsync<ArgumentException>(
			async () => await store.GetCredentialAsync("   ", CancellationToken.None));
	}

	[Fact]
	public async Task StoreCredentialAsync_ThrowOnNullCredential()
	{
		var store = CreateStore();

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await store.StoreCredentialAsync("key", null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetCredentialAsync_ReturnNullForMissingKey()
	{
		// CreateStore's fake client reports any key as missing (ResourceNotFoundException → null).
		var store = CreateStore();

		var result = await store.GetCredentialAsync("nonexistent-key", CancellationToken.None);

		result.ShouldBeNull();
	}

	private static AwsSecretsManagerCredentialStore CreateStore()
	{
		var client = A.Fake<IAmazonSecretsManager>();
		A.CallTo(() => client.GetSecretValueAsync(A<GetSecretValueRequest>._, A<CancellationToken>._))
			.Throws(new ResourceNotFoundException("not found"));
		A.CallTo(() => client.PutSecretValueAsync(A<PutSecretValueRequest>._, A<CancellationToken>._))
			.Returns(new PutSecretValueResponse());
		return CreateStoreWithClient(client);
	}

	private static AwsSecretsManagerCredentialStore CreateStoreWithSecret(string key, string value)
	{
		var client = A.Fake<IAmazonSecretsManager>();
		A.CallTo(() => client.GetSecretValueAsync(
				A<GetSecretValueRequest>.That.Matches(r => r.SecretId == key), A<CancellationToken>._))
			.Returns(new GetSecretValueResponse { SecretString = value });
		A.CallTo(() => client.GetSecretValueAsync(
				A<GetSecretValueRequest>.That.Matches(r => r.SecretId != key), A<CancellationToken>._))
			.Throws(new ResourceNotFoundException("not found"));
		return CreateStoreWithClient(client);
	}

	private static AwsSecretsManagerCredentialStore CreateStoreWithClient(IAmazonSecretsManager client)
	{
		var logger = A.Fake<ILogger<AwsSecretsManagerCredentialStore>>();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1",
			})
			.Build();

		return new AwsSecretsManagerCredentialStore(logger, configuration, client);
	}
}
