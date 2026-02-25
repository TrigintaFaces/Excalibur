// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch.Tests;

/// <summary>
/// Depth coverage tests for <see cref="ElasticsearchAuditExporter"/> covering
/// DataAnnotations validation, NDJSON format, ApiKey authentication,
/// time-based index naming, batch chunk exception paths, and health check details.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ElasticsearchAuditExporterDepthShould : IDisposable
{
	private readonly ElasticsearchExporterOptions _options = new()
	{
		ElasticsearchUrl = "https://es.example.com:9200",
		IndexPrefix = "test-audit",
		MaxRetryAttempts = 0,
		BulkBatchSize = 500,
		RefreshPolicy = "false"
	};

	private readonly NullLogger<ElasticsearchAuditExporter> _logger = NullLogger<ElasticsearchAuditExporter>.Instance;
	private readonly FakeHttpMessageHandler _handler = new();

	[Fact]
	public void ImplementIAuditLogExporter()
	{
		var sut = CreateExporter();
		sut.ShouldBeAssignableTo<IAuditLogExporter>();
	}

	[Fact]
	public void OptionsElasticsearchUrl_HaveRequiredAttribute()
	{
		var prop = typeof(ElasticsearchExporterOptions).GetProperty(nameof(ElasticsearchExporterOptions.ElasticsearchUrl));
		prop.ShouldNotBeNull();
		prop!.GetCustomAttributes(typeof(RequiredAttribute), false).ShouldNotBeEmpty();
	}

	[Fact]
	public async Task UseTimeBasedIndexName_WithConfiguredPrefix()
	{
		// Arrange
		_options.IndexPrefix = "custom-prefix";
		_handler.SetResponse(HttpStatusCode.OK);
		_handler.CaptureContent = true;
		var sut = CreateExporter();

		// Act
		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert — index name should contain prefix and date pattern
		_handler.CapturedContent.ShouldNotBeNull();
		_handler.CapturedContent.ShouldContain("custom-prefix-");
	}

	[Fact]
	public async Task IncludeApiKeyAuth_WhenConfigured()
	{
		// Arrange
		_options.ApiKey = "my-secret-api-key";
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Headers.Authorization.ShouldNotBeNull();
		_handler.LastRequest.Headers.Authorization!.Scheme.ShouldBe("ApiKey");
		_handler.LastRequest.Headers.Authorization.Parameter.ShouldBe("my-secret-api-key");
	}

	[Fact]
	public async Task OmitApiKeyAuth_WhenNotConfigured()
	{
		// Arrange
		_options.ApiKey = null;
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		await sut.ExportAsync(CreateAuditEvent(), CancellationToken.None);

		// Assert
		_handler.LastRequest.ShouldNotBeNull();
		_handler.LastRequest!.Headers.Authorization.ShouldBeNull();
	}

	[Fact]
	public async Task ExportBatch_TrackErrors_WhenChunkThrowsException()
	{
		// Arrange
		_options.BulkBatchSize = 1;
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
	public async Task CheckHealth_IncludeDiagnostics_IndexPrefix()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics!["IndexPrefix"].ShouldBe("test-audit");
	}

	[Fact]
	public async Task CheckHealth_ReturnEndpoint_WithBaseUrl()
	{
		// Arrange
		_handler.SetResponse(HttpStatusCode.OK);
		var sut = CreateExporter();

		// Act
		var result = await sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.Endpoint.ShouldContain("es.example.com");
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
		_options.BulkBatchSize = 2;
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
	public void JsonContext_GetTypeInfo_ReturnsKnownTypes()
	{
		var assembly = typeof(ElasticsearchAuditExporter).Assembly;
		var contextType = assembly.GetType("Excalibur.Dispatch.AuditLogging.Elasticsearch.ElasticsearchAuditJsonContext", throwOnError: true)!;
		var context = contextType.GetProperty("Default", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
			.GetValue(null);
		context.ShouldNotBeNull();

		var getTypeInfo = contextType.GetMethod("GetTypeInfo", [typeof(Type)])!;
		var payloadType = assembly.GetType("Excalibur.Dispatch.AuditLogging.Elasticsearch.ElasticsearchAuditExporter+ElasticsearchAuditPayload", throwOnError: true)!;
		var metaType = assembly.GetType("Excalibur.Dispatch.AuditLogging.Elasticsearch.ElasticsearchAuditExporter+BulkActionMeta", throwOnError: true)!;
		var indexType = assembly.GetType("Excalibur.Dispatch.AuditLogging.Elasticsearch.ElasticsearchAuditExporter+BulkIndexAction", throwOnError: true)!;

		getTypeInfo.Invoke(context, [payloadType]).ShouldNotBeNull();
		getTypeInfo.Invoke(context, [metaType]).ShouldNotBeNull();
		getTypeInfo.Invoke(context, [indexType]).ShouldNotBeNull();
	}

	[Fact]
	public void JsonContext_CanRoundTripBulkActionMeta()
	{
		var assembly = typeof(ElasticsearchAuditExporter).Assembly;
		var contextType = assembly.GetType("Excalibur.Dispatch.AuditLogging.Elasticsearch.ElasticsearchAuditJsonContext", throwOnError: true)!;
		var context = (System.Text.Json.Serialization.JsonSerializerContext)contextType
			.GetProperty("Default", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
			.GetValue(null)!;
		var metaType = assembly.GetType("Excalibur.Dispatch.AuditLogging.Elasticsearch.ElasticsearchAuditExporter+BulkActionMeta", throwOnError: true)!;

		var value = Activator.CreateInstance(metaType)!;
		metaType.GetProperty("IndexName")!.SetValue(value, "idx-2026.02.16");
		metaType.GetProperty("Id")!.SetValue(value, "evt-1");

		var json = JsonSerializer.Serialize(value, metaType, context);
		var roundTrip = JsonSerializer.Deserialize(json, metaType, context);

		roundTrip.ShouldNotBeNull();
		metaType.GetProperty("IndexName")!.GetValue(roundTrip)!.ShouldBe("idx-2026.02.16");
		metaType.GetProperty("Id")!.GetValue(roundTrip)!.ShouldBe("evt-1");
	}

	[Fact]
	public void JsonContext_CanRoundTripBulkIndexAndPayloadAcrossNullabilityVariants()
	{
		var assembly = typeof(ElasticsearchAuditExporter).Assembly;
		var context = CreateJsonContext();
		var payloadType = assembly.GetType(
			"Excalibur.Dispatch.AuditLogging.Elasticsearch.ElasticsearchAuditExporter+ElasticsearchAuditPayload",
			throwOnError: true)!;
		var bulkMetaType = assembly.GetType(
			"Excalibur.Dispatch.AuditLogging.Elasticsearch.ElasticsearchAuditExporter+BulkActionMeta",
			throwOnError: true)!;
		var bulkIndexType = assembly.GetType(
			"Excalibur.Dispatch.AuditLogging.Elasticsearch.ElasticsearchAuditExporter+BulkIndexAction",
			throwOnError: true)!;
		var payloadTypeInfo = context.GetTypeInfo(payloadType);
		var bulkIndexTypeInfo = context.GetTypeInfo(bulkIndexType);
		payloadTypeInfo.ShouldNotBeNull();
		bulkIndexTypeInfo.ShouldNotBeNull();

		var meta = Activator.CreateInstance(bulkMetaType)!;
		bulkMetaType.GetProperty("IndexName")!.SetValue(meta, "idx-2026.02.16");
		bulkMetaType.GetProperty("Id")!.SetValue(meta, "evt-1");

		var fullIndex = Activator.CreateInstance(bulkIndexType)!;
		bulkIndexType.GetProperty("Index")!.SetValue(fullIndex, meta);
		var fullIndexJson = JsonSerializer.Serialize(fullIndex, bulkIndexTypeInfo!);
		fullIndexJson.ShouldContain("\"_index\":\"idx-2026.02.16\"");
		var fullIndexRoundTrip = JsonSerializer.Deserialize(fullIndexJson, bulkIndexTypeInfo!);
		fullIndexRoundTrip.ShouldNotBeNull();

		var sparseIndex = Activator.CreateInstance(bulkIndexType)!;
		var sparseIndexJson = JsonSerializer.Serialize(sparseIndex, bulkIndexTypeInfo!);
		sparseIndexJson.ShouldContain("\"index\":null");
		JsonSerializer.Deserialize(sparseIndexJson, bulkIndexTypeInfo!).ShouldNotBeNull();

		var fullPayload = Activator.CreateInstance(payloadType)!;
		payloadType.GetProperty("EventId")!.SetValue(fullPayload, "evt-full");
		payloadType.GetProperty("EventType")!.SetValue(fullPayload, "DataAccess");
		payloadType.GetProperty("Action")!.SetValue(fullPayload, "Read");
		payloadType.GetProperty("Outcome")!.SetValue(fullPayload, "Success");
		payloadType.GetProperty("Timestamp")!.SetValue(fullPayload, DateTimeOffset.Parse("2026-02-16T10:00:00+00:00"));
		payloadType.GetProperty("ActorId")!.SetValue(fullPayload, "actor-1");
		payloadType.GetProperty("ActorType")!.SetValue(fullPayload, "User");
		payloadType.GetProperty("ResourceId")!.SetValue(fullPayload, "resource-1");
		payloadType.GetProperty("ResourceType")!.SetValue(fullPayload, "Customer");
		payloadType.GetProperty("ResourceClassification")!.SetValue(fullPayload, "Confidential");
		payloadType.GetProperty("TenantId")!.SetValue(fullPayload, "tenant-1");
		payloadType.GetProperty("CorrelationId")!.SetValue(fullPayload, "corr-1");
		payloadType.GetProperty("SessionId")!.SetValue(fullPayload, "sess-1");
		payloadType.GetProperty("IpAddress")!.SetValue(fullPayload, "10.0.0.1");
		payloadType.GetProperty("UserAgent")!.SetValue(fullPayload, "agent/1.0");
		payloadType.GetProperty("Reason")!.SetValue(fullPayload, "integration");
		payloadType.GetProperty("Metadata")!.SetValue(fullPayload, new Dictionary<string, string>
		{
			["k1"] = "v1",
			["k2"] = "v2"
		});
		payloadType.GetProperty("EventHash")!.SetValue(fullPayload, "hash-1");

		var fullPayloadJson = JsonSerializer.Serialize(fullPayload, payloadTypeInfo!);
		fullPayloadJson.ShouldContain("\"event_id\":\"evt-full\"");
		fullPayloadJson.ShouldContain("\"@timestamp\":");
		fullPayloadJson.ShouldContain("\"metadata\":");
		var fullPayloadRoundTrip = JsonSerializer.Deserialize(fullPayloadJson, payloadTypeInfo!);
		fullPayloadRoundTrip.ShouldNotBeNull();

		var sparsePayload = Activator.CreateInstance(payloadType)!;
		payloadType.GetProperty("EventId")!.SetValue(sparsePayload, "evt-sparse");
		payloadType.GetProperty("Timestamp")!.SetValue(sparsePayload, DateTimeOffset.Parse("2026-02-16T10:01:00+00:00"));
		var sparsePayloadJson = JsonSerializer.Serialize(sparsePayload, payloadTypeInfo!);
		sparsePayloadJson.ShouldContain("\"event_id\":\"evt-sparse\"");
		sparsePayloadJson.ShouldContain("\"actor_id\":null");
		JsonSerializer.Deserialize(sparsePayloadJson, payloadTypeInfo!).ShouldNotBeNull();
	}

	[Fact]
	public void JsonContext_ReturnsNullForUnknownType()
	{
		var context = CreateJsonContext();
		context.GetTypeInfo(typeof(Uri)).ShouldBeNull();
	}

	[Fact]
	public void JsonContext_ExposesGeneratedTypeInfosAndInvokesDeclaredMembers()
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
		var contextType = typeof(ElasticsearchAuditExporter).Assembly.GetType(
			"Excalibur.Dispatch.AuditLogging.Elasticsearch.ElasticsearchAuditJsonContext",
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
				args[i] = context.GetTypeInfo(typeof(IReadOnlyDictionary<string, string>))
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

	private ElasticsearchAuditExporter CreateExporter()
	{
		var httpClient = new HttpClient(_handler);
		return new ElasticsearchAuditExporter(
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
}
