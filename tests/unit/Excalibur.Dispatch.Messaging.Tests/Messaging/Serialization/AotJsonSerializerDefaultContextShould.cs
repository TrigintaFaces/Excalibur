// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Messaging.Tests.Messaging.Serialization;

/// <summary>Preserves the closed core-policy cycle refusal and byte parity across owned/context routes.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class AotJsonSerializerDefaultContextShould
{
	/// <summary>
	/// SAFETY: a cycle must be refused, not silently written as a null, on BOTH routes.
	/// </summary>
	/// <remarks>
	/// The deleted option set carried <c>ReferenceHandler.IgnoreCycles</c>, which replaced a cycle with a
	/// null -- the message serialized "successfully" having lost a field, and nothing downstream could
	/// tell. A throw is loud and recoverable, which is why the convergence drops that setting rather than
	/// adopting it.
	/// </remarks>
	[Fact]
	public void RefuseACycleOnBothConstructionRoutes()
	{
		var cyclic = new Dictionary<string, object>(StringComparer.Ordinal);
		cyclic["self"] = cyclic;

		var byDefaultFallback = SystemTextJsonSerializer.CreateWithMetadata(new JsonSerializerOptions(CoreMessageJsonContext.Default.Options) { TypeInfoResolver = CoreMessageJsonContext.Default });
		var byExplicitContext = SystemTextJsonSerializer.Create(CoreMessageJsonContext.Default);

		_ = Should.Throw<SerializationException>(
			() => byDefaultFallback.SerializeToBytes(cyclic),
			"the parameterless route used to carry ReferenceHandler.IgnoreCycles, so it wrote a null for "
			+ "the cycle and reported success. A field vanishing with no error is the silent class.").InnerException.ShouldBeOfType<JsonException>();

		_ = Should.Throw<SerializationException>(
			() => byExplicitContext.SerializeToBytes(cyclic),
			"the explicitly-supplied context is the route that already refused a cycle. It is asserted "
			+ "here so the arm fails if convergence is achieved by LOOSENING this route to match the "
			+ "other one, which would be the wrong direction.").InnerException.ShouldBeOfType<JsonException>();
	}

	/// <summary>
	/// LIVENESS: an ordinary message still round-trips on both routes.
	/// </summary>
	/// <remarks>
	/// Without this arm, the arm above is satisfied by a serializer that refuses everything -- which is
	/// the cheapest way to pass a safety assertion and the most expensive way to be wrong.
	/// </remarks>
	[Fact]
	public void StillRoundTripAnOrdinaryMessageOnBothConstructionRoutes()
	{
		var payload = new Dictionary<string, object>(StringComparer.Ordinal) { ["name"] = "value" };

		var byDefaultFallback = SystemTextJsonSerializer.CreateWithMetadata(new JsonSerializerOptions(CoreMessageJsonContext.Default.Options) { TypeInfoResolver = CoreMessageJsonContext.Default });
		var byExplicitContext = SystemTextJsonSerializer.Create(CoreMessageJsonContext.Default);

		var fromFallback = byDefaultFallback.SerializeToBytes(payload);
		var fromExplicit = byExplicitContext.SerializeToBytes(payload);

		fromFallback.ShouldBe(
			fromExplicit,
			"the two routes must produce byte-identical output for the same message. A difference here is "
			+ "a wire-format divergence decided by which constructor overload the consumer happened to call.");

		_ = byDefaultFallback.Deserialize<Dictionary<string, object>>(fromFallback).ShouldNotBeNull();
	}
}
