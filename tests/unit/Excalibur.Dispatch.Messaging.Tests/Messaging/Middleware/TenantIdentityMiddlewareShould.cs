// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="TenantIdentityMiddleware"/> class.
/// </summary>
/// <remarks>
/// Sprint 554 - Task S554.40: TenantIdentityMiddleware tests.
/// Tests tenant ID extraction, missing tenant handling, tenant propagation, and validation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class TenantIdentityMiddlewareShould
{
	private readonly ILogger<TenantIdentityMiddleware> _logger;

	public TenantIdentityMiddlewareShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<TenantIdentityMiddleware>();
	}

	private TenantIdentityMiddleware CreateMiddleware(TenantIdentityOptions options)
	{
		return new TenantIdentityMiddleware(MsOptions.Create(options), NullTelemetrySanitizer.Instance, _logger);
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
			new TenantIdentityMiddleware(null!, NullTelemetrySanitizer.Instance, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new TenantIdentityOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new TenantIdentityMiddleware(options, NullTelemetrySanitizer.Instance, null!));
	}

	#endregion

	#region Stage and ApplicableMessageKinds Tests

	[Fact]
	public void HavePreProcessingStage()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions());

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public void HaveAllApplicableMessageKinds()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions());

		// Assert
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions());
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions());
		var message = new FakeDispatchMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions());
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
		var middleware = CreateMiddleware(new TenantIdentityOptions { Enabled = false });
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
	}

	#endregion

	#region Tenant ID Extraction from Header Tests

	[Fact]
	public async Task ExtractTenantId_FromHeader()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			ValidateTenantAccess = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "tenant-abc");

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		context.GetItem<string>("TenantId").ShouldBe("tenant-abc");
	}

	[Fact]
	public async Task ExtractTenantName_FromHeader()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			TenantNameHeader = "X-Tenant-Name",
			ValidateTenantAccess = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "tenant-abc");
		context.SetItem("X-Tenant-Name", "Acme Corp");

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		context.GetItem<string>("TenantName").ShouldBe("Acme Corp");
	}

	[Fact]
	public async Task ExtractTenantRegion_FromHeader()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			TenantRegionHeader = "X-Tenant-Region",
			ValidateTenantAccess = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "tenant-abc");
		context.SetItem("X-Tenant-Region", "us-east-1");

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		context.GetItem<string>("TenantRegion").ShouldBe("us-east-1");
	}

	#endregion

	#region Tenant ID Extraction from Message Property Tests

	[Fact]
	public async Task ExtractTenantId_FromMessageProperty()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = null, // No header configured
			ValidateTenantAccess = false,
		});
		var message = new TenantAwareMessage { TenantId = "tenant-from-property" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		context.GetItem<string>("TenantId").ShouldBe("tenant-from-property");
	}

	#endregion

	#region Default Tenant ID Tests

	[Fact]
	public async Task UseDefaultTenantId_WhenNoTenantFound()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = null,
			DefaultTenantId = "default-tenant",
			ValidateTenantAccess = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		context.GetItem<string>("TenantId").ShouldBe("default-tenant");
	}

	#endregion

	#region Missing Tenant Handling Tests

	[Fact]
	public async Task ThrowInvalidOperationException_WhenNoTenantIdAvailable()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = null,
			DefaultTenantId = null,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowInvalidOperationException_WhenHeaderIsEmptyAndNoDefault()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			DefaultTenantId = null,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		// No X-Tenant-ID set in context

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	#endregion

	#region Tenant Propagation to Downstream Middleware Tests

	[Fact]
	public async Task PropagatesTenantId_ToDownstreamMiddleware()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			ValidateTenantAccess = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "tenant-downstream");
		string? capturedTenantId = null;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			capturedTenantId = ctx.GetItem<string>("TenantId");
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		capturedTenantId.ShouldBe("tenant-downstream");
	}

	[Fact]
	public async Task SetTenantIdHeaderInContext_ForOutboundPropagation()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			ValidateTenantAccess = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "tenant-outbound");

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert - The header value should be re-set for outbound propagation
		context.GetItem<string>("X-Tenant-ID").ShouldBe("tenant-outbound");
	}

	[Fact]
	public async Task CallNextDelegate_WithTenantContext()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			ValidateTenantAccess = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "tenant-123");
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

	#endregion

	#region Tenant Access Validation Tests

	[Fact]
	public async Task ThrowUnauthorizedAccessException_WhenTenantIdIsTooShort()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			ValidateTenantAccess = true,
			MinTenantIdLength = 5,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "ab"); // Too short - min is 5

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(
			middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowUnauthorizedAccessException_WhenTenantIdIsTooLong()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			ValidateTenantAccess = true,
			MaxTenantIdLength = 10,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "this-is-a-very-long-tenant-id"); // Too long

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(
			middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowUnauthorizedAccessException_WhenTenantIdDoesNotMatchPattern()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			ValidateTenantAccess = true,
			TenantIdPattern = @"^[a-z0-9\-]+$",
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "INVALID_TENANT!"); // Contains uppercase and special chars

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(
			middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task PassValidation_WhenTenantIdIsValidFormat()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			ValidateTenantAccess = true,
			MinTenantIdLength = 1,
			MaxTenantIdLength = 50,
			TenantIdPattern = @"^[a-z0-9\-]+$",
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "valid-tenant-123");

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task SkipValidation_WhenValidateTenantAccessIsFalse()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			ValidateTenantAccess = false,
			MinTenantIdLength = 100, // Would fail if validation ran
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "short"); // Would fail length check

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Exception Propagation Tests

	[Fact]
	public async Task RethrowExceptions_FromDownstreamMiddleware()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			ValidateTenantAccess = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "tenant-123");

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			throw new InvalidOperationException("Downstream error");

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());
	}

	#endregion

	#region Tenant Resolution Priority Tests

	[Fact]
	public async Task PreferHeaderOverMessageProperty()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			ValidateTenantAccess = false,
		});
		var message = new TenantAwareMessage { TenantId = "from-property" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		context.SetItem("X-Tenant-ID", "from-header");

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert - Header should take precedence over property
		context.GetItem<string>("TenantId").ShouldBe("from-header");
	}

	[Fact]
	public async Task FallBackToMessageProperty_WhenHeaderNotPresent()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			ValidateTenantAccess = false,
		});
		var message = new TenantAwareMessage { TenantId = "from-property" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		// No header set

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		context.GetItem<string>("TenantId").ShouldBe("from-property");
	}

	[Fact]
	public async Task FallBackToDefault_WhenHeaderAndPropertyNotPresent()
	{
		// Arrange
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			DefaultTenantId = "fallback-default",
			ValidateTenantAccess = false,
		});
		var message = new FakeDispatchMessage(); // No TenantId property
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		context.GetItem<string>("TenantId").ShouldBe("fallback-default");
	}

	#endregion

	#region Default Options Tests

	[Fact]
	public void HaveCorrectDefaultOptionValues()
	{
		// Arrange
		var options = new TenantIdentityOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.ValidateTenantAccess.ShouldBeTrue();
		options.TenantIdHeader.ShouldBe("X-Tenant-ID");
		options.TenantNameHeader.ShouldBe("X-Tenant-Name");
		options.TenantRegionHeader.ShouldBe("X-Tenant-Region");
		options.DefaultTenantId.ShouldBe(TenantDefaults.DefaultTenantId);
		options.MinTenantIdLength.ShouldBe(1);
		options.MaxTenantIdLength.ShouldBe(100);
		options.TenantIdPattern.ShouldBeNull();
	}

	#endregion

	#region Test Message Types

	/// <summary>
	/// Test message that has a TenantId property for extraction.
	/// </summary>
	private sealed class TenantAwareMessage : IDispatchMessage
	{
		public string TenantId { get; set; } = string.Empty;
	}

	#endregion
}
