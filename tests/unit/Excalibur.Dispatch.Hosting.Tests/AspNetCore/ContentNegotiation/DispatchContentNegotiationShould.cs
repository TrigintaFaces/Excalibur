// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Hosting.AspNetCore.ContentNegotiation;

using FakeItEasy;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Hosting.Tests.AspNetCore.ContentNegotiation;

// ──────────────────────────────────────────────────────────────
//  DispatchInputFormatter tests
// ──────────────────────────────────────────────────────────────

[Trait("Category", "Unit")]
[Trait("Component", "Api")]
public sealed class DispatchInputFormatterShould
{
	private readonly ISerializerRegistry _registry;
	private readonly ISerializer _jsonSerializer;

	public DispatchInputFormatterShould()
	{
		_registry = A.Fake<ISerializerRegistry>();
		_jsonSerializer = A.Fake<ISerializer>();
		A.CallTo(() => _jsonSerializer.ContentType).Returns("application/json");
		A.CallTo(() => _jsonSerializer.Name).Returns("json");

		A.CallTo(() => _registry.GetAll())
			.Returns(new[] { ((byte)1, "json", _jsonSerializer) });
	}

	[Fact]
	public void ThrowWhenRegistryIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new DispatchInputFormatter(null!));
	}

	[Fact]
	public void PopulateSupportedMediaTypesFromRegistry()
	{
		var formatter = new DispatchInputFormatter(_registry);
		formatter.SupportedMediaTypes.ShouldNotBeEmpty();
		formatter.SupportedMediaTypes.ShouldContain(mt => mt.ToString() == "application/json");
	}

	[Fact]
	public void SkipSerializersWithNullContentType()
	{
		var nullSerializer = A.Fake<ISerializer>();
		A.CallTo(() => nullSerializer.ContentType).Returns(null!);
		A.CallTo(() => nullSerializer.Name).Returns("null-ct");

		var registry = A.Fake<ISerializerRegistry>();
		A.CallTo(() => registry.GetAll())
			.Returns(new[] { ((byte)2, "null-ct", nullSerializer) });

		var formatter = new DispatchInputFormatter(registry);
		formatter.SupportedMediaTypes.ShouldBeEmpty();
	}

	[Fact]
	public void SkipSerializersWithEmptyContentType()
	{
		var emptySerializer = A.Fake<ISerializer>();
		A.CallTo(() => emptySerializer.ContentType).Returns(string.Empty);
		A.CallTo(() => emptySerializer.Name).Returns("empty-ct");

		var registry = A.Fake<ISerializerRegistry>();
		A.CallTo(() => registry.GetAll())
			.Returns(new[] { ((byte)3, "empty-ct", emptySerializer) });

		var formatter = new DispatchInputFormatter(registry);
		formatter.SupportedMediaTypes.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReadRequestBodyAsync_ReturnFailureWhenContentTypeIsNull()
	{
		var formatter = new DispatchInputFormatter(_registry);
		var context = CreateInputFormatterContext(contentType: null);

		var result = await formatter.ReadRequestBodyAsync(context);
		result.HasError.ShouldBeTrue();
	}

	[Fact]
	public async Task ReadRequestBodyAsync_ReturnFailureWhenBodyIsEmpty()
	{
		var formatter = new DispatchInputFormatter(_registry);
		var context = CreateInputFormatterContext(
			contentType: "application/json",
			body: Array.Empty<byte>());

		var result = await formatter.ReadRequestBodyAsync(context);
		result.HasError.ShouldBeTrue();
	}

	[Fact]
	public async Task ReadRequestBodyAsync_ThrowWhenContextIsNull()
	{
		var formatter = new DispatchInputFormatter(_registry);
		await Should.ThrowAsync<ArgumentNullException>(
			() => formatter.ReadRequestBodyAsync(null!));
	}

	[Fact]
	public async Task ReadRequestBodyAsync_DeserializeWithMatchingSerializer()
	{
		// Use a concrete test serializer because FakeItEasy cannot proxy ReadOnlySpan<byte>
		var testSerializer = new StubJsonSerializer();
		var registry = A.Fake<ISerializerRegistry>();
		A.CallTo(() => registry.GetAll())
			.Returns(new[] { ((byte)1, "json", (ISerializer)testSerializer) });

		var formatter = new DispatchInputFormatter(registry);
		var body = Encoding.UTF8.GetBytes("{\"Value\":\"test\"}");
		var context = CreateInputFormatterContext(
			contentType: "application/json",
			body: body,
			modelType: typeof(TestPayload));

		var result = await formatter.ReadRequestBodyAsync(context);
		result.IsModelSet.ShouldBeTrue();
		result.Model.ShouldNotBeNull();
		result.Model.ShouldBeOfType<TestPayload>();
	}

	[Fact]
	public async Task ReadRequestBodyAsync_ReturnFailureWhenNoSerializerMatchesContentType()
	{
		var formatter = new DispatchInputFormatter(_registry);
		var body = Encoding.UTF8.GetBytes("<xml/>");
		var context = CreateInputFormatterContext(
			contentType: "application/xml",
			body: body);

		var result = await formatter.ReadRequestBodyAsync(context);
		result.HasError.ShouldBeTrue();
	}

	private static InputFormatterContext CreateInputFormatterContext(
		string? contentType = "application/json",
		byte[]? body = null,
		Type? modelType = null)
	{
		var httpContext = new DefaultHttpContext();
		if (contentType != null)
		{
			httpContext.Request.ContentType = contentType;
		}

		if (body != null)
		{
			httpContext.Request.Body = new MemoryStream(body);
		}

		var metadata = new EmptyModelMetadataProvider()
			.GetMetadataForType(modelType ?? typeof(object));

		return new InputFormatterContext(
			httpContext,
			string.Empty,
			new ModelStateDictionary(),
			metadata,
			(stream, encoding) => new StreamReader(stream, encoding));
	}
}

// ──────────────────────────────────────────────────────────────
//  DispatchOutputFormatter tests
// ──────────────────────────────────────────────────────────────

[Trait("Category", "Unit")]
[Trait("Component", "Api")]
public sealed class DispatchOutputFormatterShould
{
	private readonly ISerializerRegistry _registry;
	private readonly ISerializer _jsonSerializer;

	public DispatchOutputFormatterShould()
	{
		_registry = A.Fake<ISerializerRegistry>();
		_jsonSerializer = A.Fake<ISerializer>();
		A.CallTo(() => _jsonSerializer.ContentType).Returns("application/json");
		A.CallTo(() => _jsonSerializer.Name).Returns("json");

		A.CallTo(() => _registry.GetAll())
			.Returns(new[] { ((byte)1, "json", _jsonSerializer) });
		A.CallTo(() => _registry.GetCurrent())
			.Returns(((byte)1, _jsonSerializer));
	}

	[Fact]
	public void ThrowWhenRegistryIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new DispatchOutputFormatter(null!));
	}

	[Fact]
	public void PopulateSupportedMediaTypesFromRegistry()
	{
		var formatter = new DispatchOutputFormatter(_registry);
		formatter.SupportedMediaTypes.ShouldNotBeEmpty();
		formatter.SupportedMediaTypes.ShouldContain(mt => mt.ToString() == "application/json");
	}

	[Fact]
	public async Task WriteResponseBodyAsync_ThrowWhenContextIsNull()
	{
		var formatter = new DispatchOutputFormatter(_registry);
		await Should.ThrowAsync<ArgumentNullException>(
			() => formatter.WriteResponseBodyAsync(null!));
	}

	[Fact]
	public async Task WriteResponseBodyAsync_DoNothingWhenObjectIsNull()
	{
		var formatter = new DispatchOutputFormatter(_registry);
		var context = CreateOutputFormatterContext(obj: null, contentType: "application/json");

		// Should not throw -- just return
		await formatter.WriteResponseBodyAsync(context);

		A.CallTo(() => _jsonSerializer.SerializeObject(A<object>._, A<Type>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task WriteResponseBodyAsync_SerializeWithMatchingSerializer()
	{
		// Use concrete serializer to avoid FakeItEasy Span issues
		var stubSerializer = new StubJsonSerializer();
		var registry = A.Fake<ISerializerRegistry>();
		A.CallTo(() => registry.GetAll())
			.Returns(new[] { ((byte)1, "json", (ISerializer)stubSerializer) });
		A.CallTo(() => registry.GetCurrent())
			.Returns(((byte)1, (ISerializer)stubSerializer));

		var payload = new TestPayload { Value = "output" };

		var formatter = new DispatchOutputFormatter(registry);
		var context = CreateOutputFormatterContext(
			obj: payload,
			contentType: "application/json",
			objectType: typeof(TestPayload));

		await formatter.WriteResponseBodyAsync(context);

		var responseBody = ((MemoryStream)context.HttpContext.Response.Body).ToArray();
		responseBody.Length.ShouldBeGreaterThan(0);
		var deserialized = System.Text.Json.JsonSerializer.Deserialize<TestPayload>(responseBody);
		deserialized.ShouldNotBeNull();
		deserialized!.Value.ShouldBe("output");
	}

	[Fact]
	public async Task WriteResponseBodyAsync_FallbackToCurrentSerializerWhenNoContentTypeMatch()
	{
		var stubSerializer = new StubJsonSerializer();
		var registry = A.Fake<ISerializerRegistry>();
		A.CallTo(() => registry.GetAll())
			.Returns(new[] { ((byte)1, "json", (ISerializer)stubSerializer) });
		A.CallTo(() => registry.GetCurrent())
			.Returns(((byte)1, (ISerializer)stubSerializer));

		var payload = new TestPayload { Value = "fallback" };

		var formatter = new DispatchOutputFormatter(registry);
		var context = CreateOutputFormatterContext(
			obj: payload,
			contentType: "application/xml",
			objectType: typeof(TestPayload));

		await formatter.WriteResponseBodyAsync(context);

		var responseBody = ((MemoryStream)context.HttpContext.Response.Body).ToArray();
		responseBody.Length.ShouldBeGreaterThan(0);
	}

	private static OutputFormatterWriteContext CreateOutputFormatterContext(
		object? obj,
		string contentType,
		Type? objectType = null)
	{
		var httpContext = new DefaultHttpContext();
		httpContext.Response.Body = new MemoryStream();

		return new OutputFormatterWriteContext(
			httpContext,
			(stream, encoding) => new StreamWriter(stream, encoding),
			objectType ?? obj?.GetType() ?? typeof(object),
			obj)
		{
			ContentType = new Microsoft.Extensions.Primitives.StringSegment(contentType)
		};
	}
}

// ──────────────────────────────────────────────────────────────
//  DispatchContentNegotiationExtensions tests
// ──────────────────────────────────────────────────────────────

[Trait("Category", "Unit")]
[Trait("Component", "Api")]
public sealed class DispatchContentNegotiationExtensionsShould
{
	[Fact]
	public void AddDispatchContentNegotiation_ThrowWhenBuilderIsNull()
	{
		IMvcBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() =>
			builder.AddDispatchContentNegotiation());
	}
}

// ──────────────────────────────────────────────────────────────
//  Test helper type
// ──────────────────────────────────────────────────────────────

internal sealed class TestPayload
{
	public string Value { get; set; } = string.Empty;
}

/// <summary>
/// A concrete test serializer because FakeItEasy cannot proxy ReadOnlySpan parameters.
/// </summary>
internal sealed class StubJsonSerializer : ISerializer
{
	public string Name => "json";
	public string Version => "1.0.0";
	public string ContentType => "application/json";

	public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
	{
		var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);
		bufferWriter.Write(json);
	}

	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		return System.Text.Json.JsonSerializer.Deserialize<T>(data)!;
	}

	public byte[] SerializeObject(object value, Type type)
	{
		return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value, type);
	}

	public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
	{
		return System.Text.Json.JsonSerializer.Deserialize(data, type)!;
	}
}
