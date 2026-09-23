// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;

using Excalibur.Dispatch.Hosting.AspNetCore.ContentNegotiation;
using Excalibur.Dispatch.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests.ContentNegotiation;

/// <summary>
/// Locks the ORDER-INDEPENDENCE guarantee that <c>AddDispatchContentNegotiation</c> states in its own
/// XML documentation: a serializer registered AFTER the call is still negotiable.
/// </summary>
/// <remarks>
/// <para>
/// This is the third of the three outcomes of the original defect, and the only one the sibling
/// registry-identity arms do not reach. <c>AddDispatchContentNegotiation</c> used to call
/// <c>builder.Services.BuildServiceProvider()</c> inside its options callback, which SNAPSHOTS the
/// service collection at the moment of the call. A serializer registered afterwards was therefore
/// absent from the formatters with no error and no log — a silent 415 for a content type the
/// application believed it supported.
/// </para>
/// <para>
/// Reference identity (the sibling arms) and order-independence are NOT the same property. A fix could
/// hand the formatters the application's registry instance and still build them too early; identity
/// would pass and this would fail. This arm exists because the XML doc makes order-independence a
/// PROMISE to consumers — <c>"Registration ORDER does not matter"</c> — and a documented guarantee with
/// no enforcing test is unverified rather than kept.
/// </para>
/// <para>
/// Both arms assert on <c>SupportedMediaTypes</c> because that collection is what ASP.NET Core's
/// content negotiation actually consults; both formatters populate it from the registry in their
/// constructors, so it is the observable consequence of the registry the formatter was handed.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DispatchContentNegotiationLateRegistrationShould : UnitTestBase
{
	private const string LateContentType = "application/vnd.excalibur-late";
	private const string EarlyContentType = "application/vnd.excalibur-early";

	/// <summary>
	/// SAFETY: this is the arm that binds the documented promise. RED under the old
	/// <c>BuildServiceProvider</c> implementation, which could not see a registration made after it ran.
	/// </summary>
	[Fact]
	public void NegotiateASerializerRegisteredAfterTheCall()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();

		// The call under test runs FIRST ...
		_ = services.AddControllers().AddDispatchContentNegotiation();

		// ... and the serializer arrives AFTER it, which is the case the XML doc promises works.
		_ = services.AddPluggableSerializer(200, new StubSerializer("Late", LateContentType));

		using var provider = services.BuildServiceProvider();

		OutputMediaTypes(provider).ShouldContain(
			LateContentType,
			"AddDispatchContentNegotiation documents that registration ORDER does not matter, so a "
			+ "serializer registered after it must still be negotiable; a snapshot of the service "
			+ "collection taken during the call cannot see this registration");

		InputMediaTypes(provider).ShouldContain(LateContentType);
	}

	/// <summary>
	/// NON-VACUOUS CONTROL: the ordinary serializers-first order still works.
	/// </summary>
	/// <remarks>
	/// Without this, the arm above is satisfied by an implementation that is simply late about
	/// everything — including a regression that broke the normal registration order nobody would
	/// otherwise notice. It also proves the fixture itself can put a media type on a formatter, so a
	/// failure above is about ordering rather than about a stub the pipeline never accepted.
	/// </remarks>
	[Fact]
	public void StillNegotiateASerializerRegisteredBeforeTheCall()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();
		_ = services.AddPluggableSerializer(201, new StubSerializer("Early", EarlyContentType));

		_ = services.AddControllers().AddDispatchContentNegotiation();

		using var provider = services.BuildServiceProvider();

		OutputMediaTypes(provider).ShouldContain(EarlyContentType);
		InputMediaTypes(provider).ShouldContain(EarlyContentType);
	}

	#region Helpers

	private static IEnumerable<string> OutputMediaTypes(IServiceProvider provider) =>
		provider.GetRequiredService<IOptions<MvcOptions>>().Value.OutputFormatters
			.OfType<DispatchOutputFormatter>()
			.ShouldHaveSingleItem()
			.SupportedMediaTypes;

	private static IEnumerable<string> InputMediaTypes(IServiceProvider provider) =>
		provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
			.OfType<DispatchInputFormatter>()
			.ShouldHaveSingleItem()
			.SupportedMediaTypes;

	/// <summary>
	/// A serializer that exists only to contribute a DISTINCTIVE content type. The arms assert on media
	/// type discovery, never on payload bytes, so the serialization members are deliberately unreachable
	/// — if one is ever called, that is a different test making a wrong assumption and it should fail
	/// loudly rather than return a plausible empty value.
	/// </summary>
	private sealed class StubSerializer(string name, string contentType) : ISerializer
	{
		public string Name => name;

		public string Version => "1.0.0";

		public string ContentType => contentType;

		public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter) => throw new NotSupportedException();

		public T Deserialize<T>(ReadOnlySpan<byte> data) => throw new NotSupportedException();

		public byte[] SerializeObject(object value, Type type) => throw new NotSupportedException();

		public object DeserializeObject(ReadOnlySpan<byte> data, Type type) => throw new NotSupportedException();
	}

	#endregion
}
