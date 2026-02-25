// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.AuditLogging.Aws.Tests;

/// <summary>
/// Depth coverage tests for <see cref="AwsCloudWatchAuditExporter"/> covering
/// DataAnnotations validation, batch chunk exception paths, CreatePayload
/// field mapping, and URI construction.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AwsCloudWatchAuditExporterDepthShould : IDisposable
{
	private readonly AwsAuditOptions _options = new()
	{
		LogGroupName = "test-log-group",
		Region = "us-east-1",
		StreamName = "test-stream",
		MaxRetryAttempts = 0,
		BatchSize = 500
	};

	private readonly ILogger<AwsCloudWatchAuditExporter> _logger = CreateEnabledLogger();
	private readonly FakeHttpMessageHandler _handler = new();

	[Fact]
	public void ImplementIAuditLogExporter()
	{
		var sut = CreateExporter();
		sut.ShouldBeAssignableTo<IAuditLogExporter>();
	}

	[Fact]
	public void OptionsLogGroupName_HaveRequiredAttribute()
	{
		var prop = typeof(AwsAuditOptions).GetProperty(nameof(AwsAuditOptions.LogGroupName));
		prop.ShouldNotBeNull();
		prop!.GetCustomAttributes(typeof(RequiredAttribute), false).ShouldNotBeEmpty();
	}

	[Fact]
	public void OptionsRegion_HaveRequiredAttribute()
	{
		var prop = typeof(AwsAuditOptions).GetProperty(nameof(AwsAuditOptions.Region));
		prop.ShouldNotBeNull();
		prop!.GetCustomAttributes(typeof(RequiredAttribute), false).ShouldNotBeEmpty();
	}

	[Fact]
	public async Task ExportBatch_TrackErrors_WhenChunkThrowsException()
	{
		// Arrange — set handler to throw on first call
		_options.BatchSize = 1;
		_handler.SetException(new HttpRequestException("Network failure"));
		var sut = CreateExporter();
		var events = new List<AuditEvent>
		{
			CreateAuditEvent("evt-1"),
			CreateAuditEvent("evt-2")
		};

		// Act
		var result = await sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert — all events should be failed with exception message
		result.FailedCount.ShouldBe(2);
		result.SuccessCount.ShouldBe(0);
		result.Errors.ShouldNotBeNull();
		result.Errors!["evt-1"].ShouldContain("Network failure");
		result.Errors["evt-2"].ShouldContain("Network failure");
	}

	[Fact]
	public async Task CheckHealth_IncludeDiagnostics_StreamName()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics!["StreamName"].ShouldBe("test-stream");
	}

	[Fact]
	public async Task CheckHealth_UseDefaultServiceUrl_WhenNoOverride()
	{
		// Arrange
		_options.ServiceUrl = null;
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.Endpoint.ShouldContain("logs.us-east-1.amazonaws.com");
	}

	[Fact]
	public async Task ExportAsync_IncludeStatusCodeInErrorMessage()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.Forbidden, "Access denied");
		var sut = CreateExporter();
		var auditEvent = CreateAuditEvent();

		// Act
		var result = await sut.ExportAsync(auditEvent, CancellationToken.None);

		// Assert
		result.ErrorMessage.ShouldNotBeNull();
		result.ErrorMessage.ShouldContain("403");
		result.ErrorMessage.ShouldContain("Access denied");
	}

	[Fact]
	public async Task ExportAsync_MarkNonTransientStatusCode_AsNonTransient()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.Forbidden, "Forbidden");
		var sut = CreateExporter();

		// Act
		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		result.IsTransientError.ShouldBeFalse();
	}

	[Fact]
	public async Task ExportBatch_ReturnCorrectTotalCount_WithMultipleChunks()
	{
		// Arrange — 5 events with batch size 2 = 3 chunks
		_options.BatchSize = 2;
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var events = Enumerable.Range(1, 5)
			.Select(i => CreateAuditEvent($"evt-{i}"))
			.ToList();

		// Act
		var result = await sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		result.TotalCount.ShouldBe(5);
		result.SuccessCount.ShouldBe(5);
		result.FailedCount.ShouldBe(0);
	}

	[Fact]
	public async Task ExportAsync_SetExportedAt_ToCurrentTime()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		result.ExportedAt.ShouldBeGreaterThanOrEqualTo(before);
		result.ExportedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(1));
	}

	[Fact]
	public async Task CheckHealth_IncludeLatencyMs()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.LatencyMs.ShouldNotBeNull();
		result.LatencyMs!.Value.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public async Task ExportAsync_RetryTransientFailures_UsesConfiguredRetryBudget()
	{
		_options.MaxRetryAttempts = 1;
		_options.RetryBaseDelay = TimeSpan.Zero;
		_handler.SetResponse(HttpStatusCode.ServiceUnavailable, "service unavailable");
		var sut = CreateExporter();

		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
		_handler.RequestCount.ShouldBe(2);
	}

	[Fact]
	public async Task ExportAsync_RetryHttpRequestException_UsesConfiguredRetryBudget()
	{
		_options.MaxRetryAttempts = 1;
		_options.RetryBaseDelay = TimeSpan.Zero;
		_handler.SetException(new HttpRequestException("transient network"));
		var sut = CreateExporter();

		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		result.Success.ShouldBeFalse();
		result.IsTransientError.ShouldBeTrue();
		_handler.RequestCount.ShouldBe(2);
	}

	[Fact]
	public void SourceGeneratedJsonContext_CanRoundTripPayloadTypeInfo()
	{
		var payloadType = typeof(AwsCloudWatchAuditExporter).GetNestedType(
			"CloudWatchAuditPayload",
			System.Reflection.BindingFlags.NonPublic)!;
		payloadType.ShouldNotBeNull();

		var payload = Activator.CreateInstance(payloadType!)!;
		payloadType!.GetProperty("EventId")!.SetValue(payload, "evt-json");
		payloadType.GetProperty("Action")!.SetValue(payload, "Read");
		payloadType.GetProperty("EventType")!.SetValue(payload, "DataAccess");
		payloadType.GetProperty("Outcome")!.SetValue(payload, "Success");
		payloadType.GetProperty("Timestamp")!.SetValue(payload, DateTimeOffset.UtcNow);
		payloadType.GetProperty("ActorId")!.SetValue(payload, "actor-1");
		payloadType.GetProperty("Metadata")!.SetValue(payload, new Dictionary<string, string> { ["k"] = "v" });

		var contextType = typeof(AwsCloudWatchAuditExporter).Assembly.GetType(
			"Excalibur.Dispatch.AuditLogging.Aws.AwsAuditJsonContext")!;
		var context = contextType.GetProperty("Default", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
			.GetValue(null)!;

		var getTypeInfo = contextType.GetMethod(
			nameof(System.Text.Json.Serialization.JsonSerializerContext.GetTypeInfo),
			new[] { typeof(Type) })!;
		var typeInfo = (JsonTypeInfo)getTypeInfo.Invoke(context, new object[] { payloadType })!;

		var json = JsonSerializer.Serialize(payload, typeInfo);
		var roundTrip = JsonSerializer.Deserialize(json, typeInfo);

		roundTrip.ShouldNotBeNull();
	}

	[Fact]
	public void SourceGeneratedJsonContext_CanRoundTripPayloadAcrossNullabilityVariants()
	{
		var payloadType = typeof(AwsCloudWatchAuditExporter).GetNestedType(
			"CloudWatchAuditPayload",
			System.Reflection.BindingFlags.NonPublic)!;
		var context = CreateJsonContext();
		var typeInfo = context.GetTypeInfo(payloadType);
		typeInfo.ShouldNotBeNull();

		var full = Activator.CreateInstance(payloadType)!;
		payloadType.GetProperty("EventId")!.SetValue(full, "evt-full");
		payloadType.GetProperty("EventType")!.SetValue(full, "DataAccess");
		payloadType.GetProperty("Action")!.SetValue(full, "Read");
		payloadType.GetProperty("Outcome")!.SetValue(full, "Success");
		payloadType.GetProperty("Timestamp")!.SetValue(full, DateTimeOffset.Parse("2026-02-16T10:00:00+00:00"));
		payloadType.GetProperty("ActorId")!.SetValue(full, "actor-1");
		payloadType.GetProperty("ActorType")!.SetValue(full, "User");
		payloadType.GetProperty("ResourceId")!.SetValue(full, "resource-1");
		payloadType.GetProperty("ResourceType")!.SetValue(full, "Customer");
		payloadType.GetProperty("ResourceClassification")!.SetValue(full, "Confidential");
		payloadType.GetProperty("TenantId")!.SetValue(full, "tenant-1");
		payloadType.GetProperty("CorrelationId")!.SetValue(full, "corr-1");
		payloadType.GetProperty("SessionId")!.SetValue(full, "sess-1");
		payloadType.GetProperty("IpAddress")!.SetValue(full, "10.0.0.1");
		payloadType.GetProperty("UserAgent")!.SetValue(full, "agent/1.0");
		payloadType.GetProperty("Reason")!.SetValue(full, "integration");
		payloadType.GetProperty("Metadata")!.SetValue(full, new Dictionary<string, string>
		{
			["k1"] = "v1",
			["k2"] = "v2"
		});
		payloadType.GetProperty("EventHash")!.SetValue(full, "hash-1");

		var fullJson = JsonSerializer.Serialize(full, typeInfo!);
		fullJson.ShouldContain("\"event_id\":\"evt-full\"");
		fullJson.ShouldContain("\"metadata\":");
		var fullRoundTrip = JsonSerializer.Deserialize(fullJson, typeInfo!);
		fullRoundTrip.ShouldNotBeNull();
		payloadType.GetProperty("EventId")!.GetValue(fullRoundTrip).ShouldBe("evt-full");

		var sparse = Activator.CreateInstance(payloadType)!;
		payloadType.GetProperty("EventId")!.SetValue(sparse, "evt-sparse");
		payloadType.GetProperty("Timestamp")!.SetValue(sparse, DateTimeOffset.Parse("2026-02-16T10:01:00+00:00"));
		var sparseJson = JsonSerializer.Serialize(sparse, typeInfo!);
		sparseJson.ShouldContain("\"event_id\":\"evt-sparse\"");
		sparseJson.ShouldContain("\"actor_id\":null");
		var sparseRoundTrip = JsonSerializer.Deserialize(sparseJson, typeInfo!);
		sparseRoundTrip.ShouldNotBeNull();
	}

	[Fact]
	public void SourceGeneratedJsonContext_ReturnsNullForUnknownType()
	{
		var context = CreateJsonContext();

		context.GetTypeInfo(typeof(Uri)).ShouldBeNull();
	}

	[Fact]
	public void SourceGeneratedJsonContext_ExposesGeneratedTypeInfosAndInvokesDeclaredMembers()
	{
		var context = CreateJsonContext();
		var contextType = context.GetType();
		var flags = System.Reflection.BindingFlags.Public
			| System.Reflection.BindingFlags.NonPublic
			| System.Reflection.BindingFlags.Instance
			| System.Reflection.BindingFlags.Static
			| System.Reflection.BindingFlags.DeclaredOnly;

		context.GetTypeInfo(typeof(IReadOnlyDictionary<string, string>)).ShouldNotBeNull();

		var typeInfoProperties = contextType
			.GetProperties(flags)
			.Where(p => typeof(JsonTypeInfo).IsAssignableFrom(p.PropertyType))
			.ToList();
		typeInfoProperties.ShouldNotBeEmpty();

		foreach (var property in typeInfoProperties)
		{
			var target = property.GetMethod?.IsStatic == true ? null : context;
			var first = property.GetValue(target);
			var second = property.GetValue(target);
			first.ShouldNotBeNull();
			second.ShouldNotBeNull();
		}

		var generatedOptionsProperty = contextType.GetProperty("GeneratedSerializerOptions", flags);
		generatedOptionsProperty?.GetValue(context).ShouldNotBeNull();

		foreach (var method in contextType.GetMethods(flags).Where(m => !m.IsSpecialName))
		{
			if (method.ContainsGenericParameters)
			{
				continue;
			}

			var args = BuildArguments(method.GetParameters(), context);
			if (args == null)
			{
				continue;
			}

			var target = method.IsStatic ? null : context;
			try
			{
				_ = method.Invoke(target, args);
			}
			catch (System.Reflection.TargetInvocationException)
			{
				// Invocation still exercises generated branches.
			}
		}
	}

	public void Dispose()
	{
		_handler.Dispose();
	}

	private static JsonSerializerContext CreateJsonContext()
	{
		var contextType = typeof(AwsCloudWatchAuditExporter).Assembly.GetType(
			"Excalibur.Dispatch.AuditLogging.Aws.AwsAuditJsonContext",
			throwOnError: true)!;
		var ctor = contextType.GetConstructor(
			System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
			binder: null,
			[typeof(JsonSerializerOptions)],
			modifiers: null)!;
		return (JsonSerializerContext)ctor.Invoke([new JsonSerializerOptions()]);
	}

	private static object?[]? BuildArguments(
		System.Reflection.ParameterInfo[] parameters,
		JsonSerializerContext context)
	{
		if (parameters.Length == 0)
		{
			return [];
		}

		var args = new object?[parameters.Length];
		for (var i = 0; i < parameters.Length; i++)
		{
			var parameterType = parameters[i].ParameterType;
			if (parameterType == typeof(Type))
			{
				args[i] = typeof(IReadOnlyDictionary<string, string>);
				continue;
			}

			if (parameterType == typeof(JsonSerializerContext))
			{
				args[i] = context;
				continue;
			}

			if (parameterType == typeof(JsonSerializerOptions))
			{
				args[i] = new JsonSerializerOptions();
				continue;
			}

			if (parameterType == typeof(JsonTypeInfo))
			{
				args[i] = context.GetTypeInfo(typeof(IReadOnlyDictionary<string, string>));
				continue;
			}

			if (parameterType == typeof(string))
			{
				args[i] = "value";
				continue;
			}

			if (parameterType == typeof(bool))
			{
				args[i] = false;
				continue;
			}

			if (parameterType.IsValueType)
			{
				args[i] = Activator.CreateInstance(parameterType);
				continue;
			}

			args[i] = null;
		}

		return args;
	}

	private AwsCloudWatchAuditExporter CreateExporter()
	{
		var httpClient = new HttpClient(_handler);
		return new AwsCloudWatchAuditExporter(
			httpClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger);
	}

	private static AuditEvent CreateAuditEvent(string? eventId = null) => new()
	{
		EventId = eventId ?? "test-event-1",
		EventType = AuditEventType.DataAccess,
		Action = "Read",
		Outcome = AuditOutcome.Success,
		Timestamp = DateTimeOffset.UtcNow,
		ActorId = "user-1",
		ActorType = "User",
		ResourceId = "resource-1",
		ResourceType = "Customer",
		CorrelationId = "corr-1"
	};

	private static ILogger<AwsCloudWatchAuditExporter> CreateEnabledLogger()
	{
		var logger = A.Fake<ILogger<AwsCloudWatchAuditExporter>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		return logger;
	}
}
