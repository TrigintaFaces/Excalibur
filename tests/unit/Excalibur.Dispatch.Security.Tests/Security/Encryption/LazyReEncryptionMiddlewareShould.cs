// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Compliance;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Encryption;

/// <summary>
/// Unit tests for <see cref="LazyReEncryptionMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LazyReEncryptionMiddlewareShould
{
	private readonly IEncryptionMigrationService _migrationService;
	private readonly IEncryptionProvider _encryptionProvider;
	private readonly LazyReEncryptionOptions _options;
	private readonly LazyReEncryptionMiddleware _sut;

	public LazyReEncryptionMiddlewareShould()
	{
		_migrationService = A.Fake<IEncryptionMigrationService>();
		_encryptionProvider = A.Fake<IEncryptionProvider>();
		_options = new LazyReEncryptionOptions();

		_sut = new LazyReEncryptionMiddleware(
			_migrationService,
			_encryptionProvider,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<LazyReEncryptionMiddleware>.Instance);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenMigrationServiceIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new LazyReEncryptionMiddleware(
			null!,
			A.Fake<IEncryptionProvider>(),
			Microsoft.Extensions.Options.Options.Create(new LazyReEncryptionOptions()),
			NullLogger<LazyReEncryptionMiddleware>.Instance));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenEncryptionProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new LazyReEncryptionMiddleware(
			A.Fake<IEncryptionMigrationService>(),
			null!,
			Microsoft.Extensions.Options.Options.Create(new LazyReEncryptionOptions()),
			NullLogger<LazyReEncryptionMiddleware>.Instance));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new LazyReEncryptionMiddleware(
			A.Fake<IEncryptionMigrationService>(),
			A.Fake<IEncryptionProvider>(),
			null!,
			NullLogger<LazyReEncryptionMiddleware>.Instance));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new LazyReEncryptionMiddleware(
			A.Fake<IEncryptionMigrationService>(),
			A.Fake<IEncryptionProvider>(),
			Microsoft.Extensions.Options.Options.Create(new LazyReEncryptionOptions()),
			null!));
	}

	#endregion Constructor Tests

	#region Stage Tests

	[Fact]
	public void Stage_ReturnPreProcessing()
	{
		// Assert
		_sut.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	#endregion Stage Tests

	#region InvokeAsync Tests

	[Fact]
	public async Task InvokeAsync_ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.InvokeAsync(null!, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.InvokeAsync(message, null!, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_SkipProcessing_WhenDisabled()
	{
		// Arrange
		var disabledOptions = new LazyReEncryptionOptions { Enabled = false };
		var middleware = new LazyReEncryptionMiddleware(
			_migrationService,
			_encryptionProvider,
			Microsoft.Extensions.Options.Options.Create(disabledOptions),
			NullLogger<LazyReEncryptionMiddleware>.Instance);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateEmptyContext();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _migrationService.RequiresMigrationAsync(A<EncryptedData>._, A<MigrationPolicy>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task InvokeAsync_CallNextDelegate_WhenNoEncryptedPayload()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = CreateEmptyContext();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task InvokeAsync_SkipMigration_WhenDataDoesNotRequireMigration()
	{
		// Arrange
		var encryptedData = CreateEncryptedData();
		var message = A.Fake<IDispatchMessage>();
		var context = CreateContextWithEncryptedPayload(encryptedData);
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		_ = A.CallTo(() => _migrationService.RequiresMigrationAsync(encryptedData, A<MigrationPolicy>._, A<CancellationToken>._))
			.Returns(false);

		// Act
		var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _migrationService.MigrateAsync(A<EncryptedData>._, A<EncryptionContext>._, A<EncryptionContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task InvokeAsync_MigrateData_WhenMigrationRequired()
	{
		// Arrange
		var originalData = CreateEncryptedData("old-key");
		var migratedData = CreateEncryptedData("new-key");
		var message = A.Fake<IDispatchMessage>();
		var (context, properties) = CreateContextWithEncryptedPayloadAndProperties(originalData);
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		_ = A.CallTo(() => _migrationService.RequiresMigrationAsync(originalData, A<MigrationPolicy>._, A<CancellationToken>._))
			.Returns(true);
		_ = A.CallTo(() => _migrationService.MigrateAsync(originalData, A<EncryptionContext>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(EncryptionMigrationResult.Succeeded(migratedData, TimeSpan.FromMilliseconds(10), "old-key", "new-key"));

		// Act
		var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		properties["EncryptedPayload"].ShouldBe(migratedData);
		properties["WasLazilyReEncrypted"].ShouldBe(true);
	}

	[Fact]
	public async Task InvokeAsync_SetContextProperties_AfterSuccessfulMigration()
	{
		// Arrange
		var originalData = CreateEncryptedData("old-key");
		var migratedData = CreateEncryptedData("new-key");
		var message = A.Fake<IDispatchMessage>();
		var (context, properties) = CreateContextWithEncryptedPayloadAndProperties(originalData);
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		_ = A.CallTo(() => _migrationService.RequiresMigrationAsync(originalData, A<MigrationPolicy>._, A<CancellationToken>._))
			.Returns(true);
		_ = A.CallTo(() => _migrationService.MigrateAsync(originalData, A<EncryptionContext>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(EncryptionMigrationResult.Succeeded(migratedData, TimeSpan.FromMilliseconds(10), "old-key", "new-key"));

		// Act
		_ = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		properties["OriginalKeyId"].ShouldBe("old-key");
		properties["MigratedKeyId"].ShouldBe("new-key");
	}

	[Fact]
	public async Task InvokeAsync_ContinueProcessing_WhenMigrationFailsAndContinueOnFailureTrue()
	{
		// Arrange
		var options = new LazyReEncryptionOptions { ContinueOnFailure = true };
		var middleware = new LazyReEncryptionMiddleware(
			_migrationService,
			_encryptionProvider,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<LazyReEncryptionMiddleware>.Instance);

		var originalData = CreateEncryptedData("old-key");
		var message = A.Fake<IDispatchMessage>();
		var context = CreateContextWithEncryptedPayload(originalData);
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		_ = A.CallTo(() => _migrationService.RequiresMigrationAsync(originalData, A<MigrationPolicy>._, A<CancellationToken>._))
			.Returns(true);
		_ = A.CallTo(() => _migrationService.MigrateAsync(originalData, A<EncryptionContext>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(EncryptionMigrationResult.Failed("Migration failed"));

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - should still call next
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task InvokeAsync_ThrowException_WhenMigrationFailsAndContinueOnFailureFalse()
	{
		// Arrange
		var options = new LazyReEncryptionOptions { ContinueOnFailure = false };
		var middleware = new LazyReEncryptionMiddleware(
			_migrationService,
			_encryptionProvider,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<LazyReEncryptionMiddleware>.Instance);

		var originalData = CreateEncryptedData("old-key");
		var message = A.Fake<IDispatchMessage>();
		var context = CreateContextWithEncryptedPayload(originalData);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		_ = A.CallTo(() => _migrationService.RequiresMigrationAsync(originalData, A<MigrationPolicy>._, A<CancellationToken>._))
			.Returns(true);
		_ = A.CallTo(() => _migrationService.MigrateAsync(originalData, A<EncryptionContext>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(EncryptionMigrationResult.Failed("Migration failed"));

		// Act & Assert
		_ = await Should.ThrowAsync<EncryptionMigrationException>(() =>
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_InvokeCallback_AfterSuccessfulMigration()
	{
		// Arrange
		var callbackInvoked = false;
		EncryptedData? originalFromCallback = null;
		EncryptedData? migratedFromCallback = null;

		var options = new LazyReEncryptionOptions
		{
			OnReEncrypted = (original, migrated, _) =>
			{
				callbackInvoked = true;
				originalFromCallback = original;
				migratedFromCallback = migrated;
				return Task.CompletedTask;
			}
		};
		var middleware = new LazyReEncryptionMiddleware(
			_migrationService,
			_encryptionProvider,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<LazyReEncryptionMiddleware>.Instance);

		var originalData = CreateEncryptedData("old-key");
		var migratedData = CreateEncryptedData("new-key");
		var message = A.Fake<IDispatchMessage>();
		var context = CreateContextWithEncryptedPayload(originalData);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		_ = A.CallTo(() => _migrationService.RequiresMigrationAsync(originalData, A<MigrationPolicy>._, A<CancellationToken>._))
			.Returns(true);
		_ = A.CallTo(() => _migrationService.MigrateAsync(originalData, A<EncryptionContext>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(EncryptionMigrationResult.Succeeded(migratedData, TimeSpan.FromMilliseconds(10), "old-key", "new-key"));

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		callbackInvoked.ShouldBeTrue();
		originalFromCallback.ShouldBe(originalData);
		migratedFromCallback.ShouldBe(migratedData);
	}

	[Fact]
	public async Task InvokeAsync_UseTargetAlgorithmFromOptions()
	{
		// Arrange
		var options = new LazyReEncryptionOptions
		{
			TargetAlgorithm = EncryptionAlgorithm.Aes256CbcHmac,
			TargetKeyId = "target-key"
		};
		var middleware = new LazyReEncryptionMiddleware(
			_migrationService,
			_encryptionProvider,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<LazyReEncryptionMiddleware>.Instance);

		var originalData = CreateEncryptedData("old-key");
		var migratedData = CreateEncryptedData("target-key", EncryptionAlgorithm.Aes256CbcHmac);
		var message = A.Fake<IDispatchMessage>();
		var context = CreateContextWithEncryptedPayload(originalData);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		_ = A.CallTo(() => _migrationService.RequiresMigrationAsync(originalData, A<MigrationPolicy>._, A<CancellationToken>._))
			.Returns(true);

		EncryptionContext? capturedTargetContext = null;
		_ = A.CallTo(() => _migrationService.MigrateAsync(originalData, A<EncryptionContext>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Invokes((EncryptedData _, EncryptionContext _, EncryptionContext target, CancellationToken _) =>
			{
				capturedTargetContext = target;
			})
			.Returns(EncryptionMigrationResult.Succeeded(migratedData, TimeSpan.FromMilliseconds(10), "old-key", "target-key"));

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		_ = capturedTargetContext.ShouldNotBeNull();
		capturedTargetContext.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256CbcHmac);
		capturedTargetContext.KeyId.ShouldBe("target-key");
	}

	[Fact]
	public async Task InvokeAsync_WrapGeneralException_WhenContinueOnFailureFalse()
	{
		// Arrange - Test the catch block for non-EncryptionMigrationException exceptions
		var options = new LazyReEncryptionOptions { ContinueOnFailure = false };
		var middleware = new LazyReEncryptionMiddleware(
			_migrationService,
			_encryptionProvider,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<LazyReEncryptionMiddleware>.Instance);

		var originalData = CreateEncryptedData("old-key");
		var message = A.Fake<IDispatchMessage>();
		var context = CreateContextWithEncryptedPayload(originalData);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		_ = A.CallTo(() => _migrationService.RequiresMigrationAsync(originalData, A<MigrationPolicy>._, A<CancellationToken>._))
			.Returns(true);
		_ = A.CallTo(() => _migrationService.MigrateAsync(originalData, A<EncryptionContext>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Unexpected error"));

		// Act & Assert
		var ex = await Should.ThrowAsync<EncryptionMigrationException>(() =>
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());

		ex.ItemId.ShouldBe("old-key");
		ex.InnerException.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task InvokeAsync_ContinueOnGeneralException_WhenContinueOnFailureTrue()
	{
		// Arrange
		var options = new LazyReEncryptionOptions { ContinueOnFailure = true };
		var middleware = new LazyReEncryptionMiddleware(
			_migrationService,
			_encryptionProvider,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<LazyReEncryptionMiddleware>.Instance);

		var originalData = CreateEncryptedData("old-key");
		var message = A.Fake<IDispatchMessage>();
		var context = CreateContextWithEncryptedPayload(originalData);
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		_ = A.CallTo(() => _migrationService.RequiresMigrationAsync(originalData, A<MigrationPolicy>._, A<CancellationToken>._))
			.Returns(true);
		_ = A.CallTo(() => _migrationService.MigrateAsync(originalData, A<EncryptionContext>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Unexpected error"));

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - should continue processing
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task InvokeAsync_UseTenantIdFromContext_InTargetContext()
	{
		// Arrange
		var originalData = CreateEncryptedData("old-key");
		var migratedData = CreateEncryptedData("new-key");
		var message = A.Fake<IDispatchMessage>();
		var context = CreateContextWithEncryptedPayloadAndTenantId(originalData, "tenant-123", "special-purpose");
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		_ = A.CallTo(() => _migrationService.RequiresMigrationAsync(originalData, A<MigrationPolicy>._, A<CancellationToken>._))
			.Returns(true);

		EncryptionContext? capturedTargetContext = null;
		_ = A.CallTo(() => _migrationService.MigrateAsync(originalData, A<EncryptionContext>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Invokes((EncryptedData _, EncryptionContext _, EncryptionContext target, CancellationToken _) =>
			{
				capturedTargetContext = target;
			})
			.Returns(EncryptionMigrationResult.Succeeded(migratedData, TimeSpan.FromMilliseconds(10), "old-key", "new-key"));

		// Act
		_ = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		_ = capturedTargetContext.ShouldNotBeNull();
		capturedTargetContext.TenantId.ShouldBe("tenant-123");
		capturedTargetContext.Purpose.ShouldBe("special-purpose");
	}

	[Fact]
	public async Task InvokeAsync_UseUnknownErrorMessage_WhenResultErrorMessageIsNull()
	{
		// Arrange - Test the fallback to "Unknown error" when result.ErrorMessage is null
		var options = new LazyReEncryptionOptions { ContinueOnFailure = false };
		var middleware = new LazyReEncryptionMiddleware(
			_migrationService,
			_encryptionProvider,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<LazyReEncryptionMiddleware>.Instance);

		var originalData = CreateEncryptedData("old-key");
		var message = A.Fake<IDispatchMessage>();
		var context = CreateContextWithEncryptedPayload(originalData);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		_ = A.CallTo(() => _migrationService.RequiresMigrationAsync(originalData, A<MigrationPolicy>._, A<CancellationToken>._))
			.Returns(true);
		// Return a failed result with null ErrorMessage
		_ = A.CallTo(() => _migrationService.MigrateAsync(originalData, A<EncryptionContext>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(new EncryptionMigrationResult { Success = false, ErrorMessage = null });

		// Act & Assert
		var ex = await Should.ThrowAsync<EncryptionMigrationException>(() =>
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());

		// Should contain "Unknown error" or the localized equivalent
		ex.Message.ShouldNotBeNullOrEmpty();
		ex.ItemId.ShouldBe("old-key");
	}

	[Fact]
	public async Task InvokeAsync_UseSourceContextFromEncryptedData()
	{
		// Arrange
		var originalData = new EncryptedData
		{
			Ciphertext = new byte[] { 1, 2, 3, 4, 5 },
			KeyId = "source-key-123",
			KeyVersion = 5,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = new byte[12],
			AuthTag = new byte[16],
			TenantId = "source-tenant",
			EncryptedAt = DateTimeOffset.UtcNow
		};
		var migratedData = CreateEncryptedData("new-key");
		var message = A.Fake<IDispatchMessage>();
		var context = CreateContextWithEncryptedPayload(originalData);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		_ = A.CallTo(() => _migrationService.RequiresMigrationAsync(originalData, A<MigrationPolicy>._, A<CancellationToken>._))
			.Returns(true);

		EncryptionContext? capturedSourceContext = null;
		_ = A.CallTo(() => _migrationService.MigrateAsync(originalData, A<EncryptionContext>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Invokes((EncryptedData _, EncryptionContext source, EncryptionContext _, CancellationToken _) =>
			{
				capturedSourceContext = source;
			})
			.Returns(EncryptionMigrationResult.Succeeded(migratedData, TimeSpan.FromMilliseconds(10), "source-key-123", "new-key"));

		// Act
		_ = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		_ = capturedSourceContext.ShouldNotBeNull();
		capturedSourceContext.KeyId.ShouldBe("source-key-123");
		capturedSourceContext.KeyVersion.ShouldBe(5);
		capturedSourceContext.TenantId.ShouldBe("source-tenant");
	}

	#endregion InvokeAsync Tests

	#region LazyReEncryptionOptions Tests

	[Fact]
	public void Options_HaveCorrectDefaults()
	{
		// Arrange
		var options = new LazyReEncryptionOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.ContinueOnFailure.ShouldBeTrue();
		options.TargetAlgorithm.ShouldBeNull();
		options.TargetKeyId.ShouldBeNull();
		options.OnReEncrypted.ShouldBeNull();
		_ = options.MigrationPolicy.ShouldNotBeNull();
	}

	#endregion LazyReEncryptionOptions Tests

	#region Helper Methods

	private static EncryptedData CreateEncryptedData(
		string keyId = "test-key",
		EncryptionAlgorithm algorithm = EncryptionAlgorithm.Aes256Gcm)
	{
		return new EncryptedData
		{
			Ciphertext = new byte[] { 1, 2, 3, 4, 5 },
			KeyId = keyId,
			KeyVersion = 1,
			Algorithm = algorithm,
			Iv = new byte[12],
			AuthTag = new byte[16],
			EncryptedAt = DateTimeOffset.UtcNow
		};
	}

	private static IMessageContext CreateEmptyContext()
	{
		var context = A.Fake<IMessageContext>();

		// Use a real dictionary for Items - the extension method TryGetValue uses Items internally
		var items = new Dictionary<string, object>();
		_ = A.CallTo(() => context.Items).Returns(items);

		return context;
	}

	private static IMessageContext CreateContextWithEncryptedPayload(EncryptedData encryptedData)
	{
		var context = A.Fake<IMessageContext>();

		// Use a real dictionary for Items - the extension method TryGetValue uses Items internally
		var items = new Dictionary<string, object>
		{
			["EncryptedPayload"] = encryptedData
		};
		_ = A.CallTo(() => context.Items).Returns(items);

		return context;
	}

	private static (IMessageContext Context, Dictionary<string, object?> Properties) CreateContextWithEncryptedPayloadAndProperties(EncryptedData encryptedData)
	{
		var context = A.Fake<IMessageContext>();

		// Use real dictionaries - extension methods use these internally
		var items = new Dictionary<string, object>
		{
			["EncryptedPayload"] = encryptedData
		};
		var properties = new Dictionary<string, object?>();

		_ = A.CallTo(() => context.Items).Returns(items);
		_ = A.CallTo(() => context.Properties).Returns(properties);

		return (context, properties);
	}

	private static IMessageContext CreateContextWithEncryptedPayloadAndTenantId(
		EncryptedData encryptedData,
		string tenantId,
		string encryptionPurpose)
	{
		var context = A.Fake<IMessageContext>();

		// Use a real dictionary for Items - the extension method TryGetValue uses Items internally
		var items = new Dictionary<string, object>
		{
			["EncryptedPayload"] = encryptedData,
			["TenantId"] = tenantId,
			["EncryptionPurpose"] = encryptionPurpose
		};
		_ = A.CallTo(() => context.Items).Returns(items);

		return context;
	}

	#endregion Helper Methods
}
