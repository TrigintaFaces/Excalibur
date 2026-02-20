// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance.Azure;
using Excalibur.Dispatch.Compliance;

using Azure;
using Azure.Security.KeyVault.Keys;

using FakeItEasy;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System.Reflection;

namespace Excalibur.Dispatch.Compliance.Tests.Azure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AzureKeyVaultProviderShould
{
	[Fact]
	public void Constructor_ThrowsForNullArguments()
	{
		var options = Microsoft.Extensions.Options.Options.Create(CreateValidOptions());
		var cache = new MemoryCache(new MemoryCacheOptions());
		var logger = NullLogger<AzureKeyVaultProvider>.Instance;

		_ = Should.Throw<ArgumentNullException>(() => new AzureKeyVaultProvider(null!, cache, logger));
		_ = Should.Throw<ArgumentNullException>(() => new AzureKeyVaultProvider(options, null!, logger));
		_ = Should.Throw<ArgumentNullException>(() => new AzureKeyVaultProvider(options, cache, null!));
	}

	[Fact]
	public void Constructor_ThrowsWhenVaultUriMissing()
	{
		var options = CreateValidOptions();
		options.VaultUri = null;

		_ = Should.Throw<ArgumentException>(() => new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(options),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance));
	}

	[Fact]
	public async Task Methods_ThrowObjectDisposedException_AfterDispose()
	{
		var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		sut.Dispose();

		_ = await Should.ThrowAsync<ObjectDisposedException>(() => sut.GetKeyAsync("key-1", CancellationToken.None));
	}

	[Fact]
	public async Task Methods_ThrowArgumentException_ForInvalidArguments()
	{
		var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		_ = await Should.ThrowAsync<ArgumentException>(() => sut.GetKeyAsync("", CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(() => sut.GetKeyVersionAsync("", 1, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(() => sut.RotateKeyAsync(
			"",
			EncryptionAlgorithm.Aes256Gcm,
			null,
			null,
			CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(() => sut.DeleteKeyAsync("", 30, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(() => sut.SuspendKeyAsync("", "reason", CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(() => sut.SuspendKeyAsync("key-1", "", CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(() => sut.GetCryptographyClientAsync("", CancellationToken.None));
	}

	[Fact]
	public void DetermineKeyStatus_HandlesSuspendedDisabledExpiredAndActive()
	{
		var determineStatus = typeof(AzureKeyVaultProvider).GetMethod(
			"DetermineKeyStatus",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

		determineStatus.ShouldNotBeNull();

		var suspendedByTag = new KeyProperties("suspended-by-tag");
		suspendedByTag.Tags["excalibur:suspended"] = "true";
		((KeyStatus)determineStatus!.Invoke(null, [suspendedByTag])!).ShouldBe(KeyStatus.Suspended);

		var suspendedByDisable = new KeyProperties("suspended-by-disable")
		{
			Enabled = false
		};
		((KeyStatus)determineStatus.Invoke(null, [suspendedByDisable])!).ShouldBe(KeyStatus.Suspended);

		var decryptOnly = new KeyProperties("decrypt-only")
		{
			Enabled = true,
			ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(-1)
		};
		((KeyStatus)determineStatus.Invoke(null, [decryptOnly])!).ShouldBe(KeyStatus.DecryptOnly);

		var active = new KeyProperties("active")
		{
			Enabled = true,
			ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
		};
		((KeyStatus)determineStatus.Invoke(null, [active])!).ShouldBe(KeyStatus.Active);
	}

	[Fact]
	public void ExtractVersionNumber_ReturnsOne_WhenVersionIsMissing()
	{
		var extractVersion = typeof(AzureKeyVaultProvider).GetMethod(
			"ExtractVersionNumber",
			BindingFlags.NonPublic | BindingFlags.Static);
		extractVersion.ShouldNotBeNull();

		var properties = new KeyProperties("dispatch-orders");
		var version = (int)extractVersion!.Invoke(null, [properties])!;

		version.ShouldBe(1);
	}

	[Fact]
	public void MapToKeyMetadata_ProjectsExpectedFields()
	{
		var mapToMetadata = typeof(AzureKeyVaultProvider).GetMethod(
			"MapToKeyMetadata",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		mapToMetadata.ShouldNotBeNull();

		var expires = DateTimeOffset.UtcNow.AddDays(30);

		var key = KeyModelFactory.KeyVaultKey(
			KeyModelFactory.KeyProperties(
				id: new Uri("https://unit-tests.vault.azure.net/keys/metadata-key/version-1"),
				vaultUri: new Uri("https://unit-tests.vault.azure.net/"),
				name: "metadata-key",
				version: "version-1",
				managed: false,
				createdOn: DateTimeOffset.UtcNow.AddDays(-2),
				updatedOn: DateTimeOffset.UtcNow.AddDays(-1),
				recoveryLevel: "Recoverable"),
			KeyModelFactory.JsonWebKey(
				KeyType.RsaHsm,
				id: null,
				keyOps: [],
				curveName: null,
				x: null,
				y: null,
				d: null,
				n: null,
				e: null,
				k: null,
				t: null,
				p: null,
				q: null,
				dp: null,
				dq: null,
				qi: null));
		key.Properties.ExpiresOn = expires;
		key.Properties.Enabled = true;

		var expectedCreated = key.Properties.CreatedOn ?? DateTimeOffset.UtcNow;
		var expectedUpdated = key.Properties.UpdatedOn;
		key.Properties.Tags["excalibur:purpose"] = "messaging";
		key.Properties.Tags["excalibur:algorithm"] = EncryptionAlgorithm.Aes256Gcm.ToString();

		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var metadata = (KeyMetadata)mapToMetadata!.Invoke(sut, ["key-42", key, 7])!;

		metadata.KeyId.ShouldBe("key-42");
		metadata.Version.ShouldBe(7);
		metadata.Status.ShouldBe(KeyStatus.Active);
		metadata.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		metadata.CreatedAt.ShouldBe(expectedCreated);
		metadata.LastRotatedAt.ShouldBe(expectedUpdated);
		metadata.ExpiresAt.ShouldBe(expires);
		metadata.Purpose.ShouldBe("messaging");
		metadata.IsFipsCompliant.ShouldBeTrue();
	}

	[Fact]
	public void MapToKeyMetadata_UsesDefaultAlgorithm_AndNonFipsForSoftwareKeys()
	{
		var mapToMetadata = typeof(AzureKeyVaultProvider).GetMethod(
			"MapToKeyMetadata",
			BindingFlags.NonPublic | BindingFlags.Instance);
		mapToMetadata.ShouldNotBeNull();

		var key = KeyModelFactory.KeyVaultKey(
			KeyModelFactory.KeyProperties(
				id: new Uri("https://unit-tests.vault.azure.net/keys/software-key/version-1"),
				vaultUri: new Uri("https://unit-tests.vault.azure.net/"),
				name: "software-key",
				version: "version-1",
				managed: false,
				createdOn: null,
				updatedOn: null,
				recoveryLevel: "Recoverable"),
			KeyModelFactory.JsonWebKey(
				KeyType.Rsa,
				id: null,
				keyOps: [],
				curveName: null,
				x: null,
				y: null,
				d: null,
				n: null,
				e: null,
				k: null,
				t: null,
				p: null,
				q: null,
				dp: null,
				dq: null,
				qi: null));
		key.Properties.Tags["excalibur:purpose"] = "general";

		var options = CreateValidOptions();
		options.WarnOnStandardTierInProduction = true;
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(options),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var metadata = (KeyMetadata)mapToMetadata!.Invoke(sut, ["software-key", key, null])!;

		metadata.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		metadata.IsFipsCompliant.ShouldBeFalse();
		metadata.Purpose.ShouldBe("general");
	}

	[Fact]
	public void Dispose_IsIdempotent()
	{
		var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		sut.Dispose();
		sut.Dispose();
	}

	[Fact]
	public async Task GetKeyAsync_ReturnsMetadata_AndUsesCache()
	{
		using var cache = new MemoryCache(new MemoryCacheOptions());
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			cache,
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		var key = CreateKey("dispatch-order-key", "v1", KeyType.RsaHsm, "orders", EncryptionAlgorithm.Aes256Gcm);
		A.CallTo(() => keyClient.GetKeyAsync("dispatch-order-key", A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(key, A.Fake<Response>())));

		var first = await sut.GetKeyAsync("order-key", CancellationToken.None);
		var second = await sut.GetKeyAsync("order-key", CancellationToken.None);

		first.ShouldNotBeNull();
		first!.KeyId.ShouldBe("order-key");
		first.Purpose.ShouldBe("orders");
		second.ShouldNotBeNull();
		A.CallTo(() => keyClient.GetKeyAsync("dispatch-order-key", A<string?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetKeyVersionAsync_ReturnsMetadata_WhenRequestedVersionExists()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		var versionProps = KeyModelFactory.KeyProperties(
			id: new Uri("https://unit-tests.vault.azure.net/keys/dispatch-orders/version-7"),
			vaultUri: new Uri("https://unit-tests.vault.azure.net/"),
			name: "dispatch-orders",
			version: "version-7",
			managed: false,
			createdOn: DateTimeOffset.UtcNow.AddDays(-2),
			updatedOn: DateTimeOffset.UtcNow.AddDays(-1),
			recoveryLevel: "Recoverable");

		var expectedVersion = InvokeExtractVersionNumber(versionProps);
		var key = CreateKey("dispatch-orders", "version-7", KeyType.RsaHsm, "orders", EncryptionAlgorithm.Aes256Gcm);

		A.CallTo(() => keyClient.GetPropertiesOfKeyVersionsAsync("dispatch-orders", A<CancellationToken>._))
			.Returns(CreateAsyncPageable(versionProps));
		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders", "version-7", A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(key, A.Fake<Response>())));

		var result = await sut.GetKeyVersionAsync("orders", expectedVersion, CancellationToken.None);

		result.ShouldNotBeNull();
		result!.Version.ShouldBe(expectedVersion);
		result.KeyId.ShouldBe("orders");
	}

	[Fact]
	public async Task GetKeyVersionAsync_ReturnsNull_WhenRequestedVersionDoesNotExist()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		var versionProps = KeyModelFactory.KeyProperties(
			id: new Uri("https://unit-tests.vault.azure.net/keys/dispatch-orders/version-1"),
			vaultUri: new Uri("https://unit-tests.vault.azure.net/"),
			name: "dispatch-orders",
			version: "version-1",
			managed: false,
			createdOn: DateTimeOffset.UtcNow.AddDays(-2),
			updatedOn: DateTimeOffset.UtcNow.AddDays(-1),
			recoveryLevel: "Recoverable");

		A.CallTo(() => keyClient.GetPropertiesOfKeyVersionsAsync("dispatch-orders", A<CancellationToken>._))
			.Returns(CreateAsyncPageable(versionProps));

		var result = await sut.GetKeyVersionAsync("orders", int.MaxValue, CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetKeyAsync_ReturnsNull_WhenKeyNotFound()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders", A<string?>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "missing"));

		var result = await sut.GetKeyAsync("orders", CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetKeyVersionAsync_ReturnsNull_WhenKeyNotFound()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		A.CallTo(() => keyClient.GetPropertiesOfKeyVersionsAsync("dispatch-orders", A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "missing"));

		var result = await sut.GetKeyVersionAsync("orders", 1, CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task ListKeysAsync_FiltersByPrefixStatusAndPurpose()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		var prefixedProps = new KeyProperties("dispatch-orders");
		var nonPrefixedProps = new KeyProperties("external-payments");

		var activeOrders = CreateKey("dispatch-orders", "v1", KeyType.RsaHsm, "orders", EncryptionAlgorithm.Aes256Gcm);
		var activeBilling = CreateKey("dispatch-billing", "v1", KeyType.RsaHsm, "billing", EncryptionAlgorithm.Aes256Gcm);

		A.CallTo(() => keyClient.GetPropertiesOfKeysAsync(A<CancellationToken>._))
			.Returns(CreateAsyncPageable(prefixedProps, nonPrefixedProps, new KeyProperties("dispatch-billing")));

		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders", A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(activeOrders, A.Fake<Response>())));
		A.CallTo(() => keyClient.GetKeyAsync("dispatch-billing", A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(activeBilling, A.Fake<Response>())));

		var results = await sut.ListKeysAsync(KeyStatus.Active, "orders", CancellationToken.None);

		results.Count.ShouldBe(1);
		results[0].KeyId.ShouldBe("orders");
		results[0].Purpose.ShouldBe("orders");
	}

	[Fact]
	public async Task ListKeysAsync_SkipsKeys_WhenDetailsLookupReturnsNotFound()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		A.CallTo(() => keyClient.GetPropertiesOfKeysAsync(A<CancellationToken>._))
			.Returns(CreateAsyncPageable(new KeyProperties("dispatch-orders")));
		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders", A<string?>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "deleted"));

		var results = await sut.ListKeysAsync(status: null, purpose: null, CancellationToken.None);

		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetActiveKeyAsync_ReturnsMostRecentlyCreatedNonExpiredKey()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		var oldKey = CreateKey(
			"dispatch-orders-old",
			"v1",
			KeyType.RsaHsm,
			"orders",
			EncryptionAlgorithm.Aes256Gcm,
			createdOn: DateTimeOffset.UtcNow.AddDays(-10),
			expiresOn: DateTimeOffset.UtcNow.AddDays(2));
		var recentKey = CreateKey(
			"dispatch-orders-new",
			"v1",
			KeyType.RsaHsm,
			"orders",
			EncryptionAlgorithm.Aes256Gcm,
			createdOn: DateTimeOffset.UtcNow.AddDays(-1),
			expiresOn: DateTimeOffset.UtcNow.AddDays(10));

		A.CallTo(() => keyClient.GetPropertiesOfKeysAsync(A<CancellationToken>._))
			.Returns(CreateAsyncPageable(new KeyProperties("dispatch-orders-old"), new KeyProperties("dispatch-orders-new")));
		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders-old", A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(oldKey, A.Fake<Response>())));
		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders-new", A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(recentKey, A.Fake<Response>())));

		var active = await sut.GetActiveKeyAsync("orders", CancellationToken.None);

		active.ShouldNotBeNull();
		active!.KeyId.ShouldBe("orders-new");
	}

	[Fact]
	public async Task GetActiveKeyAsync_ReturnsNull_WhenOnlyExpiredKeysExist()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		var expired = CreateKey(
			"dispatch-orders-expired",
			"v1",
			KeyType.RsaHsm,
			"orders",
			EncryptionAlgorithm.Aes256Gcm,
			createdOn: DateTimeOffset.UtcNow.AddDays(-5),
			expiresOn: DateTimeOffset.UtcNow.AddMinutes(-1));

		A.CallTo(() => keyClient.GetPropertiesOfKeysAsync(A<CancellationToken>._))
			.Returns(CreateAsyncPageable(new KeyProperties("dispatch-orders-expired")));
		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders-expired", A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(expired, A.Fake<Response>())));

		var active = await sut.GetActiveKeyAsync("orders", CancellationToken.None);

		active.ShouldBeNull();
	}

	[Fact]
	public async Task GetActiveKeyAsync_UsesCache_ForSamePurpose()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		var key = CreateKey(
			"dispatch-orders",
			"v1",
			KeyType.RsaHsm,
			"orders",
			EncryptionAlgorithm.Aes256Gcm,
			createdOn: DateTimeOffset.UtcNow.AddDays(-1),
			expiresOn: DateTimeOffset.UtcNow.AddDays(5));

		A.CallTo(() => keyClient.GetPropertiesOfKeysAsync(A<CancellationToken>._))
			.Returns(CreateAsyncPageable(new KeyProperties("dispatch-orders")));
		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders", A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(key, A.Fake<Response>())));

		var first = await sut.GetActiveKeyAsync("orders", CancellationToken.None);
		var second = await sut.GetActiveKeyAsync("orders", CancellationToken.None);

		first.ShouldNotBeNull();
		second.ShouldNotBeNull();
		A.CallTo(() => keyClient.GetPropertiesOfKeysAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RotateKeyAsync_ReturnsFailure_WhenAzureReturnsRequestFailedException()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders", A<string?>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(500, "rotate-fail"));

		var result = await sut.RotateKeyAsync(
			"orders",
			EncryptionAlgorithm.Aes256Gcm,
			"orders",
			DateTimeOffset.UtcNow.AddDays(90),
			CancellationToken.None);

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Azure Key Vault error");
	}

	[Fact]
	public async Task RotateKeyAsync_RotatesExistingKey_WhenKeyAlreadyExists()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		var existing = CreateKey("dispatch-orders", "v1", KeyType.RsaHsm, "orders", EncryptionAlgorithm.Aes256Gcm);
		var rotated = CreateKey("dispatch-orders", "v2", KeyType.RsaHsm, "orders", EncryptionAlgorithm.Aes256Gcm);

		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders", A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(existing, A.Fake<Response>())));
		A.CallTo(() => keyClient.RotateKeyAsync("dispatch-orders", A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(rotated, A.Fake<Response>())));

		var result = await sut.RotateKeyAsync(
			"orders",
			EncryptionAlgorithm.Aes256Gcm,
			"orders",
			DateTimeOffset.UtcNow.AddDays(30),
			CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NewKey.ShouldNotBeNull();
		result.PreviousKey.ShouldNotBeNull();
		result.NewKey!.KeyId.ShouldBe("orders");
		result.PreviousKey!.KeyId.ShouldBe("orders");
	}

	[Fact]
	public async Task RotateKeyAsync_CreatesNewKey_WhenKeyDoesNotExist()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		var created = CreateKey("dispatch-orders", "v1", KeyType.RsaHsm, "orders", EncryptionAlgorithm.Aes256Gcm);
		CreateRsaKeyOptions? capturedOptions = null;

		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders", A<string?>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "missing"));
		A.CallTo(() => keyClient.CreateRsaKeyAsync(A<CreateRsaKeyOptions>._, A<CancellationToken>._))
			.Invokes((CreateRsaKeyOptions options, CancellationToken _) => capturedOptions = options)
			.Returns(Task.FromResult(Response.FromValue(created, A.Fake<Response>())));

		var result = await sut.RotateKeyAsync(
			"orders",
			EncryptionAlgorithm.Aes256Gcm,
			"orders",
			DateTimeOffset.UtcNow.AddDays(45),
			CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NewKey.ShouldNotBeNull();
		result.PreviousKey.ShouldBeNull();
		capturedOptions.ShouldNotBeNull();
		capturedOptions!.Name.ShouldBe("dispatch-orders");
		capturedOptions.Tags["excalibur:purpose"].ShouldBe("orders");
		capturedOptions.Tags["excalibur:algorithm"].ShouldBe(EncryptionAlgorithm.Aes256Gcm.ToString());
	}

	[Fact]
	public async Task SuspendKeyAsync_ReturnsFalse_WhenKeyIsNotFound()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders", A<string?>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "missing"));

		var suspended = await sut.SuspendKeyAsync("orders", "test", CancellationToken.None);
		suspended.ShouldBeFalse();
	}

	[Fact]
	public async Task SuspendKeyAsync_ReturnsTrue_WhenKeyExists()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		var key = CreateKey("dispatch-orders", "v1", KeyType.RsaHsm, "orders", EncryptionAlgorithm.Aes256Gcm);
		KeyProperties? updated = null;

		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders", A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(key, A.Fake<Response>())));
		A.CallTo(() => keyClient.UpdateKeyPropertiesAsync(
			A<KeyProperties>._,
			A<IEnumerable<KeyOperation>>._,
			A<CancellationToken>._))
			.Invokes((KeyProperties properties, IEnumerable<KeyOperation> _, CancellationToken _) => updated = properties)
			.Returns(Task.FromResult(Response.FromValue(key, A.Fake<Response>())));

		var suspended = await sut.SuspendKeyAsync("orders", "maintenance", CancellationToken.None);

		suspended.ShouldBeTrue();
		updated.ShouldNotBeNull();
		updated!.Enabled.ShouldBe(false);
		updated.Tags["excalibur:suspended"].ShouldBe("true");
		updated.Tags["excalibur:suspension_reason"].ShouldBe("maintenance");
	}

	[Fact]
	public async Task DeleteKeyAsync_ReturnsFalse_WhenKeyIsNotFound()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		A.CallTo(() => keyClient.StartDeleteKeyAsync("dispatch-orders", A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "missing"));

		var deleted = await sut.DeleteKeyAsync("orders", 30, CancellationToken.None);
		deleted.ShouldBeFalse();
	}

	[Fact]
	public async Task DeleteKeyAsync_ReturnsTrue_WhenDeletionCompletes()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		var operation = A.Fake<DeleteKeyOperation>();
		var deletedKey = KeyModelFactory.DeletedKey(
			KeyModelFactory.KeyProperties(
				id: new Uri("https://unit-tests.vault.azure.net/keys/dispatch-orders/version-1"),
				vaultUri: new Uri("https://unit-tests.vault.azure.net/"),
				name: "dispatch-orders",
				version: "version-1",
				managed: false,
				createdOn: DateTimeOffset.UtcNow.AddDays(-2),
				updatedOn: DateTimeOffset.UtcNow.AddDays(-1),
				recoveryLevel: "Recoverable"),
			KeyModelFactory.JsonWebKey(
				KeyType.RsaHsm,
				id: null,
				keyOps: [],
				curveName: null,
				x: null,
				y: null,
				d: null,
				n: null,
				e: null,
				k: null,
				t: null,
				p: null,
				q: null,
				dp: null,
				dq: null,
				qi: null),
			recoveryId: new Uri("https://unit-tests.vault.azure.net/deletedkeys/dispatch-orders"),
			deletedOn: DateTimeOffset.UtcNow,
			scheduledPurgeDate: DateTimeOffset.UtcNow.AddDays(90));
		A.CallTo(() => keyClient.StartDeleteKeyAsync("dispatch-orders", A<CancellationToken>._))
			.Returns(Task.FromResult(operation));
		A.CallTo(() => operation.WaitForCompletionAsync(A<CancellationToken>._))
			.Returns(new ValueTask<Response<DeletedKey>>(Response.FromValue(deletedKey, A.Fake<Response>())));

		var deleted = await sut.DeleteKeyAsync("orders", 30, CancellationToken.None);

		deleted.ShouldBeTrue();
		A.CallTo(() => keyClient.StartDeleteKeyAsync("dispatch-orders", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => operation.WaitForCompletionAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetCryptographyClientAsync_ReturnsCachedClient_ForSameKeyId()
	{
		using var sut = new AzureKeyVaultProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = CreateFakeKeyClient();
		SetPrivateField(sut, "_keyClient", keyClient);

		var key = CreateKey("dispatch-orders", "v1", KeyType.RsaHsm, "orders", EncryptionAlgorithm.Aes256Gcm);
		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders", A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(key, A.Fake<Response>())));

		var first = await sut.GetCryptographyClientAsync("orders", CancellationToken.None);
		var second = await sut.GetCryptographyClientAsync("orders", CancellationToken.None);

		first.ShouldBeSameAs(second);
		A.CallTo(() => keyClient.GetKeyAsync("dispatch-orders", A<string?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private static AzureKeyVaultOptions CreateValidOptions() =>
		new()
		{
			VaultUri = new Uri("https://unit-tests.vault.azure.net/"),
			KeyNamePrefix = "dispatch-"
		};

	private static KeyClient CreateFakeKeyClient()
	{
		return A.Fake<KeyClient>(options => options.WithArgumentsForConstructor(() =>
			new KeyClient(new Uri("https://unit-tests.vault.azure.net/"), new global::Azure.Identity.DefaultAzureCredential())));
	}

	private static AsyncPageable<KeyProperties> CreateAsyncPageable(params KeyProperties[] values)
	{
		var page = Page<KeyProperties>.FromValues(values, continuationToken: null, A.Fake<Response>());
		return AsyncPageable<KeyProperties>.FromPages([page]);
	}

	private static int InvokeExtractVersionNumber(KeyProperties properties)
	{
		var method = typeof(AzureKeyVaultProvider).GetMethod(
			"ExtractVersionNumber",
			BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();
		return (int)method!.Invoke(null, [properties])!;
	}

	private static KeyVaultKey CreateKey(
		string keyName,
		string version,
		KeyType keyType,
		string purpose,
		EncryptionAlgorithm algorithm,
		DateTimeOffset? createdOn = null,
		DateTimeOffset? updatedOn = null,
		DateTimeOffset? expiresOn = null)
	{
		var key = KeyModelFactory.KeyVaultKey(
			KeyModelFactory.KeyProperties(
				id: new Uri($"https://unit-tests.vault.azure.net/keys/{keyName}/{version}"),
				vaultUri: new Uri("https://unit-tests.vault.azure.net/"),
				name: keyName,
				version: version,
				managed: false,
				createdOn: createdOn ?? DateTimeOffset.UtcNow.AddDays(-1),
				updatedOn: updatedOn ?? DateTimeOffset.UtcNow,
				recoveryLevel: "Recoverable"),
			KeyModelFactory.JsonWebKey(
				keyType,
				id: null,
				keyOps: [],
				curveName: null,
				x: null,
				y: null,
				d: null,
				n: null,
				e: null,
				k: null,
				t: null,
				p: null,
				q: null,
				dp: null,
				dq: null,
				qi: null));

		key.Properties.Tags["excalibur:purpose"] = purpose;
		key.Properties.Tags["excalibur:algorithm"] = algorithm.ToString();
		key.Properties.Enabled = true;
		key.Properties.ExpiresOn = expiresOn;
		return key;
	}

	private static void SetPrivateField<T>(object instance, string fieldName, T value)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(instance, value);
	}
}
