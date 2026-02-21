// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Compliance;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Encryption.Decorators;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Encryption;

/// <summary>
/// Extended unit tests for <see cref="EncryptingProjectionStoreDecorator{TProjection}"/>
/// covering additional encryption modes, query with encryption, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingProjectionStoreDecoratorExtendedShould
{
	private readonly IEncryptionProviderRegistry _registry;
	private readonly IEncryptionProvider _provider;
	private readonly CancellationToken _ct = CancellationToken.None;

	public EncryptingProjectionStoreDecoratorExtendedShould()
	{
		_registry = A.Fake<IEncryptionProviderRegistry>();
		_provider = A.Fake<IEncryptionProvider>();
	}

	/// <summary>
	/// Creates encrypted field data with EXCR magic bytes prefix.
	/// </summary>
	private static byte[] CreateEncryptedFieldBytes(byte[] ciphertext)
	{
		var envelope = new EncryptedData
		{
			Ciphertext = ciphertext,
			KeyId = "key-1",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }
		};

		var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(envelope);
		var magicBytes = new byte[] { 0x45, 0x58, 0x43, 0x52 }; // EXCR
		var result = new byte[magicBytes.Length + jsonBytes.Length];
		magicBytes.CopyTo(result, 0);
		jsonBytes.CopyTo(result, magicBytes.Length);
		return result;
	}

	/// <summary>
	/// Sets up the decryption mock to return plainData when DecryptAsync is called.
	/// </summary>
	private void SetupDecryption(byte[] plainData)
	{
		A.CallTo(() => _registry.FindDecryptionProvider(A<EncryptedData>._))
			.Returns(_provider);
		A.CallTo(() => _provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, _ct))
			.Returns(Task.FromResult(plainData));
	}

	#region Test Types

#pragma warning disable CA1034 // Nested types should not be visible

	public sealed class TestProjection
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;

		[EncryptedField]
		public byte[]? SensitiveData { get; set; }
	}

	public sealed class PlainProjection
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public byte[]? Data { get; set; }
	}

#pragma warning restore CA1034

	#endregion

	#region GetByIdAsync - Decrypt encrypted fields

	[Fact]
	public async Task GetByIdAsync_ShouldDecryptEncryptedField_WhenModeIsEncryptAndDecrypt()
	{
		// Arrange
		var innerStore = A.Fake<IProjectionStore<TestProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "test",
			DefaultTenantId = "tenant-1"
		});
		var decorator = new EncryptingProjectionStoreDecorator<TestProjection>(innerStore, _registry, options);

		var encryptedBytes = CreateEncryptedFieldBytes(new byte[] { 99, 98, 97 });
		var decryptedData = new byte[] { 10, 20, 30 };
		var projection = new TestProjection { Id = "p1", Name = "Test", SensitiveData = encryptedBytes };

		A.CallTo(() => innerStore.GetByIdAsync("p1", _ct))
			.Returns(Task.FromResult<TestProjection?>(projection));
		SetupDecryption(decryptedData);

		// Act
		var result = await decorator.GetByIdAsync("p1", _ct);

		// Assert
		result.ShouldNotBeNull();
		result.SensitiveData.ShouldBe(decryptedData);
	}

	[Fact]
	public async Task GetByIdAsync_ShouldThrowEncryptionException_WhenNoProviderForDecryption()
	{
		// Arrange
		var innerStore = A.Fake<IProjectionStore<TestProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "test"
		});
		var decorator = new EncryptingProjectionStoreDecorator<TestProjection>(innerStore, _registry, options);

		var encryptedBytes = CreateEncryptedFieldBytes(new byte[] { 99 });
		var projection = new TestProjection { Id = "p1", Name = "Test", SensitiveData = encryptedBytes };

		A.CallTo(() => innerStore.GetByIdAsync("p1", _ct))
			.Returns(Task.FromResult<TestProjection?>(projection));
		A.CallTo(() => _registry.FindDecryptionProvider(A<EncryptedData>._))
			.Returns((IEncryptionProvider?)null);

		// Act & Assert
		await Should.ThrowAsync<EncryptionException>(async () =>
			await decorator.GetByIdAsync("p1", _ct));
	}

	#endregion

	#region UpsertAsync - EncryptNewDecryptAll Mode

	[Fact]
	public async Task UpsertAsync_ShouldEncryptFields_WhenModeIsEncryptNewDecryptAll()
	{
		// Arrange
		var innerStore = A.Fake<IProjectionStore<TestProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptNewDecryptAll,
			DefaultPurpose = "test",
			DefaultTenantId = "tenant-1"
		});
		var decorator = new EncryptingProjectionStoreDecorator<TestProjection>(innerStore, _registry, options);

		var plainData = new byte[] { 10, 20, 30 };
		var projection = new TestProjection { Id = "p1", Name = "Test", SensitiveData = plainData };

		var encryptedData = new EncryptedData
		{
			Ciphertext = new byte[] { 99, 98, 97 },
			KeyId = "key-1",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = new byte[] { 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }
		};
		A.CallTo(() => _registry.GetPrimary()).Returns(_provider);
		A.CallTo(() => _provider.EncryptAsync(plainData, A<EncryptionContext>._, _ct))
			.Returns(Task.FromResult(encryptedData));

		// Act
		await decorator.UpsertAsync("p1", projection, _ct);

		// Assert
		A.CallTo(() => innerStore.UpsertAsync("p1", projection, _ct))
			.MustHaveHappenedOnceExactly();
		projection.SensitiveData.ShouldNotBeNull();
		// Magic bytes check
		projection.SensitiveData[0].ShouldBe((byte)0x45);
		projection.SensitiveData[1].ShouldBe((byte)0x58);
		projection.SensitiveData[2].ShouldBe((byte)0x43);
		projection.SensitiveData[3].ShouldBe((byte)0x52);
	}

	#endregion

	#region DeleteAsync - Additional Modes

	[Fact]
	public async Task DeleteAsync_ShouldDelegateToInner_WhenModeIsDecryptOnlyWritePlaintext()
	{
		// Arrange
		var innerStore = A.Fake<IProjectionStore<TestProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.DecryptOnlyWritePlaintext,
			DefaultPurpose = "test"
		});
		var decorator = new EncryptingProjectionStoreDecorator<TestProjection>(innerStore, _registry, options);

		// Act
		await decorator.DeleteAsync("p1", _ct);

		// Assert
		A.CallTo(() => innerStore.DeleteAsync("p1", _ct))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteAsync_ShouldDelegateToInner_WhenModeIsEncryptNewDecryptAll()
	{
		// Arrange
		var innerStore = A.Fake<IProjectionStore<TestProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptNewDecryptAll,
			DefaultPurpose = "test"
		});
		var decorator = new EncryptingProjectionStoreDecorator<TestProjection>(innerStore, _registry, options);

		// Act
		await decorator.DeleteAsync("p1", _ct);

		// Assert
		A.CallTo(() => innerStore.DeleteAsync("p1", _ct))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region QueryAsync - With Encryption

	[Fact]
	public async Task QueryAsync_ShouldDecryptProjections_WhenModeIsEncryptAndDecrypt()
	{
		// Arrange
		var innerStore = A.Fake<IProjectionStore<TestProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "test",
			DefaultTenantId = "tenant-1"
		});
		var decorator = new EncryptingProjectionStoreDecorator<TestProjection>(innerStore, _registry, options);

		var plainData = new byte[] { 10, 20, 30 }; // Not encrypted (no magic bytes)
		var projections = new List<TestProjection>
		{
			new() { Id = "p1", Name = "Test1", SensitiveData = plainData }
		};

		A.CallTo(() => innerStore.QueryAsync(A<IDictionary<string, object>?>._, A<QueryOptions?>._, _ct))
			.Returns(Task.FromResult<IReadOnlyList<TestProjection>>(projections));

		// Act
		var result = await decorator.QueryAsync(null, null, _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].SensitiveData.ShouldBe(plainData);
	}

	[Fact]
	public async Task QueryAsync_ShouldDecryptEncryptedProjections()
	{
		// Arrange
		var innerStore = A.Fake<IProjectionStore<TestProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "test",
			DefaultTenantId = "tenant-1"
		});
		var decorator = new EncryptingProjectionStoreDecorator<TestProjection>(innerStore, _registry, options);

		var encryptedBytes = CreateEncryptedFieldBytes(new byte[] { 99 });
		var decryptedData = new byte[] { 10, 20, 30 };
		var projections = new List<TestProjection>
		{
			new() { Id = "p1", Name = "Test1", SensitiveData = encryptedBytes }
		};

		A.CallTo(() => innerStore.QueryAsync(A<IDictionary<string, object>?>._, A<QueryOptions?>._, _ct))
			.Returns(Task.FromResult<IReadOnlyList<TestProjection>>(projections));
		SetupDecryption(decryptedData);

		// Act
		var result = await decorator.QueryAsync(null, null, _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].SensitiveData.ShouldBe(decryptedData);
	}

	#endregion

	#region CountAsync Tests

	[Fact]
	public async Task CountAsync_ShouldDelegateToInner_WithFilters()
	{
		// Arrange
		var innerStore = A.Fake<IProjectionStore<TestProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "test"
		});
		var decorator = new EncryptingProjectionStoreDecorator<TestProjection>(innerStore, _registry, options);
		var filters = new Dictionary<string, object> { { "Name", "Test" } };

		A.CallTo(() => innerStore.CountAsync(A<IDictionary<string, object>?>._, _ct))
			.Returns(Task.FromResult(5L));

		// Act
		var result = await decorator.CountAsync(filters, _ct);

		// Assert
		result.ShouldBe(5L);
		A.CallTo(() => innerStore.CountAsync(A<IDictionary<string, object>>.That.IsNotNull(), _ct))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Plain Projection - No Encrypted Properties

	[Fact]
	public async Task UpsertAsync_ShouldNotEncrypt_WhenProjectionHasNoEncryptedFields()
	{
		// Arrange
		var innerStore = A.Fake<IProjectionStore<PlainProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "test"
		});
		var decorator = new EncryptingProjectionStoreDecorator<PlainProjection>(innerStore, _registry, options);

		var projection = new PlainProjection { Id = "p1", Name = "Test", Data = new byte[] { 1, 2, 3 } };

		// Act
		await decorator.UpsertAsync("p1", projection, _ct);

		// Assert
		A.CallTo(() => _registry.GetPrimary()).MustNotHaveHappened();
		A.CallTo(() => innerStore.UpsertAsync("p1", projection, _ct)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task QueryAsync_ShouldNotDecrypt_WhenProjectionHasNoEncryptedFields()
	{
		// Arrange
		var innerStore = A.Fake<IProjectionStore<PlainProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "test"
		});
		var decorator = new EncryptingProjectionStoreDecorator<PlainProjection>(innerStore, _registry, options);

		var projections = new List<PlainProjection>
		{
			new() { Id = "p1", Name = "Test", Data = new byte[] { 1, 2, 3 } }
		};

		A.CallTo(() => innerStore.QueryAsync(A<IDictionary<string, object>?>._, A<QueryOptions?>._, _ct))
			.Returns(Task.FromResult<IReadOnlyList<PlainProjection>>(projections));

		// Act
		var result = await decorator.QueryAsync(null, null, _ct);

		// Assert
		result.Count.ShouldBe(1);
		result[0].Data.ShouldBe(new byte[] { 1, 2, 3 });
	}

	#endregion

	#region FIPS Compliance Option

	[Fact]
	public void Constructor_ShouldHandleFipsComplianceOption()
	{
		// Arrange
		var innerStore = A.Fake<IProjectionStore<TestProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "fips-test",
			DefaultTenantId = "tenant-fips",
			RequireFipsCompliance = true
		});

		// Act - should not throw
		var decorator = new EncryptingProjectionStoreDecorator<TestProjection>(innerStore, _registry, options);

		// Assert
		decorator.ShouldNotBeNull();
	}

	#endregion
}
