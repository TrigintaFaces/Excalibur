// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security;

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Configuration;

/// <summary>
/// Depth tests for <see cref="SecureCredentialProvider"/>.
/// Covers multi-store fallback, validation of credential complexity requirements,
/// test pattern detection, prohibited values, rotate with store failures, and cache invalidation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SecureCredentialProviderDepthShould : IDisposable
{
	private readonly ILogger<SecureCredentialProvider> _logger;

	public SecureCredentialProviderDepthShould()
	{
		_logger = NullLogger<SecureCredentialProvider>.Instance;
	}

	[Fact]
	public async Task FallbackToSecondStoreWhenFirstReturnsNull()
	{
		// Arrange
		var store1 = A.Fake<ICredentialStore>();
		var store2 = A.Fake<ICredentialStore>();
		A.CallTo(() => store1.GetCredentialAsync("key", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(null));
		A.CallTo(() => store2.GetCredentialAsync("key", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(CreateSecureString("found")));

		using var sut = new SecureCredentialProvider(_logger, [store1, store2]);

		// Act
		var result = await sut.GetCredentialAsync("key", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		A.CallTo(() => store1.GetCredentialAsync("key", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => store2.GetCredentialAsync("key", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FallbackToSecondStoreWhenFirstThrows()
	{
		// Arrange
		var store1 = A.Fake<ICredentialStore>();
		var store2 = A.Fake<ICredentialStore>();
		A.CallTo(() => store1.GetCredentialAsync("key", A<CancellationToken>._))
			.Throws(new InvalidOperationException("Store 1 down"));
		A.CallTo(() => store2.GetCredentialAsync("key", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(CreateSecureString("ok")));

		using var sut = new SecureCredentialProvider(_logger, [store1, store2]);

		// Act
		var result = await sut.GetCredentialAsync("key", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task ReturnNullWhenAllStoresReturnNull()
	{
		// Arrange
		var store1 = A.Fake<ICredentialStore>();
		var store2 = A.Fake<ICredentialStore>();
		A.CallTo(() => store1.GetCredentialAsync("missing", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(null));
		A.CallTo(() => store2.GetCredentialAsync("missing", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(null));

		using var sut = new SecureCredentialProvider(_logger, [store1, store2]);

		// Act
		var result = await sut.GetCredentialAsync("missing", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task ValidateCredentialFailsWhenMissingUppercase()
	{
		// Arrange
		var store = A.Fake<ICredentialStore>();
		A.CallTo(() => store.GetCredentialAsync("key", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(CreateSecureString("lowercase1!")));

		using var sut = new SecureCredentialProvider(_logger, [store]);
		var requirements = new CredentialRequirements { RequireUppercase = true };

		// Act
		var result = await sut.ValidateCredentialAsync("key", requirements, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ValidateCredentialFailsWhenMissingLowercase()
	{
		// Arrange
		var store = A.Fake<ICredentialStore>();
		A.CallTo(() => store.GetCredentialAsync("key", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(CreateSecureString("UPPERCASE1!")));

		using var sut = new SecureCredentialProvider(_logger, [store]);
		var requirements = new CredentialRequirements { RequireLowercase = true };

		// Act
		var result = await sut.ValidateCredentialAsync("key", requirements, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ValidateCredentialFailsWhenMissingDigit()
	{
		// Arrange
		var store = A.Fake<ICredentialStore>();
		A.CallTo(() => store.GetCredentialAsync("key", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(CreateSecureString("NoDigits!")));

		using var sut = new SecureCredentialProvider(_logger, [store]);
		var requirements = new CredentialRequirements { RequireDigit = true };

		// Act
		var result = await sut.ValidateCredentialAsync("key", requirements, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ValidateCredentialFailsWhenMissingSpecialCharacter()
	{
		// Arrange
		var store = A.Fake<ICredentialStore>();
		A.CallTo(() => store.GetCredentialAsync("key", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(CreateSecureString("NoSpecial1")));

		using var sut = new SecureCredentialProvider(_logger, [store]);
		var requirements = new CredentialRequirements { RequireSpecialCharacter = true };

		// Act
		var result = await sut.ValidateCredentialAsync("key", requirements, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ValidateCredentialFailsForProhibitedValues()
	{
		// Arrange
		var store = A.Fake<ICredentialStore>();
		A.CallTo(() => store.GetCredentialAsync("key", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(CreateSecureString("MyDefaultPwd")));

		using var sut = new SecureCredentialProvider(_logger, [store]);
		var requirements = new CredentialRequirements
		{
			ProhibitedValues = new HashSet<string> { "MyDefaultPwd", "admin123" },
		};

		// Act
		var result = await sut.ValidateCredentialAsync("key", requirements, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ValidateCredentialFailsForTestPatternCredentials()
	{
		// Arrange - credential containing test patterns like "TEST", "DEMO", "PASSWORD", etc.
		var store = A.Fake<ICredentialStore>();
		A.CallTo(() => store.GetCredentialAsync("key", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(CreateSecureString("myTestCredential")));

		using var sut = new SecureCredentialProvider(_logger, [store]);
		var requirements = new CredentialRequirements { MinimumLength = 1 };

		// Act
		var result = await sut.ValidateCredentialAsync("key", requirements, CancellationToken.None);

		// Assert - contains "TEST" pattern, should fail
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ValidateCredentialThrowsWhenKeyIsNullOrEmpty()
	{
		// Arrange
		var store = A.Fake<ICredentialStore>();
		using var sut = new SecureCredentialProvider(_logger, [store]);
		var requirements = new CredentialRequirements();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await sut.ValidateCredentialAsync(null!, requirements, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(async () =>
			await sut.ValidateCredentialAsync("", requirements, CancellationToken.None));
	}

	[Fact]
	public async Task ValidateCredentialThrowsWhenRequirementsIsNull()
	{
		// Arrange
		var store = A.Fake<ICredentialStore>();
		using var sut = new SecureCredentialProvider(_logger, [store]);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.ValidateCredentialAsync("key", null!, CancellationToken.None));
	}

	[Fact]
	public async Task RotateCredentialThrowsWhenKeyIsNullOrWhitespace()
	{
		// Arrange
		var store = A.Fake<ICredentialStore>();
		using var sut = new SecureCredentialProvider(_logger, [store]);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await sut.RotateCredentialAsync(null!, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(async () =>
			await sut.RotateCredentialAsync("", CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(async () =>
			await sut.RotateCredentialAsync("   ", CancellationToken.None));
	}

	[Fact]
	public async Task RotateCredentialContinuesWhenOneWritableStoreThrows()
	{
		// Arrange
		var failingStore = A.Fake<IWritableCredentialStore>();
		var workingStore = A.Fake<IWritableCredentialStore>();
		A.CallTo(() => failingStore.StoreCredentialAsync(A<string>._, A<SecureString>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Store failure"));
		A.CallTo(() => workingStore.StoreCredentialAsync(A<string>._, A<SecureString>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		using var sut = new SecureCredentialProvider(_logger, new ICredentialStore[] { failingStore, workingStore });

		// Act - should not throw because at least one store succeeded
		await sut.RotateCredentialAsync("key", CancellationToken.None);

		// Assert
		A.CallTo(() => workingStore.StoreCredentialAsync("key", A<SecureString>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RotateCredentialInvalidatesCacheAfterRotation()
	{
		// Arrange - set up a writable store that also serves credentials
		var writableStore = A.Fake<IWritableCredentialStore>();
		var firstCredential = CreateSecureString("first");
		var secondCredential = CreateSecureString("second");

		// First call returns firstCredential, subsequent calls return secondCredential
		A.CallTo(() => writableStore.GetCredentialAsync("key", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(firstCredential)).Once()
			.Then
			.Returns(Task.FromResult<SecureString?>(secondCredential));

		A.CallTo(() => writableStore.StoreCredentialAsync(A<string>._, A<SecureString>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		using var sut = new SecureCredentialProvider(_logger, new ICredentialStore[] { writableStore });

		// Act - retrieve to populate cache
		var result1 = await sut.GetCredentialAsync("key", CancellationToken.None);

		// Rotate invalidates cache
		await sut.RotateCredentialAsync("key", CancellationToken.None);

		// Get again - should NOT use cache, should call store again
		var result2 = await sut.GetCredentialAsync("key", CancellationToken.None);

		// Assert - store should have been called twice (cache was invalidated)
		A.CallTo(() => writableStore.GetCredentialAsync("key", A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
	}

	[Fact]
	public async Task ValidateCredentialWithAllRequirementsMet()
	{
		// Arrange - credential that meets all requirements
		var store = A.Fake<ICredentialStore>();
		A.CallTo(() => store.GetCredentialAsync("key", A<CancellationToken>._))
			.Returns(Task.FromResult<SecureString?>(CreateSecureString("Str0ng#Cred!")));

		using var sut = new SecureCredentialProvider(_logger, [store]);
		var requirements = new CredentialRequirements
		{
			MinimumLength = 8,
			RequireUppercase = true,
			RequireLowercase = true,
			RequireDigit = true,
			RequireSpecialCharacter = true,
		};

		// Act
		var result = await sut.ValidateCredentialAsync("key", requirements, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void CreateWithEmptyStoresCollection()
	{
		// Act - should not throw with empty stores
		using var sut = new SecureCredentialProvider(_logger, Array.Empty<ICredentialStore>());
		sut.ShouldNotBeNull();
	}

	public void Dispose()
	{
		// No-op: individual tests manage their own SUT disposal
	}

	private static SecureString CreateSecureString(string value)
	{
		var ss = new SecureString();
		foreach (var c in value)
		{
			ss.AppendChar(c);
		}

		ss.MakeReadOnly();
		return ss;
	}
}
