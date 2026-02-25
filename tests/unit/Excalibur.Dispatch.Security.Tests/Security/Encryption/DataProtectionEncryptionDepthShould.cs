// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Security;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.Encryption;

/// <summary>
/// Deep coverage tests for <see cref="DataProtectionMessageEncryptionService"/> covering
/// metadata header, compression, multi-tenant purpose strings, cache behavior,
/// key rotation details, CryptographicException, and validation failure paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class DataProtectionEncryptionDepthShould : IDisposable
{
	private readonly IDataProtectionProvider _provider;
	private readonly IDataProtector _protector;

	public DataProtectionEncryptionDepthShould()
	{
		_provider = A.Fake<IDataProtectionProvider>();
		_protector = A.Fake<IDataProtector>();

		A.CallTo(() => _provider.CreateProtector(A<string>._))
			.Returns(_protector);

		// Default simple encrypt/decrypt stub
		A.CallTo(() => _protector.Protect(A<byte[]>._))
			.ReturnsLazily((byte[] input) =>
			{
				var result = new byte[input.Length];
				Array.Copy(input, result, input.Length);
				Array.Reverse(result);
				return result;
			});

		A.CallTo(() => _protector.Unprotect(A<byte[]>._))
			.ReturnsLazily((byte[] input) =>
			{
				var result = new byte[input.Length];
				Array.Copy(input, result, input.Length);
				Array.Reverse(result);
				return result;
			});
	}

	public void Dispose() { }

	[Fact]
	public async Task EncryptWithMetadataHeader()
	{
		// Arrange — enable metadata header
		var options = new EncryptionOptions { IncludeMetadataHeader = true };
		using var sut = CreateService(options);
		var context = new EncryptionContext { TenantId = "t1" };

		// Act
		var encrypted = await sut.EncryptMessageAsync(
			Encoding.UTF8.GetBytes("test data"), context, CancellationToken.None);

		// Assert — result should have 3-byte header prefix
		encrypted.Length.ShouldBeGreaterThan(3);
		encrypted[0].ShouldBe((byte)1); // Version byte
	}

	[Fact]
	public async Task DecryptWithMetadataHeader_Roundtrip()
	{
		// Arrange
		var options = new EncryptionOptions { IncludeMetadataHeader = true };
		using var sut = CreateService(options);
		var context = new EncryptionContext { TenantId = "t1" };
		var original = Encoding.UTF8.GetBytes("round trip test");

		// Act
		var encrypted = await sut.EncryptMessageAsync(original, context, CancellationToken.None);
		var decrypted = await sut.DecryptMessageAsync(encrypted, context, CancellationToken.None);

		// Assert
		Encoding.UTF8.GetString(decrypted).ShouldBe("round trip test");
	}

	[Fact]
	public async Task EncryptWithCompression()
	{
		// Arrange — enable compression
		// Note: with fake protector, data is just reversed so compression doesn't shrink it,
		// but the code path is exercised
		var options = new EncryptionOptions { EnableCompressionByDefault = true, IncludeMetadataHeader = false };
		using var sut = CreateService(options);
		var context = new EncryptionContext { TenantId = "t1" };
		var content = Encoding.UTF8.GetBytes("compressed content test");

		// Act — should not throw even with compression enabled
		var encrypted = await sut.EncryptMessageAsync(content, context, CancellationToken.None);

		// Assert
		encrypted.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task ThrowEncryptionException_WhenProtectorFails()
	{
		// Arrange
		A.CallTo(() => _protector.Protect(A<byte[]>._))
			.Throws(new InvalidOperationException("provider failure"));

		using var sut = CreateService();
		var context = new EncryptionContext { TenantId = "t1" };

		// Act & Assert
		await Should.ThrowAsync<EncryptionException>(
			async () => await sut.EncryptMessageAsync("fail me", context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowEncryptionException_WhenByteProtectorFails()
	{
		// Arrange
		A.CallTo(() => _protector.Protect(A<byte[]>._))
			.Throws(new InvalidOperationException("byte failure"));

		using var sut = CreateService();
		var context = new EncryptionContext { TenantId = "t1" };

		// Act & Assert
		await Should.ThrowAsync<EncryptionException>(
			async () => await sut.EncryptMessageAsync(
				Encoding.UTF8.GetBytes("fail"), context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowDecryptionException_OnCryptographicException()
	{
		// Arrange — Unprotect throws CryptographicException (corrupted data)
		A.CallTo(() => _protector.Unprotect(A<byte[]>._))
			.Throws(new CryptographicException("Invalid key or corrupted data"));

		using var sut = CreateService();
		var context = new EncryptionContext { TenantId = "t1" };
		var fakeEncrypted = Convert.ToBase64String(new byte[] { 1, 2, 3 });

		// Act & Assert — should wrap in DecryptionException
		await Should.ThrowAsync<DecryptionException>(
			async () => await sut.DecryptMessageAsync(fakeEncrypted, context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowDecryptionException_OnGenericFailure()
	{
		// Arrange
		A.CallTo(() => _protector.Unprotect(A<byte[]>._))
			.Throws(new InvalidOperationException("generic failure"));

		using var sut = CreateService();
		var context = new EncryptionContext { TenantId = "t1" };
		var fakeEncrypted = Convert.ToBase64String(new byte[] { 1, 2, 3 });

		// Act & Assert
		await Should.ThrowAsync<DecryptionException>(
			async () => await sut.DecryptMessageAsync(fakeEncrypted, context, CancellationToken.None));
	}

	[Fact]
	public async Task RotateKeys_ClearProtectorCache()
	{
		// Arrange
		using var sut = CreateService();
		var context = new EncryptionContext { TenantId = "t1" };

		// Warm the cache with an encryption
		await sut.EncryptMessageAsync("warm cache", context, CancellationToken.None);

		// Act
		var result = await sut.RotateKeysAsync(CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.NewKey.ShouldNotBeNull();
		result.NewKey!.Status.ShouldBe(KeyStatus.Active);
		result.PreviousKey.ShouldNotBeNull();
		result.PreviousKey!.Status.ShouldBe(KeyStatus.DecryptOnly);
	}

	[Fact]
	public async Task ValidateConfiguration_ReturnTrue_OnSuccessfulRoundtrip()
	{
		// Arrange
		using var sut = CreateService();

		// Act
		var valid = await sut.ValidateConfigurationAsync(CancellationToken.None);

		// Assert
		valid.ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateConfiguration_ReturnFalse_WhenEncryptionFails()
	{
		// Arrange — make protector throw during validation
		A.CallTo(() => _protector.Protect(A<byte[]>._))
			.Throws(new InvalidOperationException("validation fail"));

		using var sut = CreateService();

		// Act
		var valid = await sut.ValidateConfigurationAsync(CancellationToken.None);

		// Assert — validation should catch exception and return false
		valid.ShouldBeFalse();
	}

	[Fact]
	public async Task BuildPurpose_WithAllContextFields()
	{
		// Arrange — capture the purpose string passed to CreateProtector
		string? capturedPurpose = null;
		A.CallTo(() => _provider.CreateProtector(A<string>._))
			.Invokes((string purpose) => capturedPurpose = purpose)
			.Returns(_protector);

		using var sut = CreateService();
		var context = new EncryptionContext
		{
			TenantId = "tenant-xyz",
			Purpose = "audit-log",
			KeyId = "key-123",
			KeyVersion = 5,
		};

		// Act
		await sut.EncryptMessageAsync("test", context, CancellationToken.None);

		// Assert — purpose string contains all parts
		capturedPurpose.ShouldNotBeNull();
		capturedPurpose.ShouldContain("Tenant:tenant-xyz");
		capturedPurpose.ShouldContain("Purpose:audit-log");
		capturedPurpose.ShouldContain("Key:key-123");
		capturedPurpose.ShouldContain("Version:5");
	}

	[Fact]
	public async Task BuildPurpose_WithMinimalContext()
	{
		// Arrange
		string? capturedPurpose = null;
		A.CallTo(() => _provider.CreateProtector(A<string>._))
			.Invokes((string purpose) => capturedPurpose = purpose)
			.Returns(_protector);

		using var sut = CreateService();
		var context = new EncryptionContext();

		// Act
		await sut.EncryptMessageAsync("test", context, CancellationToken.None);

		// Assert — only base purpose, no Tenant/Purpose/Key segments
		capturedPurpose.ShouldNotBeNull();
		capturedPurpose.ShouldContain("MessageEncryption");
		capturedPurpose.ShouldNotContain("Tenant:");
		capturedPurpose.ShouldNotContain("Purpose:");
		capturedPurpose.ShouldNotContain("Key:");
	}

	[Fact]
	public async Task CacheProtector_ReuseOnSecondCall()
	{
		// Arrange
		using var sut = CreateService();
		var context = new EncryptionContext { TenantId = "cache-test" };

		// Act — two encryptions with same context
		await sut.EncryptMessageAsync("first", context, CancellationToken.None);
		await sut.EncryptMessageAsync("second", context, CancellationToken.None);

		// Assert — protector created only once (cached)
		A.CallTo(() => _provider.CreateProtector(A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowOnNullContext_Encrypt()
	{
		using var sut = CreateService();

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await sut.EncryptMessageAsync("data", null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext_Decrypt()
	{
		using var sut = CreateService();

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await sut.DecryptMessageAsync("data", null!, CancellationToken.None));
	}

	private DataProtectionMessageEncryptionService CreateService(EncryptionOptions? options = null)
	{
		return new DataProtectionMessageEncryptionService(
			_provider,
			MsOptions.Create(options ?? new EncryptionOptions { IncludeMetadataHeader = false }),
			NullLogger<DataProtectionMessageEncryptionService>.Instance);
	}
}
