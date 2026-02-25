// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security;

using Excalibur.Dispatch.Security;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Security.Configuration;

/// <summary>
/// Unit tests for <see cref="SecureCredentialProvider"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Configuration")]
public sealed class SecureCredentialProviderShould : IDisposable
{
    private readonly ILogger<SecureCredentialProvider> _logger;
    private readonly ICredentialStore _store;
    private readonly SecureCredentialProvider _sut;

    public SecureCredentialProviderShould()
    {
        _logger = new NullLogger<SecureCredentialProvider>();
        _store = A.Fake<ICredentialStore>();

        _sut = new SecureCredentialProvider(_logger, [_store]);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void ImplementISecureCredentialProvider()
    {
        _sut.ShouldBeAssignableTo<ISecureCredentialProvider>();
    }

    [Fact]
    public void ImplementIDisposable()
    {
        _sut.ShouldBeAssignableTo<IDisposable>();
    }

    [Fact]
    public void ThrowWhenLoggerIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SecureCredentialProvider(null!, [_store]));
    }

    [Fact]
    public void ThrowWhenCredentialStoresIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SecureCredentialProvider(_logger, null!));
    }

    [Fact]
    public async Task RetrieveCredentialFromStore()
    {
        // Arrange
        var secureString = new SecureString();
        secureString.AppendChar('s');
        secureString.AppendChar('e');
        secureString.AppendChar('c');
        secureString.MakeReadOnly();

        A.CallTo(() => _store.GetCredentialAsync("test-key", A<CancellationToken>._))
            .Returns(Task.FromResult<SecureString?>(secureString));

        // Act
        var result = await _sut.GetCredentialAsync("test-key", CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReturnNullWhenCredentialNotFound()
    {
        // Arrange
        A.CallTo(() => _store.GetCredentialAsync("missing-key", A<CancellationToken>._))
            .Returns(Task.FromResult<SecureString?>(null));

        // Act
        var result = await _sut.GetCredentialAsync("missing-key", CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ThrowWhenKeyIsNullOrWhitespace()
    {
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _sut.GetCredentialAsync(null!, CancellationToken.None));

        await Should.ThrowAsync<ArgumentException>(async () =>
            await _sut.GetCredentialAsync("", CancellationToken.None));

        await Should.ThrowAsync<ArgumentException>(async () =>
            await _sut.GetCredentialAsync("  ", CancellationToken.None));
    }

    [Fact]
    public async Task CacheCredentialsAfterRetrieval()
    {
        // Arrange
        var secureString = new SecureString();
        secureString.AppendChar('x');
        secureString.MakeReadOnly();

        A.CallTo(() => _store.GetCredentialAsync("cached-key", A<CancellationToken>._))
            .Returns(Task.FromResult<SecureString?>(secureString));

        // Act - retrieve twice
        var result1 = await _sut.GetCredentialAsync("cached-key", CancellationToken.None);
        var result2 = await _sut.GetCredentialAsync("cached-key", CancellationToken.None);

        // Assert - store should only be called once (second call from cache)
        result1.ShouldNotBeNull();
        result2.ShouldNotBeNull();
        A.CallTo(() => _store.GetCredentialAsync("cached-key", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ValidateCredentialReturnsSuccessForValidCredential()
    {
        // Arrange
        var secureString = CreateSecureString("StrongP@ssw0rd!XY");
        A.CallTo(() => _store.GetCredentialAsync("valid-key", A<CancellationToken>._))
            .Returns(Task.FromResult<SecureString?>(secureString));

        var requirements = new CredentialRequirements
        {
            MinimumLength = 8,
            RequireUppercase = true,
            RequireLowercase = true,
            RequireDigit = true,
            RequireSpecialCharacter = true,
        };

        // Act
        var result = await _sut.ValidateCredentialAsync("valid-key", requirements, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateCredentialReturnsFailureWhenNotFound()
    {
        // Arrange
        A.CallTo(() => _store.GetCredentialAsync("not-found", A<CancellationToken>._))
            .Returns(Task.FromResult<SecureString?>(null));

        var requirements = new CredentialRequirements { MinimumLength = 1 };

        // Act
        var result = await _sut.ValidateCredentialAsync("not-found", requirements, CancellationToken.None);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateCredentialReturnsFailureForShortCredential()
    {
        // Arrange
        var secureString = CreateSecureString("abc");
        A.CallTo(() => _store.GetCredentialAsync("short-key", A<CancellationToken>._))
            .Returns(Task.FromResult<SecureString?>(secureString));

        var requirements = new CredentialRequirements { MinimumLength = 10 };

        // Act
        var result = await _sut.ValidateCredentialAsync("short-key", requirements, CancellationToken.None);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task RotateCredentialThrowsWhenNoWritableStores()
    {
        // Arrange - _store is not IWritableCredentialStore

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _sut.RotateCredentialAsync("some-key", CancellationToken.None));
    }

    [Fact]
    public async Task RotateCredentialUpdatesWritableStores()
    {
        // Arrange
        var writableStore = A.Fake<IWritableCredentialStore>();
        using var sut = new SecureCredentialProvider(_logger, new ICredentialStore[] { writableStore });

        // IWritableCredentialStore also implements ICredentialStore indirectly
        A.CallTo(() => writableStore.StoreCredentialAsync(A<string>._, A<SecureString>._, A<CancellationToken>._))
            .Returns(Task.CompletedTask);

        // Act
        await sut.RotateCredentialAsync("rotate-key", CancellationToken.None);

        // Assert
        A.CallTo(() => writableStore.StoreCredentialAsync("rotate-key", A<SecureString>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleStoreExceptionGracefully()
    {
        // Arrange
        A.CallTo(() => _store.GetCredentialAsync("error-key", A<CancellationToken>._))
            .Throws(new InvalidOperationException("Store error"));

        // Act
        var result = await _sut.GetCredentialAsync("error-key", CancellationToken.None);

        // Assert - should return null, not throw
        result.ShouldBeNull();
    }

    [Fact]
    public void DisposeWithoutException()
    {
        using var sut = new SecureCredentialProvider(_logger, [_store]);
        Should.NotThrow(() => sut.Dispose());
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
