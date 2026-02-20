// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Tests.TestFakes;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="ContractVersionCheckMiddleware"/> class.
/// </summary>
/// <remarks>
/// Sprint 554 - Task S554.42: ContractVersionCheckMiddleware tests.
/// Tests version compatibility check, rejection of incompatible versions,
/// deprecated version warnings, and configuration options.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class ContractVersionCheckMiddlewareShould
{
	private readonly ILogger<ContractVersionCheckMiddleware> _logger;
	private readonly IContractVersionService _versionService;

	public ContractVersionCheckMiddlewareShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<ContractVersionCheckMiddleware>();
		_versionService = A.Fake<IContractVersionService>();
	}

	private ContractVersionCheckMiddleware CreateMiddleware(ContractVersionCheckOptions options)
	{
		return new ContractVersionCheckMiddleware(MsOptions.Create(options), _versionService, _logger);
	}

	private static DispatchRequestDelegate CreateSuccessDelegate()
	{
		return (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success());
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ContractVersionCheckMiddleware(null!, _versionService, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenVersionServiceIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new ContractVersionCheckOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ContractVersionCheckMiddleware(options, null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new ContractVersionCheckOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ContractVersionCheckMiddleware(options, _versionService, null!));
	}

	#endregion

	#region Stage and ApplicableMessageKinds Tests

	[Fact]
	public void HavePreProcessingStage()
	{
		// Arrange
		var middleware = CreateMiddleware(new ContractVersionCheckOptions());

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public void HaveEventApplicableMessageKinds()
	{
		// Arrange
		var middleware = CreateMiddleware(new ContractVersionCheckOptions());

		// Assert
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Event);
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new ContractVersionCheckOptions());
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new ContractVersionCheckOptions());
		var message = new FakeDispatchMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new ContractVersionCheckOptions());
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Disabled Middleware Tests

	[Fact]
	public async Task PassThroughDirectly_WhenDisabled()
	{
		// Arrange
		var middleware = CreateMiddleware(new ContractVersionCheckOptions { Enabled = false });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		var nextCalled = false;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => _versionService.CheckCompatibilityAsync(
				A<string>._, A<string>._, A<string[]?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Compatible Version Tests

	[Fact]
	public async Task CallNextDelegate_WhenVersionIsCompatible()
	{
		// Arrange
		A.CallTo(() => _versionService.CheckCompatibilityAsync(
				A<string>._, A<string>._, A<string[]?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(VersionCompatibilityResult.Compatible()));

		var middleware = CreateMiddleware(new ContractVersionCheckOptions
		{
			Enabled = true,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Message-Version", "1.0.0");
		var nextCalled = false;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task AssumeCompatible_WhenNoVersionSpecifiedAndExplicitNotRequired()
	{
		// Arrange
		var middleware = CreateMiddleware(new ContractVersionCheckOptions
		{
			Enabled = true,
			RequireExplicitVersions = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		// No version set

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Incompatible Version Tests

	[Fact]
	public async Task ThrowContractVersionException_WhenVersionIsIncompatible()
	{
		// Arrange
		A.CallTo(() => _versionService.CheckCompatibilityAsync(
				A<string>._, A<string>._, A<string[]?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(
				VersionCompatibilityResult.Incompatible("Version 0.1.0 is not compatible with supported versions")));

		var middleware = CreateMiddleware(new ContractVersionCheckOptions
		{
			Enabled = true,
			FailOnIncompatibleVersions = true,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Message-Version", "0.1.0");

		// Act & Assert
		_ = await Should.ThrowAsync<ContractVersionException>(
			middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task NotThrow_WhenVersionIsIncompatibleButFailOnIncompatibleIsFalse()
	{
		// Arrange
		A.CallTo(() => _versionService.CheckCompatibilityAsync(
				A<string>._, A<string>._, A<string[]?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(
				VersionCompatibilityResult.Incompatible("Version 0.1.0 is outdated")));

		var middleware = CreateMiddleware(new ContractVersionCheckOptions
		{
			Enabled = true,
			FailOnIncompatibleVersions = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Message-Version", "0.1.0");

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert - Should proceed despite incompatibility
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnIncompatible_WhenExplicitVersionRequired_ButNoneProvided()
	{
		// Arrange
		var middleware = CreateMiddleware(new ContractVersionCheckOptions
		{
			Enabled = true,
			RequireExplicitVersions = true,
			FailOnIncompatibleVersions = true,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		// No version set

		// Act & Assert
		_ = await Should.ThrowAsync<ContractVersionException>(
			middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	#endregion

	#region Deprecated Version Tests

	[Fact]
	public async Task AllowProcessing_WhenVersionIsDeprecated()
	{
		// Arrange
		A.CallTo(() => _versionService.CheckCompatibilityAsync(
				A<string>._, A<string>._, A<string[]?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(
				VersionCompatibilityResult.Deprecated("Version 1.0.0 will be removed in next release")));

		var middleware = CreateMiddleware(new ContractVersionCheckOptions
		{
			Enabled = true,
			RecordDeprecationMetrics = true,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Message-Version", "1.0.0");
		var nextCalled = false;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - Deprecated versions should still be processed
		nextCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Unknown Version Tests

	[Fact]
	public async Task ThrowContractVersionException_WhenVersionIsUnknownAndFailOnUnknownIsTrue()
	{
		// Arrange
		A.CallTo(() => _versionService.CheckCompatibilityAsync(
				A<string>._, A<string>._, A<string[]?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(
				VersionCompatibilityResult.Unknown("Version not recognized")));

		var middleware = CreateMiddleware(new ContractVersionCheckOptions
		{
			Enabled = true,
			FailOnUnknownVersions = true,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Message-Version", "999.0.0");

		// Act & Assert
		_ = await Should.ThrowAsync<ContractVersionException>(
			middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task AllowProcessing_WhenVersionIsUnknownAndFailOnUnknownIsFalse()
	{
		// Arrange
		A.CallTo(() => _versionService.CheckCompatibilityAsync(
				A<string>._, A<string>._, A<string[]?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(
				VersionCompatibilityResult.Unknown("Version not recognized")));

		var middleware = CreateMiddleware(new ContractVersionCheckOptions
		{
			Enabled = true,
			FailOnUnknownVersions = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Message-Version", "999.0.0");

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Version Resolution Tests

	[Fact]
	public async Task ExtractVersion_FromHeaderContext()
	{
		// Arrange
		string? capturedVersion = null;
		A.CallTo(() => _versionService.CheckCompatibilityAsync(
				A<string>._, A<string>._, A<string[]?>._, A<CancellationToken>._))
			.Invokes((string schemaId, string version, string[]? supported, CancellationToken ct) =>
			{
				capturedVersion = version;
			})
			.Returns(Task.FromResult(VersionCompatibilityResult.Compatible()));

		var middleware = CreateMiddleware(new ContractVersionCheckOptions
		{
			Enabled = true,
			Headers = new VersionCheckHeaders
			{
				VersionHeaderName = "X-Message-Version",
			},
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Message-Version", "2.1.0");

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		capturedVersion.ShouldBe("2.1.0");
	}

	[Fact]
	public async Task PassSupportedVersionsToService()
	{
		// Arrange
		string[]? capturedSupportedVersions = null;
		A.CallTo(() => _versionService.CheckCompatibilityAsync(
				A<string>._, A<string>._, A<string[]?>._, A<CancellationToken>._))
			.Invokes((string schemaId, string version, string[]? supported, CancellationToken ct) =>
			{
				capturedSupportedVersions = supported;
			})
			.Returns(Task.FromResult(VersionCompatibilityResult.Compatible()));

		var middleware = CreateMiddleware(new ContractVersionCheckOptions
		{
			Enabled = true,
			SupportedVersions = ["1.0.0", "2.0.0", "3.0.0"],
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Message-Version", "2.0.0");

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		capturedSupportedVersions.ShouldNotBeNull();
		capturedSupportedVersions.Length.ShouldBe(3);
		capturedSupportedVersions.ShouldContain("1.0.0");
		capturedSupportedVersions.ShouldContain("2.0.0");
		capturedSupportedVersions.ShouldContain("3.0.0");
	}

	#endregion

	#region Exception Propagation Tests

	[Fact]
	public async Task RethrowExceptions_FromDownstreamMiddleware()
	{
		// Arrange
		A.CallTo(() => _versionService.CheckCompatibilityAsync(
				A<string>._, A<string>._, A<string[]?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(VersionCompatibilityResult.Compatible()));

		var middleware = CreateMiddleware(new ContractVersionCheckOptions
		{
			Enabled = true,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Message-Version", "1.0.0");

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			throw new InvalidOperationException("Downstream error");

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());
	}

	#endregion

	#region Default Options Tests

	[Fact]
	public void HaveCorrectDefaultOptionValues()
	{
		// Arrange
		var options = new ContractVersionCheckOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RequireExplicitVersions.ShouldBeFalse();
		options.FailOnIncompatibleVersions.ShouldBeTrue();
		options.FailOnUnknownVersions.ShouldBeFalse();
		options.RecordDeprecationMetrics.ShouldBeTrue();
		options.SupportedVersions.ShouldBeNull();
		options.BypassVersionCheckForTypes.ShouldBeNull();
	}

	[Fact]
	public void HaveCorrectDefaultHeaderValues()
	{
		// Arrange
		var headers = new VersionCheckHeaders();

		// Assert
		headers.VersionHeaderName.ShouldBe("X-Message-Version");
		headers.SchemaIdHeaderName.ShouldBe("X-Schema-MessageId");
		headers.ProducerVersionHeaderName.ShouldBe("X-Producer-Version");
		headers.ProducerServiceHeaderName.ShouldBe("X-Producer-Service");
	}

	#endregion
}
