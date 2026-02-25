// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.CloudEvents;

/// <summary>
/// Unit tests for the <see cref="CloudEventMiddleware"/> class.
/// Sprint 561 S561.42: Validates CloudEvents envelope, content-type, extensions, and schema processing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class CloudEventMiddlewareShould
{
	private readonly ILogger<CloudEventMiddleware> _logger;
	private readonly IEnvelopeCloudEventBridge _bridge;
	private readonly ISchemaRegistry _schemaRegistry;

	public CloudEventMiddlewareShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<CloudEventMiddleware>();
		_bridge = A.Fake<IEnvelopeCloudEventBridge>();
		_schemaRegistry = A.Fake<ISchemaRegistry>();
	}

	private CloudEventMiddleware CreateMiddleware(
		CloudEventOptions? options = null,
		ISchemaRegistry? schemaRegistry = null,
		bool includeSchemaRegistry = false)
	{
		return new CloudEventMiddleware(
			_logger,
			MsOptions.Create(options ?? new CloudEventOptions()),
			_bridge,
			includeSchemaRegistry ? (schemaRegistry ?? _schemaRegistry) : null);
	}

	private static DispatchRequestDelegate CreateSuccessDelegate()
	{
		return (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success());
	}

	#region Constructor and Stage Tests

	[Fact]
	public void HavePreProcessingStage()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new CloudEventMiddleware(null!, MsOptions.Create(new CloudEventOptions()), _bridge));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new CloudEventMiddleware(_logger, null!, _bridge));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBridgeIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new CloudEventMiddleware(_logger, MsOptions.Create(new CloudEventOptions()), null!));
	}

	[Fact]
	public void AllowNullSchemaRegistry()
	{
		// Act
		var middleware = new CloudEventMiddleware(
			_logger, MsOptions.Create(new CloudEventOptions()), _bridge, null);

		// Assert
		_ = middleware.ShouldNotBeNull();
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var context = new FakeMessageContext { MessageId = "test-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Incoming CloudEvent Processing Tests

	[Fact]
	public async Task ProcessIncomingCloudEvent_FromContextItems()
	{
		// Arrange
		var middleware = CreateMiddleware(new CloudEventOptions { ValidateSchema = false });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };

		var incomingCe = new CloudEvent
		{
			Id = "ce-123",
			Source = new Uri("urn:test-source"),
			Type = "test.event.v1",
			Time = DateTimeOffset.UtcNow,
		};
		context.Items["cloudevent"] = incomingCe;

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		context.MessageId.ShouldBe("ce-123");
		context.Source.ShouldBe("urn:test-source");
	}

	[Fact]
	public async Task EnrichContext_WithCorrelationId_FromCloudEventExtension()
	{
		// Arrange
		var middleware = CreateMiddleware(new CloudEventOptions { ValidateSchema = false });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };

		var incomingCe = new CloudEvent
		{
			Id = "ce-456",
			Source = new Uri("urn:test"),
			Type = "test.event.v1",
		};
		incomingCe["correlationid"] = "corr-abc-123";
		context.Items["cloudevent"] = incomingCe;

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		context.CorrelationId.ShouldBe("corr-abc-123");
	}

	[Fact]
	public async Task EnrichContext_WithTraceParent_FromCloudEventExtension()
	{
		// Arrange
		var middleware = CreateMiddleware(new CloudEventOptions { ValidateSchema = false });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };

		var incomingCe = new CloudEvent
		{
			Id = "ce-789",
			Source = new Uri("urn:test"),
			Type = "test.event.v1",
		};
		incomingCe["traceparent"] = "00-trace-id-span-id-01";
		context.Items["cloudevent"] = incomingCe;

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		context.TraceParent.ShouldBe("00-trace-id-span-id-01");
	}

	[Fact]
	public async Task CopyExtensionAttributes_ToContextItems()
	{
		// Arrange
		var middleware = CreateMiddleware(new CloudEventOptions { ValidateSchema = false });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };

		var incomingCe = new CloudEvent
		{
			Id = "ce-ext",
			Source = new Uri("urn:test"),
			Type = "test.event.v1",
		};
		incomingCe["customext"] = "custom-value";
		context.Items["cloudevent"] = incomingCe;

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		context.Items.ShouldContainKey("ce.customext");
		context.Items["ce.customext"].ShouldBe("custom-value");
	}

	[Fact]
	public async Task ExcludeExtensions_WhenConfiguredInOptions()
	{
		// Arrange
		var options = new CloudEventOptions { ValidateSchema = false };
		options.ExcludedExtensions.Add("secretext");

		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };

		var incomingCe = new CloudEvent
		{
			Id = "ce-exclude",
			Source = new Uri("urn:test"),
			Type = "test.event.v1",
		};
		incomingCe["secretext"] = "should-be-excluded";
		incomingCe["publicext"] = "should-be-included";
		context.Items["cloudevent"] = incomingCe;

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		context.Items.ShouldNotContainKey("ce.secretext");
		context.Items.ShouldContainKey("ce.publicext");
	}

	#endregion

	#region CloudEvent Validation Tests

	[Fact]
	public async Task ThrowInvalidOperationException_WhenCloudEventHasNoType()
	{
		// Arrange
		var middleware = CreateMiddleware(new CloudEventOptions { ValidateSchema = false });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };

		var incomingCe = new CloudEvent
		{
			Id = "ce-no-type",
			Source = new Uri("urn:test"),
			// Type is null
		};
		context.Items["cloudevent"] = incomingCe;

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task UseCustomValidator_WhenConfigured()
	{
		// Arrange
		var validatorCalled = false;
		var options = new CloudEventOptions
		{
			ValidateSchema = false,
			CustomValidator = (ce, ct) =>
			{
				validatorCalled = true;
				return Task.FromResult(true);
			},
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };

		var incomingCe = new CloudEvent
		{
			Id = "ce-custom-val",
			Source = new Uri("urn:test"),
			Type = "test.event.v1",
		};
		context.Items["cloudevent"] = incomingCe;

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		validatorCalled.ShouldBeTrue("Custom validator should be called");
	}

	[Fact]
	public async Task ThrowInvalidOperationException_WhenCustomValidatorReturnsFailure()
	{
		// Arrange
		var options = new CloudEventOptions
		{
			ValidateSchema = false,
			CustomValidator = (ce, ct) => Task.FromResult(false),
		};
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };

		var incomingCe = new CloudEvent
		{
			Id = "ce-fail-val",
			Source = new Uri("urn:test"),
			Type = "test.event.v1",
		};
		context.Items["cloudevent"] = incomingCe;

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	#endregion

	#region Pass-Through Tests

	[Fact]
	public async Task PassThrough_WhenNoCloudEventInContext()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-no-ce" };
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
	public async Task DetectIncomingCloudEvent_FromAlternateKey()
	{
		// Arrange
		var middleware = CreateMiddleware(new CloudEventOptions { ValidateSchema = false });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };

		var incomingCe = new CloudEvent
		{
			Id = "ce-alt",
			Source = new Uri("urn:alt-source"),
			Type = "test.event.alternate",
		};
		context.Items["cloudevent.incoming"] = incomingCe;

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		context.MessageId.ShouldBe("ce-alt");
	}

	#endregion

	#region CloudEvent Time and Source Tests

	[Fact]
	public async Task SetSentTimestamp_FromCloudEventTime()
	{
		// Arrange
		var middleware = CreateMiddleware(new CloudEventOptions { ValidateSchema = false });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };
		var eventTime = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

		var incomingCe = new CloudEvent
		{
			Id = "ce-time",
			Source = new Uri("urn:test"),
			Type = "test.event.v1",
			Time = eventTime,
		};
		context.Items["cloudevent"] = incomingCe;

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		context.SentTimestampUtc.ShouldBe(eventTime);
	}

	[Fact]
	public async Task SetSource_FromCloudEventSource()
	{
		// Arrange
		var middleware = CreateMiddleware(new CloudEventOptions { ValidateSchema = false });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };

		var incomingCe = new CloudEvent
		{
			Id = "ce-source",
			Source = new Uri("https://my-service.example.com/events"),
			Type = "test.event.v1",
		};
		context.Items["cloudevent"] = incomingCe;

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		context.Source.ShouldBe("https://my-service.example.com/events");
	}

	#endregion

	#region Schema Validation Tests

	[Fact]
	public async Task ValidateSchema_WhenRegistryIsAvailable()
	{
		// Arrange
		var options = new CloudEventOptions { ValidateSchema = true };
		var middleware = CreateMiddleware(options, includeSchemaRegistry: true);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-1" };

		var incomingCe = new CloudEvent
		{
			Id = "ce-schema",
			Source = new Uri("urn:test"),
			Type = "test.event.v1",
		};
		incomingCe.SetSchemaVersion("1.0");
		context.Items["cloudevent"] = incomingCe;

		_ = A.CallTo(() => _schemaRegistry.GetSchemaAsync("test.event.v1", "1.0", A<CancellationToken>._))
			.Returns(Task.FromResult<string?>("{}"));

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		_ = A.CallTo(() => _schemaRegistry.GetSchemaAsync("test.event.v1", "1.0", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion
}
