// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Security;

using FakeItEasy;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.Encryption;

/// <summary>
/// Unit tests for <see cref="DataProtectionMessageEncryptionService"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Encryption")]
public sealed class DataProtectionMessageEncryptionServiceShould : IDisposable
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IDataProtector _protector;
    private readonly ILogger<DataProtectionMessageEncryptionService> _logger;
    private readonly DataProtectionMessageEncryptionService _sut;

    public DataProtectionMessageEncryptionServiceShould()
    {
        _dataProtectionProvider = A.Fake<IDataProtectionProvider>();
        _protector = A.Fake<IDataProtector>();
        _logger = new NullLogger<DataProtectionMessageEncryptionService>();

        A.CallTo(() => _dataProtectionProvider.CreateProtector(A<string>._))
            .Returns(_protector);

        // Setup protector to return predictable data
        A.CallTo(() => _protector.Protect(A<byte[]>._))
            .ReturnsLazily((byte[] input) =>
            {
                // Simple "encryption": reverse the bytes (for testing)
                var result = new byte[input.Length];
                Array.Copy(input, result, input.Length);
                Array.Reverse(result);
                return result;
            });

        A.CallTo(() => _protector.Unprotect(A<byte[]>._))
            .ReturnsLazily((byte[] input) =>
            {
                // Simple "decryption": reverse back
                var result = new byte[input.Length];
                Array.Copy(input, result, input.Length);
                Array.Reverse(result);
                return result;
            });

        _sut = new DataProtectionMessageEncryptionService(
            _dataProtectionProvider,
            Microsoft.Extensions.Options.Options.Create(new EncryptionOptions { IncludeMetadataHeader = false }),
            _logger);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void ImplementIMessageEncryptionService()
    {
        _sut.ShouldBeAssignableTo<IMessageEncryptionService>();
    }

    [Fact]
    public void ImplementIDisposable()
    {
        _sut.ShouldBeAssignableTo<IDisposable>();
    }

    [Fact]
    public void BePublicAndSealed()
    {
        typeof(DataProtectionMessageEncryptionService).IsPublic.ShouldBeTrue();
        typeof(DataProtectionMessageEncryptionService).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void ThrowWhenProviderIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DataProtectionMessageEncryptionService(null!, Microsoft.Extensions.Options.Options.Create(new EncryptionOptions()), _logger));
    }

    [Fact]
    public void ThrowWhenOptionsIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DataProtectionMessageEncryptionService(_dataProtectionProvider, null!, _logger));
    }

    [Fact]
    public void ThrowWhenLoggerIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DataProtectionMessageEncryptionService(_dataProtectionProvider, Microsoft.Extensions.Options.Options.Create(new EncryptionOptions()), null!));
    }

    [Fact]
    public async Task EncryptStringContent()
    {
        // Arrange
        var context = new EncryptionContext { TenantId = "tenant-1" };

        // Act
        var encrypted = await _sut.EncryptMessageAsync("test data", context, CancellationToken.None);

        // Assert
        encrypted.ShouldNotBeNullOrWhiteSpace();
        encrypted.ShouldNotBe("test data");
    }

    [Fact]
    public async Task EncryptByteContent()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("test data");
        var context = new EncryptionContext { TenantId = "tenant-1" };

        // Act
        var encrypted = await _sut.EncryptMessageAsync(content, context, CancellationToken.None);

        // Assert
        encrypted.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DecryptStringContent()
    {
        // Arrange
        var context = new EncryptionContext { TenantId = "tenant-1" };
        var encrypted = await _sut.EncryptMessageAsync("hello world", context, CancellationToken.None);

        // Act
        var decrypted = await _sut.DecryptMessageAsync(encrypted, context, CancellationToken.None);

        // Assert
        decrypted.ShouldBe("hello world");
    }

    [Fact]
    public async Task DecryptByteContent()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("byte test");
        var context = new EncryptionContext { TenantId = "tenant-1" };
        var encrypted = await _sut.EncryptMessageAsync(content, context, CancellationToken.None);

        // Act
        var decrypted = await _sut.DecryptMessageAsync(encrypted, context, CancellationToken.None);

        // Assert
        Encoding.UTF8.GetString(decrypted).ShouldBe("byte test");
    }

    [Fact]
    public async Task ThrowWhenEncryptingNullString()
    {
        var context = new EncryptionContext();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.EncryptMessageAsync((string)null!, context, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenEncryptingNullBytes()
    {
        var context = new EncryptionContext();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.EncryptMessageAsync((byte[])null!, context, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenDecryptingNullString()
    {
        var context = new EncryptionContext();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.DecryptMessageAsync((string)null!, context, CancellationToken.None));
    }

    [Fact]
    public async Task RotateKeys()
    {
        // Act
        var result = await _sut.RotateKeysAsync(CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.NewKey.ShouldNotBeNull();
        result.PreviousKey.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidateConfiguration()
    {
        // Act
        var isValid = await _sut.ValidateConfigurationAsync(CancellationToken.None);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void DisposeWithoutException()
    {
        using var sut = new DataProtectionMessageEncryptionService(
            _dataProtectionProvider,
            Microsoft.Extensions.Options.Options.Create(new EncryptionOptions()),
            _logger);

        Should.NotThrow(() => sut.Dispose());
    }

    [Fact]
    public void DisposeMultipleTimesWithoutException()
    {
        var sut = new DataProtectionMessageEncryptionService(
            _dataProtectionProvider,
            Microsoft.Extensions.Options.Options.Create(new EncryptionOptions()),
            _logger);

        Should.NotThrow(() =>
        {
            sut.Dispose();
            sut.Dispose();
        });
    }
}
