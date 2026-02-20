// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Encryption.Decorators;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptingProjectionStoreDecorator{TProjection}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingProjectionStoreDecoratorShould
{
	private readonly IProjectionStore<TestProjection> _innerStore;
	private readonly IEncryptionProviderRegistry _registry;
	private readonly IEncryptionProvider _provider;
	private readonly CancellationToken _ct = CancellationToken.None;

	public EncryptingProjectionStoreDecoratorShould()
	{
		_innerStore = A.Fake<IProjectionStore<TestProjection>>();
		_registry = A.Fake<IEncryptionProviderRegistry>();
		_provider = A.Fake<IEncryptionProvider>();
	}

	private EncryptingProjectionStoreDecorator<TestProjection> CreateDecorator(
		EncryptionMode mode = EncryptionMode.EncryptAndDecrypt)
	{
		var options = Options.Create(new EncryptionOptions
		{
			Mode = mode,
			DefaultPurpose = "test",
			DefaultTenantId = "tenant-1"
		});
		return new EncryptingProjectionStoreDecorator<TestProjection>(_innerStore, _registry, options);
	}

	private EncryptingProjectionStoreDecorator<PlainProjection> CreatePlainDecorator(
		EncryptionMode mode = EncryptionMode.EncryptAndDecrypt)
	{
		var innerPlain = A.Fake<IProjectionStore<PlainProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = mode,
			DefaultPurpose = "test",
			DefaultTenantId = "tenant-1"
		});
		return new EncryptingProjectionStoreDecorator<PlainProjection>(innerPlain, _registry, options);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenInnerStoreIsNull()
	{
		// Arrange
		var options = Options.Create(new EncryptionOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new EncryptingProjectionStoreDecorator<TestProjection>(null!, _registry, options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenRegistryIsNull()
	{
		// Arrange
		var options = Options.Create(new EncryptionOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new EncryptingProjectionStoreDecorator<TestProjection>(_innerStore, null!, options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new EncryptingProjectionStoreDecorator<TestProjection>(_innerStore, _registry, null!));
	}

	#endregion

	#region GetByIdAsync Tests

	[Fact]
	public async Task GetByIdAsync_ShouldReturnNull_WhenInnerReturnsNull()
	{
		// Arrange
		var decorator = CreateDecorator();
		A.CallTo(() => _innerStore.GetByIdAsync("proj-1", _ct))
			.Returns(Task.FromResult<TestProjection?>(null));

		// Act
		var result = await decorator.GetByIdAsync("proj-1", _ct);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnProjectionUnchanged_WhenModeIsDisabled()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.Disabled);
		var projection = new TestProjection { Id = "proj-1", Name = "Test", SensitiveData = new byte[] { 1, 2, 3 } };
		A.CallTo(() => _innerStore.GetByIdAsync("proj-1", _ct))
			.Returns(Task.FromResult<TestProjection?>(projection));

		// Act
		var result = await decorator.GetByIdAsync("proj-1", _ct);

		// Assert
		result.ShouldNotBeNull();
		result.SensitiveData.ShouldBe(new byte[] { 1, 2, 3 });
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnProjectionUnchanged_WhenFieldIsPlaintext()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var plainData = new byte[] { 10, 20, 30 }; // Not magic bytes
		var projection = new TestProjection { Id = "proj-1", Name = "Test", SensitiveData = plainData };
		A.CallTo(() => _innerStore.GetByIdAsync("proj-1", _ct))
			.Returns(Task.FromResult<TestProjection?>(projection));

		// Act
		var result = await decorator.GetByIdAsync("proj-1", _ct);

		// Assert
		result.ShouldNotBeNull();
		result.SensitiveData.ShouldBe(plainData);
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnProjectionUnchanged_WhenFieldIsNull()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var projection = new TestProjection { Id = "proj-1", Name = "Test", SensitiveData = null };
		A.CallTo(() => _innerStore.GetByIdAsync("proj-1", _ct))
			.Returns(Task.FromResult<TestProjection?>(projection));

		// Act
		var result = await decorator.GetByIdAsync("proj-1", _ct);

		// Assert
		result.ShouldNotBeNull();
		result.SensitiveData.ShouldBeNull();
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnProjectionUnchanged_WhenFieldIsEmpty()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var projection = new TestProjection { Id = "proj-1", Name = "Test", SensitiveData = Array.Empty<byte>() };
		A.CallTo(() => _innerStore.GetByIdAsync("proj-1", _ct))
			.Returns(Task.FromResult<TestProjection?>(projection));

		// Act
		var result = await decorator.GetByIdAsync("proj-1", _ct);

		// Assert
		result.ShouldNotBeNull();
		result.SensitiveData.ShouldBeEmpty();
	}

	#endregion

	#region UpsertAsync Tests

	[Fact]
	public async Task UpsertAsync_ShouldThrowInvalidOperation_WhenModeIsDecryptOnlyReadOnly()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.DecryptOnlyReadOnly);
		var projection = new TestProjection { Id = "proj-1", Name = "Test" };

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await decorator.UpsertAsync("proj-1", projection, _ct));
	}

	[Fact]
	public async Task UpsertAsync_ShouldThrowArgumentNull_WhenProjectionIsNull()
	{
		// Arrange
		var decorator = CreateDecorator();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await decorator.UpsertAsync("proj-1", null!, _ct));
	}

	[Fact]
	public async Task UpsertAsync_ShouldDelegateToInner_WhenModeIsDisabled()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.Disabled);
		var projection = new TestProjection { Id = "proj-1", Name = "Test", SensitiveData = new byte[] { 1, 2, 3 } };

		// Act
		await decorator.UpsertAsync("proj-1", projection, _ct);

		// Assert
		A.CallTo(() => _innerStore.UpsertAsync("proj-1", projection, _ct))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UpsertAsync_ShouldDelegateToInner_WhenModeIsDecryptOnlyWritePlaintext()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.DecryptOnlyWritePlaintext);
		var projection = new TestProjection { Id = "proj-1", Name = "Test", SensitiveData = new byte[] { 1, 2, 3 } };

		// Act
		await decorator.UpsertAsync("proj-1", projection, _ct);

		// Assert
		A.CallTo(() => _innerStore.UpsertAsync("proj-1", projection, _ct))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UpsertAsync_ShouldEncryptFields_WhenModeIsEncryptAndDecrypt()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var plainData = new byte[] { 10, 20, 30 };
		var projection = new TestProjection { Id = "proj-1", Name = "Test", SensitiveData = plainData };

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
		await decorator.UpsertAsync("proj-1", projection, _ct);

		// Assert
		A.CallTo(() => _innerStore.UpsertAsync("proj-1", projection, _ct))
			.MustHaveHappenedOnceExactly();
		// The SensitiveData should have been modified to encrypted form (starts with magic bytes)
		projection.SensitiveData.ShouldNotBeNull();
		projection.SensitiveData.Length.ShouldBeGreaterThan(0);
		// Check magic bytes prefix: 0x45, 0x58, 0x43, 0x52
		projection.SensitiveData[0].ShouldBe((byte)0x45);
		projection.SensitiveData[1].ShouldBe((byte)0x58);
		projection.SensitiveData[2].ShouldBe((byte)0x43);
		projection.SensitiveData[3].ShouldBe((byte)0x52);
	}

	[Fact]
	public async Task UpsertAsync_ShouldNotDoubleEncrypt_WhenDataAlreadyEncrypted()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		// Data that starts with magic bytes EXCR (0x45, 0x58, 0x43, 0x52) is considered encrypted
		var alreadyEncrypted = new byte[] { 0x45, 0x58, 0x43, 0x52, 1, 2, 3, 4, 5 };
		var projection = new TestProjection { Id = "proj-1", Name = "Test", SensitiveData = alreadyEncrypted };

		// Act
		await decorator.UpsertAsync("proj-1", projection, _ct);

		// Assert - should NOT call encrypt since data is already encrypted
		A.CallTo(() => _registry.GetPrimary()).MustNotHaveHappened();
		A.CallTo(() => _innerStore.UpsertAsync("proj-1", projection, _ct))
			.MustHaveHappenedOnceExactly();
		// Data should be unchanged
		projection.SensitiveData.ShouldBe(alreadyEncrypted);
	}

	[Fact]
	public async Task UpsertAsync_ShouldSkipNullFields()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var projection = new TestProjection { Id = "proj-1", Name = "Test", SensitiveData = null };

		// Act
		await decorator.UpsertAsync("proj-1", projection, _ct);

		// Assert
		A.CallTo(() => _registry.GetPrimary()).MustNotHaveHappened();
		A.CallTo(() => _innerStore.UpsertAsync("proj-1", projection, _ct))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UpsertAsync_ShouldSkipEmptyFields()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);
		var projection = new TestProjection { Id = "proj-1", Name = "Test", SensitiveData = Array.Empty<byte>() };

		// Act
		await decorator.UpsertAsync("proj-1", projection, _ct);

		// Assert
		A.CallTo(() => _registry.GetPrimary()).MustNotHaveHappened();
	}

	#endregion

	#region DeleteAsync Tests

	[Fact]
	public async Task DeleteAsync_ShouldThrowInvalidOperation_WhenModeIsDecryptOnlyReadOnly()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.DecryptOnlyReadOnly);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			decorator.DeleteAsync("proj-1", _ct));
	}

	[Fact]
	public async Task DeleteAsync_ShouldDelegateToInner_WhenModeAllowsWrites()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.EncryptAndDecrypt);

		// Act
		await decorator.DeleteAsync("proj-1", _ct);

		// Assert
		A.CallTo(() => _innerStore.DeleteAsync("proj-1", _ct))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteAsync_ShouldDelegateToInner_WhenModeIsDisabled()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.Disabled);

		// Act
		await decorator.DeleteAsync("proj-1", _ct);

		// Assert
		A.CallTo(() => _innerStore.DeleteAsync("proj-1", _ct))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region QueryAsync Tests

	[Fact]
	public async Task QueryAsync_ShouldReturnDecryptedProjections_WhenModeIsDisabled()
	{
		// Arrange
		var decorator = CreateDecorator(EncryptionMode.Disabled);
		var projections = new List<TestProjection>
		{
			new() { Id = "proj-1", Name = "Test1", SensitiveData = new byte[] { 1, 2, 3 } },
			new() { Id = "proj-2", Name = "Test2", SensitiveData = new byte[] { 4, 5, 6 } }
		};
		A.CallTo(() => _innerStore.QueryAsync(A<IDictionary<string, object>?>._, A<QueryOptions?>._, _ct))
			.Returns(Task.FromResult<IReadOnlyList<TestProjection>>(projections));

		// Act
		var result = await decorator.QueryAsync(null, null, _ct);

		// Assert
		result.Count.ShouldBe(2);
		result[0].SensitiveData.ShouldBe(new byte[] { 1, 2, 3 });
		result[1].SensitiveData.ShouldBe(new byte[] { 4, 5, 6 });
	}

	[Fact]
	public async Task QueryAsync_ShouldReturnEmptyList_WhenInnerReturnsEmpty()
	{
		// Arrange
		var decorator = CreateDecorator();
		A.CallTo(() => _innerStore.QueryAsync(A<IDictionary<string, object>?>._, A<QueryOptions?>._, _ct))
			.Returns(Task.FromResult<IReadOnlyList<TestProjection>>(new List<TestProjection>()));

		// Act
		var result = await decorator.QueryAsync(null, null, _ct);

		// Assert
		result.Count.ShouldBe(0);
	}

	#endregion

	#region CountAsync Tests

	[Fact]
	public async Task CountAsync_ShouldDelegateToInner()
	{
		// Arrange
		var decorator = CreateDecorator();
		A.CallTo(() => _innerStore.CountAsync(A<IDictionary<string, object>?>._, _ct))
			.Returns(Task.FromResult(42L));

		// Act
		var result = await decorator.CountAsync(null, _ct);

		// Assert
		result.ShouldBe(42L);
	}

	#endregion

	#region Projection Without Encrypted Fields Tests

	[Fact]
	public async Task GetByIdAsync_ShouldReturnUnchanged_WhenProjectionHasNoEncryptedFields()
	{
		// Arrange
		var innerPlain = A.Fake<IProjectionStore<PlainProjection>>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "test"
		});
		var decorator = new EncryptingProjectionStoreDecorator<PlainProjection>(innerPlain, _registry, options);
		var projection = new PlainProjection { Id = "p1", Name = "Test" };
		A.CallTo(() => innerPlain.GetByIdAsync("p1", _ct))
			.Returns(Task.FromResult<PlainProjection?>(projection));

		// Act
		var result = await decorator.GetByIdAsync("p1", _ct);

		// Assert
		result.ShouldNotBeNull();
		result.Name.ShouldBe("Test");
	}

	#endregion

	#region Test Types

#pragma warning disable CA1034 // Nested types should not be visible - required for FakeItEasy proxy generation

	/// <summary>
	/// Test projection with an encrypted field.
	/// </summary>
	public sealed class TestProjection
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;

		[EncryptedField]
		public byte[]? SensitiveData { get; set; }
	}

	/// <summary>
	/// Test projection without any encrypted fields.
	/// </summary>
	public sealed class PlainProjection
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public byte[]? Data { get; set; } // No [EncryptedField] attribute
	}

#pragma warning restore CA1034

	#endregion
}
