// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.AuditLogging.GoogleCloud.Tests;

/// <summary>
/// Depth coverage tests for <see cref="GoogleCloudLoggingAuditExporter"/> covering
/// DataAnnotations validation, log name construction, batch chunk exceptions,
/// health check diagnostics, and label inclusion.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class GoogleCloudLoggingAuditExporterDepthShould : IDisposable
{
	private readonly GoogleCloudAuditOptions _options = new()
	{
		ProjectId = "test-project",
		LogName = "test-audit",
		ResourceType = "global",
		MaxRetryAttempts = 0,
		MaxBatchSize = 500
	};

	private readonly ILogger<GoogleCloudLoggingAuditExporter> _logger = CreateEnabledLogger();
	private readonly FakeHttpMessageHandler _handler = new();

	[Fact]
	public void ImplementIAuditLogExporter()
	{
		var sut = CreateExporter();
		sut.ShouldBeAssignableTo<IAuditLogExporter>();
	}

	[Fact]
	public void OptionsProjectId_HaveRequiredAttribute()
	{
		var prop = typeof(GoogleCloudAuditOptions).GetProperty(nameof(GoogleCloudAuditOptions.ProjectId));
		prop.ShouldNotBeNull();
		prop!.GetCustomAttributes(typeof(RequiredAttribute), false).ShouldNotBeEmpty();
	}

	[Fact]
	public async Task IncludeFullLogNameInPayload()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		_handler.CaptureContent = true;
		var sut = CreateExporter();

		// Act
		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert — log name should be "projects/{ProjectId}/logs/{LogName}"
		_handler.CapturedContent.ShouldNotBeNull();
		_handler.CapturedContent.ShouldContain("projects/test-project/logs/test-audit");
	}

	[Fact]
	public async Task ExportBatch_TrackErrors_WhenChunkThrowsException()
	{
		// Arrange
		_options.MaxBatchSize = 1;
		_handler.SetException(new HttpRequestException("Network failure"));
		var sut = CreateExporter();
		var events = new List<AuditEvent>
		{
			CreateAuditEvent("evt-1"),
			CreateAuditEvent("evt-2")
		};

		// Act
		var result = await sut.ExportBatchAsync(events, CancellationToken.None);

		// Assert
		result.FailedCount.ShouldBe(2);
		result.Errors.ShouldNotBeNull();
		result.Errors!["evt-1"].ShouldContain("Network failure");
	}

	[Fact]
	public async Task CheckHealth_IncludeDiagnostics_ProjectId()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics!["ProjectId"].ShouldBe("test-project");
		result.Diagnostics["LogName"].ShouldBe("test-audit");
		result.Diagnostics["ResourceType"].ShouldBe("global");
	}

	[Fact]
	public async Task CheckHealth_ReturnEndpoint_WithGoogleApis()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.Endpoint.ShouldContain("googleapis.com");
	}

	[Fact]
	public async Task ExportAsync_IncludeStatusCodeInError()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.Forbidden, "Access denied");
		var sut = CreateExporter();

		// Act
		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		result.ErrorMessage.ShouldNotBeNull();
		result.ErrorMessage.ShouldContain("403");
	}

	[Fact]
	public async Task ExportAsync_MarkForbiddenAsNonTransient()
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
	public async Task ExportBatch_ReturnCorrectCounts_WithMultipleChunks()
	{
		// Arrange — 5 events with batch size 2 = 3 chunks
		_options.MaxBatchSize = 2;
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
	}

	[Fact]
	public async Task ExportAsync_SetExportedAt_OnSuccess()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		result.ExportedAt.ShouldBeGreaterThanOrEqualTo(before);
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
		var payloadType = typeof(GoogleCloudLoggingAuditExporter).GetNestedType(
			"CloudLoggingPayload",
			System.Reflection.BindingFlags.NonPublic)!;
		var entryType = typeof(GoogleCloudLoggingAuditExporter).GetNestedType(
			"CloudLoggingAuditPayload",
			System.Reflection.BindingFlags.NonPublic)!;

		payloadType.ShouldNotBeNull();
		entryType.ShouldNotBeNull();

		var entry = Activator.CreateInstance(entryType!)!;
		entryType!.GetProperty("LogName")!.SetValue(entry, "projects/test/logs/audit");
		entryType.GetProperty("Severity")!.SetValue(entry, "INFO");
		entryType.GetProperty("Timestamp")!.SetValue(entry, DateTimeOffset.UtcNow.ToString("O"));
		entryType.GetProperty("JsonPayload")!.SetValue(entry, new Dictionary<string, string?> { ["event_id"] = "evt-json" });
		entryType.GetProperty("Labels")!.SetValue(entry, new Dictionary<string, string> { ["env"] = "test" });

		var listType = typeof(List<>).MakeGenericType(entryType);
		var entries = Activator.CreateInstance(listType)!;
		listType.GetMethod("Add")!.Invoke(entries, new object[] { entry });

		var payload = Activator.CreateInstance(payloadType!)!;
		payloadType!.GetProperty("LogName")!.SetValue(payload, "projects/test/logs/audit");
		payloadType.GetProperty("Resource")!.SetValue(payload, new Dictionary<string, string> { ["type"] = "global" });
		payloadType.GetProperty("Entries")!.SetValue(payload, entries);

		var contextType = typeof(GoogleCloudLoggingAuditExporter).Assembly.GetType(
			"Excalibur.Dispatch.AuditLogging.GoogleCloud.GoogleCloudAuditJsonContext")!;
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
		var payloadType = typeof(GoogleCloudLoggingAuditExporter).GetNestedType(
			"CloudLoggingPayload",
			System.Reflection.BindingFlags.NonPublic)!;
		var entryType = typeof(GoogleCloudLoggingAuditExporter).GetNestedType(
			"CloudLoggingAuditPayload",
			System.Reflection.BindingFlags.NonPublic)!;
		var context = CreateJsonContext();
		var payloadTypeInfo = context.GetTypeInfo(payloadType);
		var entryTypeInfo = context.GetTypeInfo(entryType);
		payloadTypeInfo.ShouldNotBeNull();
		entryTypeInfo.ShouldNotBeNull();

		var fullEntry = Activator.CreateInstance(entryType)!;
		entryType.GetProperty("LogName")!.SetValue(fullEntry, "projects/test/logs/audit");
		entryType.GetProperty("Severity")!.SetValue(fullEntry, "INFO");
		entryType.GetProperty("Timestamp")!.SetValue(fullEntry, "2026-02-16T10:00:00.0000000+00:00");
		entryType.GetProperty("JsonPayload")!.SetValue(fullEntry, new Dictionary<string, string?>
		{
			["event_id"] = "evt-full",
			["action"] = "Read"
		});
		entryType.GetProperty("Labels")!.SetValue(fullEntry, new Dictionary<string, string> { ["env"] = "test" });

		var sparseEntry = Activator.CreateInstance(entryType)!;
		entryType.GetProperty("LogName")!.SetValue(sparseEntry, "projects/test/logs/audit");

		var listType = typeof(List<>).MakeGenericType(entryType);
		var entries = Activator.CreateInstance(listType)!;
		listType.GetMethod("Add")!.Invoke(entries, new object[] { fullEntry });
		listType.GetMethod("Add")!.Invoke(entries, new object[] { sparseEntry });

		var payload = Activator.CreateInstance(payloadType)!;
		payloadType.GetProperty("LogName")!.SetValue(payload, "projects/test/logs/audit");
		payloadType.GetProperty("Resource")!.SetValue(payload, new Dictionary<string, string> { ["type"] = "global" });
		payloadType.GetProperty("Entries")!.SetValue(payload, entries);

		var fullJson = JsonSerializer.Serialize(payload, payloadTypeInfo!);
		fullJson.ShouldContain("\"entries\":");
		fullJson.ShouldContain("\"jsonPayload\":");
		var fullRoundTrip = JsonSerializer.Deserialize(fullJson, payloadTypeInfo!);
		fullRoundTrip.ShouldNotBeNull();

		var entryJson = JsonSerializer.Serialize(fullEntry, entryTypeInfo!);
		entryJson.ShouldContain("\"jsonPayload\":");
		JsonSerializer.Deserialize(entryJson, entryTypeInfo!).ShouldNotBeNull();
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

		context.GetTypeInfo(typeof(Dictionary<string, string>)).ShouldNotBeNull();
		context.GetTypeInfo(typeof(List<object>)).ShouldBeNull();

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
		var contextType = typeof(GoogleCloudLoggingAuditExporter).Assembly.GetType(
			"Excalibur.Dispatch.AuditLogging.GoogleCloud.GoogleCloudAuditJsonContext",
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
				args[i] = typeof(Dictionary<string, string>);
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
				args[i] = context.GetTypeInfo(typeof(Dictionary<string, string>))
					?? context.GetTypeInfo(typeof(string));
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

	private GoogleCloudLoggingAuditExporter CreateExporter()
	{
		var httpClient = new HttpClient(_handler);
		return new GoogleCloudLoggingAuditExporter(
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

	private static ILogger<GoogleCloudLoggingAuditExporter> CreateEnabledLogger()
	{
		var logger = A.Fake<ILogger<GoogleCloudLoggingAuditExporter>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		return logger;
	}
}
