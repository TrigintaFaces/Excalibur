// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance.Vault;
using Excalibur.Dispatch.Compliance;

using FakeItEasy;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System.Reflection;

using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SecretsEngines.Transit;

namespace Excalibur.Dispatch.Compliance.Tests.Vault;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class VaultKeyProviderShould
{
	[Fact]
	public void Constructor_ThrowsForNullArguments()
	{
		var options = Microsoft.Extensions.Options.Options.Create(CreateValidOptions());
		var cache = new MemoryCache(new MemoryCacheOptions());
		var logger = NullLogger<VaultKeyProvider>.Instance;

		_ = Should.Throw<ArgumentNullException>(() => new VaultKeyProvider(null!, cache, logger));
		_ = Should.Throw<ArgumentNullException>(() => new VaultKeyProvider(options, null!, logger));
		_ = Should.Throw<ArgumentNullException>(() => new VaultKeyProvider(options, cache, null!));
	}

	[Fact]
	public void Constructor_ThrowsWhenVaultUriMissing()
	{
		var options = CreateValidOptions();
		options.VaultUri = null;

		_ = Should.Throw<ArgumentException>(() => new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(options),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<VaultKeyProvider>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsWhenAuthMethodIsUnsupported()
	{
		var options = CreateValidOptions();
		options.Auth.AuthMethod = (VaultAuthMethod)999;

		_ = Should.Throw<NotSupportedException>(() => new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(options),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<VaultKeyProvider>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsWhenKubernetesJwtIsMissing()
	{
		var options = CreateValidOptions();
		options.Auth.AuthMethod = VaultAuthMethod.Kubernetes;
		options.Auth.KubernetesRole = "dispatch";
		options.Auth.KubernetesJwtPath = Path.Combine(
			Path.GetTempPath(),
			$"missing-jwt-{Guid.NewGuid():N}");

		_ = Should.Throw<InvalidOperationException>(() => new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(options),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<VaultKeyProvider>.Instance));
	}

	[Fact]
	public void Constructor_SupportsAppRoleAuthentication()
	{
		var options = CreateValidOptions();
		options.Auth.AuthMethod = VaultAuthMethod.AppRole;
		options.Auth.AppRoleId = "role-id";
		options.Auth.AppRoleSecretId = "secret-id";

		using var cache = new MemoryCache(new MemoryCacheOptions());
		using var sut = new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(options),
			cache,
			NullLogger<VaultKeyProvider>.Instance);

		sut.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_SupportsKubernetesAuthentication_WhenJwtFileExists()
	{
		var jwtPath = Path.Combine(Path.GetTempPath(), $"vault-jwt-{Guid.NewGuid():N}.txt");
		File.WriteAllText(jwtPath, "jwt-token");
		try
		{
			var options = CreateValidOptions();
			options.Auth.AuthMethod = VaultAuthMethod.Kubernetes;
			options.Auth.KubernetesRole = "dispatch";
			options.Auth.KubernetesJwtPath = jwtPath;

			using var cache = new MemoryCache(new MemoryCacheOptions());
			using var sut = new VaultKeyProvider(
				Microsoft.Extensions.Options.Options.Create(options),
				cache,
				NullLogger<VaultKeyProvider>.Instance);

			sut.ShouldNotBeNull();
		}
		finally
		{
			if (File.Exists(jwtPath))
			{
				File.Delete(jwtPath);
			}
		}
	}

	[Fact]
	public async Task Methods_ThrowObjectDisposedException_AfterDispose()
	{
		var sut = new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<VaultKeyProvider>.Instance);

		sut.Dispose();

		_ = await Should.ThrowAsync<ObjectDisposedException>(() => sut.GetKeyAsync("key-1", CancellationToken.None));
	}

	[Fact]
	public async Task Methods_ThrowArgumentException_ForInvalidArguments()
	{
		var sut = new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<VaultKeyProvider>.Instance);

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
	}

	[Fact]
	public void PrivateHelpers_MapStatusAndNotFoundErrors()
	{
		var determineStatus = typeof(VaultKeyProvider).GetMethod(
			"DetermineKeyStatus",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		var isKeyNotFound = typeof(VaultKeyProvider).GetMethod(
			"IsKeyNotFoundException",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

		determineStatus.ShouldNotBeNull();
		isKeyNotFound.ShouldNotBeNull();

		var activeInfo = new EncryptionKeyInfo { DeletionAllowed = false };
		((KeyStatus)determineStatus!.Invoke(null, [activeInfo])!).ShouldBe(KeyStatus.Active);

		var pendingInfo = new EncryptionKeyInfo { DeletionAllowed = true };
		((KeyStatus)determineStatus.Invoke(null, [pendingInfo])!).ShouldBe(KeyStatus.PendingDestruction);

		var notFoundByStatus = new VaultApiException(System.Net.HttpStatusCode.NotFound, "missing");
		((bool)isKeyNotFound!.Invoke(null, [notFoundByStatus])!).ShouldBeTrue();

		var notFoundByMessage = new VaultApiException("no existing key named dispatch-key");
		((bool)isKeyNotFound.Invoke(null, [notFoundByMessage])!).ShouldBeTrue();

		var other = new VaultApiException(System.Net.HttpStatusCode.BadRequest, "invalid request");
		((bool)isKeyNotFound.Invoke(null, [other])!).ShouldBeFalse();
	}

	[Fact]
	public void MapToVaultKeyType_DefaultsToAesForUnknownAlgorithm()
	{
		var mapToVaultKeyType = typeof(VaultKeyProvider).GetMethod(
			"MapToVaultKeyType",
			BindingFlags.NonPublic | BindingFlags.Static);
		mapToVaultKeyType.ShouldNotBeNull();

		var mapped = (TransitKeyType)mapToVaultKeyType!.Invoke(
			null,
			[(EncryptionAlgorithm)999])!;

		mapped.ShouldBe(TransitKeyType.aes256_gcm96);
	}

	[Fact]
	public void MapToKeyMetadata_ProjectsExpectedFields()
	{
		var mapToMetadata = typeof(VaultKeyProvider).GetMethod(
			"MapToKeyMetadata",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		mapToMetadata.ShouldNotBeNull();

		var created = DateTimeOffset.UtcNow.AddDays(-3);
		var keyInfo = new EncryptionKeyInfo
		{
			LatestVersion = 4,
			Type = TransitKeyType.aes256_gcm96,
			DeletionAllowed = false,
			Keys = new Dictionary<string, object>
			{
				["1"] = new Dictionary<string, object>
				{
					["creation_time"] = created
				}
			}
		};

		using var sut = new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<VaultKeyProvider>.Instance);

		var metadata = (KeyMetadata)mapToMetadata!.Invoke(sut, ["vault-key", keyInfo, 9])!;

		metadata.KeyId.ShouldBe("vault-key");
		metadata.Version.ShouldBe(9);
		metadata.Status.ShouldBe(KeyStatus.Active);
		metadata.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		metadata.CreatedAt.ShouldBe(created);
		metadata.IsFipsCompliant.ShouldBeTrue();
	}

	[Fact]
	public void MapToKeyMetadata_ParsesCreationTimeString()
	{
		var mapToMetadata = typeof(VaultKeyProvider).GetMethod(
			"MapToKeyMetadata",
			BindingFlags.NonPublic | BindingFlags.Instance);
		mapToMetadata.ShouldNotBeNull();

		var keyInfo = new EncryptionKeyInfo
		{
			LatestVersion = 2,
			Type = TransitKeyType.aes256_gcm96,
			DeletionAllowed = false,
			Keys = new Dictionary<string, object>
			{
				["1"] = new Dictionary<string, object>
				{
					["creation_time"] = "2026-01-01T12:00:00Z"
				}
			}
		};

		using var sut = new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<VaultKeyProvider>.Instance);

		var metadata = (KeyMetadata)mapToMetadata!.Invoke(sut, ["vault-key", keyInfo, null])!;

		metadata.CreatedAt.ShouldBe(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
	}

	[Fact]
	public void MapToKeyMetadata_DefaultsAlgorithm_ForUnknownTransitKeyType()
	{
		var mapToMetadata = typeof(VaultKeyProvider).GetMethod(
			"MapToKeyMetadata",
			BindingFlags.NonPublic | BindingFlags.Instance);
		mapToMetadata.ShouldNotBeNull();

		var keyInfo = new EncryptionKeyInfo
		{
			LatestVersion = 1,
			Type = (TransitKeyType)999,
			DeletionAllowed = false,
			Keys = new Dictionary<string, object>(),
		};

		using var sut = new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<VaultKeyProvider>.Instance);

		var metadata = (KeyMetadata)mapToMetadata!.Invoke(sut, ["vault-key", keyInfo, null])!;

		metadata.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
	}

	[Fact]
	public void Dispose_IsIdempotent()
	{
		var sut = new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<VaultKeyProvider>.Instance);

		sut.Dispose();
		sut.Dispose();
	}

	[Fact]
	public async Task GetKeyAsync_ReturnsMetadata_AndUsesCache()
	{
		var setup = CreateProviderWithTransit();
		var keyInfo = CreateKeyInfo(latestVersion: 3, includeVersionKeys: true);

		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = keyInfo }));

		var first = await setup.Provider.GetKeyAsync("orders", CancellationToken.None);
		var second = await setup.Provider.GetKeyAsync("orders", CancellationToken.None);

		first.ShouldNotBeNull();
		first!.KeyId.ShouldBe("orders");
		first.Version.ShouldBe(3);
		second.ShouldNotBeNull();
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetKeyVersionAsync_ReturnsMetadata_WhenRequestedVersionExists()
	{
		var setup = CreateProviderWithTransit();
		var keyInfo = CreateKeyInfo(latestVersion: 5, includeVersionKeys: true);

		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = keyInfo }));

		var result = await setup.Provider.GetKeyVersionAsync("orders", 2, CancellationToken.None);

		result.ShouldNotBeNull();
		result!.Version.ShouldBe(2);
	}

	[Fact]
	public async Task GetKeyVersionAsync_ReturnsNull_WhenRequestedVersionMissing()
	{
		var setup = CreateProviderWithTransit();
		var keyInfo = CreateKeyInfo(latestVersion: 1, includeVersionKeys: true);

		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = keyInfo }));

		var result = await setup.Provider.GetKeyVersionAsync("orders", 99, CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetKeyVersionAsync_ReturnsNull_WhenVersionMapIsMissing()
	{
		var setup = CreateProviderWithTransit();
		var keyInfo = CreateKeyInfo(latestVersion: 1, includeVersionKeys: false);

		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = keyInfo }));

		var result = await setup.Provider.GetKeyVersionAsync("orders", 1, CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetKeyAsync_ReturnsNull_WhenTransitSecretHasNoData()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = default! }));

		var result = await setup.Provider.GetKeyAsync("orders", CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetKeyAsync_ReturnsNull_WhenTransitThrowsNotFound()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Throws(new VaultApiException(System.Net.HttpStatusCode.NotFound, "missing"));

		var result = await setup.Provider.GetKeyAsync("orders", CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetKeyVersionAsync_ReturnsNull_WhenTransitSecretHasNoData()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = default! }));

		var result = await setup.Provider.GetKeyVersionAsync("orders", 1, CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetKeyVersionAsync_ReturnsNull_WhenTransitThrowsNotFound()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Throws(new VaultApiException(System.Net.HttpStatusCode.NotFound, "missing"));

		var result = await setup.Provider.GetKeyVersionAsync("orders", 1, CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetKeyVersionAsync_UsesCache_OnRepeatedReads()
	{
		var setup = CreateProviderWithTransit();
		var keyInfo = CreateKeyInfo(latestVersion: 4, includeVersionKeys: true);

		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = keyInfo }));

		var first = await setup.Provider.GetKeyVersionAsync("orders", 1, CancellationToken.None);
		var second = await setup.Provider.GetKeyVersionAsync("orders", 1, CancellationToken.None);

		first.ShouldNotBeNull();
		second.ShouldNotBeNull();
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ListKeysAsync_ReturnsPrefixedKeys()
	{
		var setup = CreateProviderWithTransit();
		var listInfo = new ListInfo { Keys = ["dispatch-orders", "external-key", "dispatch-billing"] };

		A.CallTo(() => setup.Transit.ReadAllEncryptionKeysAsync("transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<ListInfo> { Data = listInfo }));
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = CreateKeyInfo(latestVersion: 2, includeVersionKeys: true) }));
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-billing", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = CreateKeyInfo(latestVersion: 7, includeVersionKeys: true) }));

		var results = await setup.Provider.ListKeysAsync(KeyStatus.Active, null, CancellationToken.None);

		results.Count.ShouldBe(2);
		results.Select(r => r.KeyId).ShouldContain("orders");
		results.Select(r => r.KeyId).ShouldContain("billing");
	}

	[Fact]
	public async Task ListKeysAsync_ReturnsEmpty_WhenListHasNoData()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadAllEncryptionKeysAsync("transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<ListInfo> { Data = default! }));

		var results = await setup.Provider.ListKeysAsync(null, null, CancellationToken.None);

		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task ListKeysAsync_SkipsMissingKeyDetails_WhenReadThrowsNotFound()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadAllEncryptionKeysAsync("transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<ListInfo> { Data = new ListInfo { Keys = ["dispatch-orders"] } }));
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Throws(new VaultApiException(System.Net.HttpStatusCode.NotFound, "missing"));

		var results = await setup.Provider.ListKeysAsync(null, null, CancellationToken.None);

		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task ListKeysAsync_SkipsKeys_WhenTransitReturnsNullData()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadAllEncryptionKeysAsync("transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<ListInfo> { Data = new ListInfo { Keys = ["dispatch-orders"] } }));
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = default! }));

		var results = await setup.Provider.ListKeysAsync(null, null, CancellationToken.None);

		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task ListKeysAsync_AppliesStatusFilter()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadAllEncryptionKeysAsync("transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<ListInfo>
			{
				Data = new ListInfo { Keys = ["dispatch-active", "dispatch-pending"] }
			}));
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-active", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo>
			{
				Data = CreateKeyInfo(2, includeVersionKeys: true)
			}));
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-pending", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo>
			{
				Data = new EncryptionKeyInfo
				{
					LatestVersion = 3,
					Type = TransitKeyType.aes256_gcm96,
					DeletionAllowed = true,
					Keys = new Dictionary<string, object>(),
				}
			}));

		var results = await setup.Provider.ListKeysAsync(KeyStatus.Active, null, CancellationToken.None);

		results.Count.ShouldBe(1);
		results[0].KeyId.ShouldBe("active");
	}

	[Fact]
	public async Task ListKeysAsync_AppliesPurposeFilter()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadAllEncryptionKeysAsync("transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<ListInfo>
			{
				Data = new ListInfo { Keys = ["dispatch-orders"] }
			}));
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo>
			{
				Data = CreateKeyInfo(1, includeVersionKeys: true)
			}));

		var results = await setup.Provider.ListKeysAsync(null, "orders", CancellationToken.None);

		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetActiveKeyAsync_ReturnsMostRecentKey()
	{
		var setup = CreateProviderWithTransit();
		var oldCreated = DateTimeOffset.UtcNow.AddDays(-10);
		var newCreated = DateTimeOffset.UtcNow.AddDays(-1);

		A.CallTo(() => setup.Transit.ReadAllEncryptionKeysAsync("transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<ListInfo>
			{
				Data = new ListInfo { Keys = ["dispatch-old", "dispatch-new"] }
			}));
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-old", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo>
			{
				Data = CreateKeyInfo(2, includeVersionKeys: true, createdOn: oldCreated)
			}));
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-new", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo>
			{
				Data = CreateKeyInfo(3, includeVersionKeys: true, createdOn: newCreated)
			}));

		var active = await setup.Provider.GetActiveKeyAsync(null, CancellationToken.None);

		active.ShouldNotBeNull();
		active!.KeyId.ShouldBe("new");
	}

	[Fact]
	public async Task GetActiveKeyAsync_UsesCache_ForSamePurpose()
	{
		var setup = CreateProviderWithTransit();
		var created = DateTimeOffset.UtcNow.AddMinutes(-5);

		A.CallTo(() => setup.Transit.ReadAllEncryptionKeysAsync("transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<ListInfo>
			{
				Data = new ListInfo { Keys = ["dispatch-orders"] }
			}));
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo>
			{
				Data = CreateKeyInfo(1, includeVersionKeys: true, createdOn: created)
			}));

		var first = await setup.Provider.GetActiveKeyAsync(null, CancellationToken.None);
		var second = await setup.Provider.GetActiveKeyAsync(null, CancellationToken.None);

		first.ShouldNotBeNull();
		second.ShouldNotBeNull();
		A.CallTo(() => setup.Transit.ReadAllEncryptionKeysAsync("transit", A<string?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetActiveKeyAsync_ReturnsNull_WhenNoMatchingActiveKeys()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadAllEncryptionKeysAsync("transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<ListInfo>
			{
				Data = new ListInfo { Keys = ["dispatch-orders"] }
			}));
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo>
			{
				Data = CreateKeyInfo(1, includeVersionKeys: true)
			}));

		var active = await setup.Provider.GetActiveKeyAsync("orders", CancellationToken.None);

		active.ShouldBeNull();
	}

	[Fact]
	public async Task RotateKeyAsync_ReturnsFailure_WhenVaultThrows()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Throws(new VaultApiException(System.Net.HttpStatusCode.BadRequest, "invalid"));

		var result = await setup.Provider.RotateKeyAsync(
			"orders",
			EncryptionAlgorithm.Aes256Gcm,
			null,
			null,
			CancellationToken.None);

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Vault error");
	}

	[Fact]
	public async Task RotateKeyAsync_RotatesExistingKey_WhenKeyExists()
	{
		var setup = CreateProviderWithTransit();
		var existing = CreateKeyInfo(latestVersion: 2, includeVersionKeys: true);
		var rotated = CreateKeyInfo(latestVersion: 3, includeVersionKeys: true);

		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.ReturnsNextFromSequence(
				Task.FromResult(new Secret<EncryptionKeyInfo> { Data = existing }),
				Task.FromResult(new Secret<EncryptionKeyInfo> { Data = rotated }));
		A.CallTo(() => setup.Transit.RotateEncryptionKeyAsync("dispatch-orders", "transit"))
			.Returns(Task.CompletedTask);

		var result = await setup.Provider.RotateKeyAsync(
			"orders",
			EncryptionAlgorithm.Aes256Gcm,
			"orders",
			null,
			CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NewKey.ShouldNotBeNull();
		result.PreviousKey.ShouldNotBeNull();
		result.NewKey!.Version.ShouldBe(3);
		result.PreviousKey!.Version.ShouldBe(2);
	}

	[Fact]
	public async Task RotateKeyAsync_CreatesKey_WhenKeyDoesNotExist()
	{
		var setup = CreateProviderWithTransit();
		var created = CreateKeyInfo(latestVersion: 1, includeVersionKeys: true);
		CreateKeyRequestOptions? capturedCreateOptions = null;

		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.ReturnsNextFromSequence(
				Task.FromException<Secret<EncryptionKeyInfo>>(new VaultApiException(System.Net.HttpStatusCode.NotFound, "missing")),
				Task.FromResult(new Secret<EncryptionKeyInfo> { Data = created }));
		A.CallTo(() => setup.Transit.CreateEncryptionKeyAsync("dispatch-orders", A<CreateKeyRequestOptions>._, "transit"))
			.Invokes((string _, CreateKeyRequestOptions options, string _) => capturedCreateOptions = options)
			.Returns(Task.CompletedTask);

		var result = await setup.Provider.RotateKeyAsync(
			"orders",
			EncryptionAlgorithm.Aes256Gcm,
			"orders",
			null,
			CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NewKey.ShouldNotBeNull();
		result.PreviousKey.ShouldBeNull();
		capturedCreateOptions.ShouldNotBeNull();
		capturedCreateOptions!.Type.ShouldBe(TransitKeyType.aes256_gcm96);
	}

	[Fact]
	public async Task RotateKeyAsync_ReturnsFailure_WhenUnexpectedExceptionOccurs()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Throws(new InvalidOperationException("unexpected"));

		var result = await setup.Provider.RotateKeyAsync(
			"orders",
			EncryptionAlgorithm.Aes256Gcm,
			null,
			null,
			CancellationToken.None);

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("unexpected");
	}

	[Fact]
	public async Task DeleteKeyAsync_ReturnsTrue_WhenDeleteSucceeds()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.UpdateEncryptionKeyConfigAsync("dispatch-orders", A<UpdateKeyRequestOptions>._, "transit"))
			.Returns(Task.CompletedTask);
		A.CallTo(() => setup.Transit.DeleteEncryptionKeyAsync("dispatch-orders", "transit"))
			.Returns(Task.CompletedTask);

		var deleted = await setup.Provider.DeleteKeyAsync("orders", 30, CancellationToken.None);

		deleted.ShouldBeTrue();
	}

	[Fact]
	public async Task DeleteKeyAsync_ReturnsFalse_WhenKeyNotFound()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.UpdateEncryptionKeyConfigAsync("dispatch-orders", A<UpdateKeyRequestOptions>._, "transit"))
			.Throws(new VaultApiException(System.Net.HttpStatusCode.NotFound, "missing"));

		var deleted = await setup.Provider.DeleteKeyAsync("orders", 30, CancellationToken.None);

		deleted.ShouldBeFalse();
	}

	[Fact]
	public async Task SuspendKeyAsync_ReturnsTrue_WhenUpdateSucceeds()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = CreateKeyInfo(4, includeVersionKeys: true) }));
		A.CallTo(() => setup.Transit.UpdateEncryptionKeyConfigAsync("dispatch-orders", A<UpdateKeyRequestOptions>._, "transit"))
			.Returns(Task.CompletedTask);

		var suspended = await setup.Provider.SuspendKeyAsync("orders", "maintenance", CancellationToken.None);

		suspended.ShouldBeTrue();
	}

	[Fact]
	public async Task SuspendKeyAsync_ReturnsFalse_WhenKeyNotFound()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Throws(new VaultApiException(System.Net.HttpStatusCode.NotFound, "missing"));

		var suspended = await setup.Provider.SuspendKeyAsync("orders", "maintenance", CancellationToken.None);

		suspended.ShouldBeFalse();
	}

	[Fact]
	public async Task SuspendKeyAsync_ReturnsFalse_WhenTransitReturnsNoData()
	{
		var setup = CreateProviderWithTransit();
		A.CallTo(() => setup.Transit.ReadEncryptionKeyAsync("dispatch-orders", "transit", A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = default! }));

		var suspended = await setup.Provider.SuspendKeyAsync("orders", "maintenance", CancellationToken.None);

		suspended.ShouldBeFalse();
	}

	private static VaultOptions CreateValidOptions() =>
		new()
		{
			VaultUri = new Uri("http://127.0.0.1:8200"),
			KeyNamePrefix = "dispatch-",
			Auth =
			{
				AuthMethod = VaultAuthMethod.Token,
				Token = "unit-token"
			}
		};

	private static (VaultKeyProvider Provider, ITransitSecretsEngine Transit) CreateProviderWithTransit()
	{
		var provider = new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(CreateValidOptions()),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<VaultKeyProvider>.Instance);

		var transit = A.Fake<ITransitSecretsEngine>();
		var secrets = A.Fake<ISecretsEngine>();
		var v1 = A.Fake<IVaultClientV1>();
		var client = new VaultClient(new VaultClientSettings("http://127.0.0.1:8200", new TokenAuthMethodInfo("unit-token")));
		A.CallTo(() => v1.Secrets).Returns(secrets);
		A.CallTo(() => secrets.Transit).Returns(transit);

		SetPrivateField(client, "<V1>k__BackingField", v1);
		SetPrivateField(provider, "_vaultClient", client);

		return (provider, transit);
	}

	private static EncryptionKeyInfo CreateKeyInfo(int latestVersion, bool includeVersionKeys, DateTimeOffset? createdOn = null)
	{
		var keys = new Dictionary<string, object>(StringComparer.Ordinal);
		if (includeVersionKeys)
		{
			keys["1"] = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["creation_time"] = createdOn ?? DateTimeOffset.UtcNow.AddDays(-1)
			};
			keys["2"] = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["creation_time"] = createdOn ?? DateTimeOffset.UtcNow
			};
		}

		return new EncryptionKeyInfo
		{
			LatestVersion = latestVersion,
			Type = TransitKeyType.aes256_gcm96,
			DeletionAllowed = false,
			Keys = keys
		};
	}

	private static void SetPrivateField<T>(object instance, string fieldName, T value)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(instance, value);
	}
}
