// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Text;

using Excalibur.Dispatch.Hosting.AspNetCore.ContentNegotiation;
using Excalibur.Dispatch.Serialization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests.ContentNegotiation;

/// <summary>
/// Locks that a malformed request body becomes MODEL STATE rather than an escaping exception.
/// </summary>
/// <remarks>
/// <para>
/// MVC's <c>BodyModelBinder</c> translates <c>InputFormatterException</c> and a formatter's explicitly
/// opted-in exception types. It does NOT inspect <c>ApiException.StatusCode</c>, so a Dispatch
/// <see cref="SerializationException" /> escaping the formatter bypasses the normal model-state 400
/// entirely.
/// </para>
/// <para>
/// These arms assert the FORMATTER's contract — a failure result plus a recorded model error — and
/// deliberately not an HTTP status code. A host may install an exception handler that maps
/// <c>ApiException</c>, so asserting "the response is 400" would be asserting something the framework
/// does not by itself guarantee.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DispatchInputFormatterMalformedBodyShould : UnitTestBase
{
	/// <summary>
	/// SAFETY: malformed input must not throw out of the formatter.
	/// </summary>
	[Fact]
	public async Task NotLetASerializationFailureEscape()
	{
		var context = BuildContext("{ this is not json ");

		InputFormatterResult? result = null;
		await Should.NotThrowAsync(
			async () => result = await CreateFormatter().ReadRequestBodyAsync(context));

		result.ShouldNotBeNull();
		result!.HasError.ShouldBeTrue("a body the serializer cannot read is a failed read");
	}

	/// <summary>
	/// SAFETY: the failure must be DIAGNOSABLE — a bare failure with no model error gives the caller a
	/// 400 with an empty problem detail and nothing to act on.
	/// </summary>
	[Fact]
	public async Task RecordTheFailureInModelState()
	{
		var context = BuildContext("{ this is not json ");

		_ = await CreateFormatter().ReadRequestBodyAsync(context);

		context.ModelState.ErrorCount.ShouldBeGreaterThan(
			0,
			"the formatter must tell the model binder WHY the body was rejected");
		context.ModelState.IsValid.ShouldBeFalse();
	}

	/// <summary>
	/// LIVENESS: a WELL-FORMED body still deserializes. Without this the arms above are satisfied by a
	/// formatter that rejects every request.
	/// </summary>
	[Fact]
	public async Task StillReadAWellFormedBody()
	{
		var context = BuildContext("""{"name":"ok"}"""); // camelCase — the serializer sets PropertyNamingPolicy.CamelCase

		var result = await CreateFormatter().ReadRequestBodyAsync(context);

		result.HasError.ShouldBeFalse("a valid body must still be accepted");
		_ = result.Model.ShouldBeOfType<Payload>();
		((Payload)result.Model!).Name.ShouldBe("ok");
	}

	/// <summary>
	/// SAFETY: a GENUINE fault must still escape. The catch is for malformed input, not a blanket
	/// "nothing ever throws out of this formatter".
	/// </summary>
	/// <remarks>
	/// Without this arm, widening the catch to <c>catch (Exception)</c> — the obvious "make it robust"
	/// edit — would silently convert a broken serializer, a disposed dependency or a cancelled request
	/// into a 400 blaming the caller for a body that was fine. The acceptance criteria calls this out
	/// explicitly: preserve cancellation and genuine server faults rather than converting every
	/// exception to a client error.
	/// </remarks>
	[Fact]
	public async Task LetAGenuineFaultEscape()
	{
		var formatter = new DispatchInputFormatter(
			new SingleSerializerRegistry(new ThrowingSerializer(new InvalidOperationException("the store is down"))));

		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await formatter.ReadRequestBodyAsync(BuildContext("""{"name":"ok"}""")));
	}

	/// <summary>
	/// SAFETY: cancellation stays cancellation. It is not a client error and must not be reported as one.
	/// </summary>
	[Fact]
	public async Task LetCancellationEscape()
	{
		var formatter = new DispatchInputFormatter(
			new SingleSerializerRegistry(new ThrowingSerializer(new OperationCanceledException())));

		_ = await Should.ThrowAsync<OperationCanceledException>(
			async () => await formatter.ReadRequestBodyAsync(BuildContext("""{"name":"ok"}""")));
	}

	/// <summary>
	/// SAFETY: the recorded model error must not carry the serializer's own message to the client.
	/// </summary>
	/// <remarks>
	/// The formatter records the EXCEPTION rather than a string, which is what ASP.NET Core's own
	/// formatters do: MVC then surfaces a generic message and keeps the exception for logging, so
	/// internal detail — type names, offsets, fragments of the payload — never reaches the response.
	/// Asserting the empty <c>ErrorMessage</c> is asserting exactly that mechanism; a future edit to
	/// <c>TryAddModelError(name, ex.Message)</c> would look like an improvement and would start leaking.
	/// </remarks>
	[Fact]
	public async Task NotLeakSerializerDetailIntoTheModelError()
	{
		var context = BuildContext("{ this is not json ");

		_ = await CreateFormatter().ReadRequestBodyAsync(context);

		var error = context.ModelState[context.ModelName]!.Errors.ShouldHaveSingleItem();

		error.ErrorMessage.ShouldBeEmpty(
			"MVC substitutes a safe generic message only while the error carries the EXCEPTION; putting "
			+ "the serializer's own text here would ship parser internals to the caller");
		error.Exception.ShouldNotBeNull("the detail must still be available to logging");
	}

	#region Helpers

	/// <summary>
	/// A serializer whose read always throws the supplied exception, for the arms that prove which
	/// failures are translated and which are left alone.
	/// </summary>
	private sealed class ThrowingSerializer(Exception toThrow) : ISerializer
	{
		public string Name => "Throwing";

		public string Version => "1.0.0";

		public string ContentType => "application/json";

		public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter) => throw toThrow;

		public T Deserialize<T>(ReadOnlySpan<byte> data) => throw toThrow;

		public byte[] SerializeObject(object value, Type type) => throw toThrow;

		public object DeserializeObject(ReadOnlySpan<byte> data, Type type) => throw toThrow;
	}

	private static DispatchInputFormatter CreateFormatter() =>
		new(new SingleSerializerRegistry(new SystemTextJsonSerializer()));

	/// <summary>
	/// Implements <see cref="ISerializerRegistry" /> DIRECTLY rather than deriving from the in-box
	/// registry, which is internal to the core package. A from-scratch fixture also binds the
	/// INTERFACE's requirement rather than re-testing a first-party base.
	/// </summary>
	private sealed class SingleSerializerRegistry(ISerializer serializer) : ISerializerRegistry
	{
		public (byte Id, ISerializer Serializer) GetCurrent() => (1, serializer);

		public void Register(byte id, ISerializer s) => throw new NotSupportedException();

		public void SetCurrent(string serializerName) => throw new NotSupportedException();

		public ISerializer? GetById(byte id) => id == 1 ? serializer : null;

		public IReadOnlyCollection<(byte Id, string Name, ISerializer Serializer)> GetAll() =>
			[(1, serializer.Name, serializer)];
	}

	private static InputFormatterContext BuildContext(string body)
	{
		var httpContext = new DefaultHttpContext();
		httpContext.Request.ContentType = "application/json";
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

		var provider = new EmptyModelMetadataProvider();
		return new InputFormatterContext(
			httpContext,
			modelName: "payload",
			modelState: new ModelStateDictionary(),
			metadata: provider.GetMetadataForType(typeof(Payload)),
			readerFactory: (stream, encoding) => new StreamReader(stream, encoding));
	}

	private sealed class Payload
	{
		public string? Name { get; set; }
	}

	#endregion
}
