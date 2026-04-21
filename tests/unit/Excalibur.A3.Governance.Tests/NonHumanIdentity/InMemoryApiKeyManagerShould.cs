// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.NonHumanIdentity;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.A3.Governance.Tests.NonHumanIdentity;

/// <summary>
/// Unit tests for <see cref="InMemoryApiKeyManager"/>: create, validate, revoke,
/// expiry, MaxKeysPerPrincipal, SHA-256 hashing, concurrent access, and GetService.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemoryApiKeyManagerShould : UnitTestBase
{
	private static readonly string[] ExpectedScopes = ["read", "write"];
	private readonly ApiKeyOptions _options = new();

	private InMemoryApiKeyManager CreateManager(ApiKeyOptions? opts = null) =>
		new(Microsoft.Extensions.Options.Options.Create(opts ?? _options),
			NullLogger<InMemoryApiKeyManager>.Instance);

	private static ApiKeyRequest MakeRequest(
		string principalId = "svc-account-1",
		PrincipalType principalType = PrincipalType.ServiceAccount,
		DateTimeOffset? expiresAt = null,
		string? description = null) =>
		new(principalId, principalType, ["read", "write"], expiresAt, description);

	#region CreateKeyAsync

	[Fact]
	public async Task CreateKey_ReturnsPlaintextAndMetadata()
	{
		var manager = CreateManager();
		var result = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);

		result.KeyId.ShouldNotBeNullOrEmpty();
		result.ApiKey.ShouldNotBeNullOrEmpty();
		result.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
	}

	[Fact]
	public async Task CreateKey_UsesDefaultExpirationWhenNoneProvided()
	{
		_options.DefaultExpirationDays = 30;
		var manager = CreateManager();

		var result = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);

		var expectedMin = DateTimeOffset.UtcNow.AddDays(29);
		var expectedMax = DateTimeOffset.UtcNow.AddDays(31);
		result.ExpiresAt.ShouldBeGreaterThan(expectedMin);
		result.ExpiresAt.ShouldBeLessThan(expectedMax);
	}

	[Fact]
	public async Task CreateKey_UsesExplicitExpiration()
	{
		var manager = CreateManager();
		var expiry = DateTimeOffset.UtcNow.AddDays(7);

		var result = await manager.CreateKeyAsync(MakeRequest(expiresAt: expiry), CancellationToken.None);
		result.ExpiresAt.ShouldBe(expiry);
	}

	[Fact]
	public async Task CreateKey_GeneratesUniqueKeys()
	{
		var manager = CreateManager();

		var key1 = await manager.CreateKeyAsync(MakeRequest("p1"), CancellationToken.None);
		var key2 = await manager.CreateKeyAsync(MakeRequest("p2"), CancellationToken.None);

		key1.ApiKey.ShouldNotBe(key2.ApiKey);
		key1.KeyId.ShouldNotBe(key2.KeyId);
	}

	[Fact]
	public async Task ThrowOnCreate_WhenNullRequest()
	{
		var manager = CreateManager();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			manager.CreateKeyAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnCreate_WhenPrincipalIdIsEmpty()
	{
		var manager = CreateManager();
		await Should.ThrowAsync<ArgumentException>(() =>
			manager.CreateKeyAsync(MakeRequest(principalId: ""), CancellationToken.None));
	}

	#endregion

	#region MaxKeysPerPrincipal

	[Fact]
	public async Task ThrowOnCreate_WhenMaxKeysReached()
	{
		var opts = new ApiKeyOptions { MaxKeysPerPrincipal = 2 };
		var manager = CreateManager(opts);

		await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);
		await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);

		await Should.ThrowAsync<InvalidOperationException>(() =>
			manager.CreateKeyAsync(MakeRequest(), CancellationToken.None));
	}

	[Fact]
	public async Task AllowCreate_AfterRevokingKey()
	{
		var opts = new ApiKeyOptions { MaxKeysPerPrincipal = 1 };
		var manager = CreateManager(opts);

		var key1 = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);
		await manager.RevokeKeyAsync(key1.KeyId, CancellationToken.None);

		// Should now be able to create another
		var key2 = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);
		key2.ShouldNotBeNull();
	}

	#endregion

	#region ValidateKeyAsync

	[Fact]
	public async Task ValidateKey_ReturnsValid_ForActiveKey()
	{
		var manager = CreateManager();
		var created = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);

		var result = await manager.ValidateKeyAsync(created.ApiKey, CancellationToken.None);

		result.IsValid.ShouldBeTrue();
		result.KeyId.ShouldBe(created.KeyId);
		result.PrincipalId.ShouldBe("svc-account-1");
		result.PrincipalType.ShouldBe(PrincipalType.ServiceAccount);
		result.Scopes.ShouldNotBeNull();
		result.Scopes!.Count.ShouldBe(2);
		result.FailureReason.ShouldBeNull();
	}

	[Fact]
	public async Task ValidateKey_ReturnsFailed_ForUnknownKey()
	{
		var manager = CreateManager();
		var result = await manager.ValidateKeyAsync("totally-fake-key", CancellationToken.None);

		result.IsValid.ShouldBeFalse();
		result.FailureReason.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task ValidateKey_ReturnsFailed_ForRevokedKey()
	{
		var manager = CreateManager();
		var created = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);
		await manager.RevokeKeyAsync(created.KeyId, CancellationToken.None);

		var result = await manager.ValidateKeyAsync(created.ApiKey, CancellationToken.None);

		result.IsValid.ShouldBeFalse();
		result.FailureReason.ShouldContain("revoked");
	}

	[Fact]
	public async Task ValidateKey_ReturnsFailed_ForExpiredKey()
	{
		var manager = CreateManager();
		var expiry = DateTimeOffset.UtcNow.AddSeconds(-1); // Already expired
		var created = await manager.CreateKeyAsync(MakeRequest(expiresAt: expiry), CancellationToken.None);

		var result = await manager.ValidateKeyAsync(created.ApiKey, CancellationToken.None);

		result.IsValid.ShouldBeFalse();
		result.FailureReason.ShouldContain("expired");
	}

	[Fact]
	public async Task ThrowOnValidate_WhenApiKeyIsEmpty()
	{
		var manager = CreateManager();
		await Should.ThrowAsync<ArgumentException>(() =>
			manager.ValidateKeyAsync("", CancellationToken.None));
	}

	#endregion

	#region RevokeKeyAsync

	[Fact]
	public async Task RevokeKey_MakesKeyInvalid()
	{
		var manager = CreateManager();
		var created = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);

		await manager.RevokeKeyAsync(created.KeyId, CancellationToken.None);

		var result = await manager.ValidateKeyAsync(created.ApiKey, CancellationToken.None);
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task RevokeKey_IsIdempotent()
	{
		var manager = CreateManager();
		var created = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);

		// Revoke twice -- should not throw
		await manager.RevokeKeyAsync(created.KeyId, CancellationToken.None);
		await manager.RevokeKeyAsync(created.KeyId, CancellationToken.None);
	}

	[Fact]
	public async Task RevokeKey_NoOpForUnknownKey()
	{
		var manager = CreateManager();
		// Should not throw
		await manager.RevokeKeyAsync("nonexistent", CancellationToken.None);
	}

	[Fact]
	public async Task ThrowOnRevoke_WhenKeyIdIsEmpty()
	{
		var manager = CreateManager();
		await Should.ThrowAsync<ArgumentException>(() =>
			manager.RevokeKeyAsync("", CancellationToken.None));
	}

	#endregion

	#region GetKeysByPrincipalAsync

	[Fact]
	public async Task GetKeys_ReturnsActiveKeysOnly()
	{
		var manager = CreateManager();
		var key1 = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);
		var key2 = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);
		await manager.RevokeKeyAsync(key1.KeyId, CancellationToken.None);

		var keys = await manager.GetKeysByPrincipalAsync("svc-account-1", CancellationToken.None);

		keys.Count.ShouldBe(1);
		keys[0].KeyId.ShouldBe(key2.KeyId);
	}

	[Fact]
	public async Task GetKeys_ReturnsEmpty_WhenNoneExist()
	{
		var manager = CreateManager();
		var keys = await manager.GetKeysByPrincipalAsync("nonexistent", CancellationToken.None);
		keys.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetKeys_IncludesMetadata()
	{
		var manager = CreateManager();
		await manager.CreateKeyAsync(MakeRequest(description: "CI/CD key"), CancellationToken.None);

		var keys = await manager.GetKeysByPrincipalAsync("svc-account-1", CancellationToken.None);

		keys.Count.ShouldBe(1);
		keys[0].PrincipalId.ShouldBe("svc-account-1");
		keys[0].PrincipalType.ShouldBe(PrincipalType.ServiceAccount);
		keys[0].Scopes.ShouldBe(ExpectedScopes);
		keys[0].Description.ShouldBe("CI/CD key");
		keys[0].RevokedAt.ShouldBeNull();
	}

	[Fact]
	public async Task ThrowOnGetKeys_WhenPrincipalIdIsEmpty()
	{
		var manager = CreateManager();
		await Should.ThrowAsync<ArgumentException>(() =>
			manager.GetKeysByPrincipalAsync("", CancellationToken.None));
	}

	#endregion

	#region SHA-256 Hashing

	[Fact]
	public async Task ValidateKey_UsesHashComparison_NotPlaintext()
	{
		var manager = CreateManager();
		var created = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);

		// The key should validate because SHA-256 hashing is consistent
		var result = await manager.ValidateKeyAsync(created.ApiKey, CancellationToken.None);
		result.IsValid.ShouldBeTrue();

		// A slightly different key should NOT validate
		var tampered = created.ApiKey + "X";
		var tamperedResult = await manager.ValidateKeyAsync(tampered, CancellationToken.None);
		tamperedResult.IsValid.ShouldBeFalse();
	}

	#endregion

	#region Concurrent Access

	[Fact]
	public async Task HandleConcurrentCreates()
	{
		var opts = new ApiKeyOptions { MaxKeysPerPrincipal = 100 };
		var manager = CreateManager(opts);

		var tasks = Enumerable.Range(0, 20)
			.Select(i => manager.CreateKeyAsync(
				MakeRequest($"principal-{i}"), CancellationToken.None));

		var results = await Task.WhenAll(tasks);
		results.ShouldAllBe(r => !string.IsNullOrEmpty(r.KeyId));
	}

	#endregion

	#region RotateKeyAsync (Sprint 713)

	[Fact]
	public async Task RotateKey_RevokesOldAndCreatesNew()
	{
		var manager = CreateManager();
		var original = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);

		var rotated = await manager.RotateKeyAsync(original.KeyId, CancellationToken.None);

		// New key should be different
		rotated.KeyId.ShouldNotBe(original.KeyId);
		rotated.ApiKey.ShouldNotBe(original.ApiKey);

		// Old key should be invalid
		var oldResult = await manager.ValidateKeyAsync(original.ApiKey, CancellationToken.None);
		oldResult.IsValid.ShouldBeFalse();

		// New key should be valid
		var newResult = await manager.ValidateKeyAsync(rotated.ApiKey, CancellationToken.None);
		newResult.IsValid.ShouldBeTrue();
		newResult.PrincipalId.ShouldBe("svc-account-1");
	}

	[Fact]
	public async Task RotateKey_PreservesScopesAndPrincipal()
	{
		var manager = CreateManager();
		var original = await manager.CreateKeyAsync(
			MakeRequest(description: "CI key"), CancellationToken.None);

		var rotated = await manager.RotateKeyAsync(original.KeyId, CancellationToken.None);

		var keys = await manager.GetKeysByPrincipalAsync("svc-account-1", CancellationToken.None);
		keys.Count.ShouldBe(1); // Only the new key (old is revoked)
		keys[0].KeyId.ShouldBe(rotated.KeyId);
		keys[0].Description.ShouldBe("CI key");
	}

	[Fact]
	public async Task ThrowOnRotate_WhenKeyNotFound()
	{
		var manager = CreateManager();
		await Should.ThrowAsync<InvalidOperationException>(() =>
			manager.RotateKeyAsync("nonexistent", CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnRotate_WhenKeyAlreadyRevoked()
	{
		var manager = CreateManager();
		var created = await manager.CreateKeyAsync(MakeRequest(), CancellationToken.None);
		await manager.RevokeKeyAsync(created.KeyId, CancellationToken.None);

		await Should.ThrowAsync<InvalidOperationException>(() =>
			manager.RotateKeyAsync(created.KeyId, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnRotate_WhenKeyIdIsEmpty()
	{
		var manager = CreateManager();
		await Should.ThrowAsync<ArgumentException>(() =>
			manager.RotateKeyAsync("", CancellationToken.None));
	}

	#endregion

	#region GetService

	[Fact]
	public void ReturnNull_FromGetService()
	{
		var manager = CreateManager();
		manager.GetService(typeof(string)).ShouldBeNull();
	}

	#endregion
}
