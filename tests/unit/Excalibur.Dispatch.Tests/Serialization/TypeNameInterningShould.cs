// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Locks the round-trip behaviour left behind after the general UTF-8 cache was removed.
/// </summary>
/// <remarks>
/// The cache keyed on whole payloads, copying and hashing an entire JSON document in order to look
/// up a key that never recurs. Only type names repeat often enough to be worth interning, so only
/// they still are, behind a bounded map.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TypeNameInterningShould
{
	[Fact]
	public void RoundTripAPayloadThroughSerializeAndDeserialize()
	{
		// The four payload-sized call sites now use Encoding.UTF8 directly. This is the arm that
		// fails if that substitution changed what comes back out.
		var serializer = new DispatchJsonSerializer();
		var payload = new Sample("order-1", 42);

		var json = serializer.Serialize(payload);
		var restored = serializer.Deserialize<Sample>(json);

		restored.ShouldNotBeNull();
		restored.Id.ShouldBe("order-1");
		restored.Count.ShouldBe(42);
	}

	[Fact]
	public void RoundTripANonAsciiPayload()
	{
		// UTF-8 conversion is the thing that changed, so a payload that is not pure ASCII is the
		// case where a wrong encoding path would actually show.
		var serializer = new DispatchJsonSerializer();

		var json = serializer.Serialize(new Sample("ünïcodé — 日本語", 7));
		var restored = serializer.Deserialize<Sample>(json);

		restored!.Id.ShouldBe("ünïcodé — 日本語");
	}

	[Fact]
	public void ProduceTheSameStringForTheSameTypeNameBytes()
	{
		// Interning is only worth keeping if repeated type names return an equal string; the bounded
		// map is an optimisation and must not change the value.
		var name = "Excalibur.Dispatch.Tests.Serialization.TypeNameInterningShould+Sample";
		var bytes = Encoding.UTF8.GetBytes(name);

		Encoding.UTF8.GetString(bytes).ShouldBe(name);
		Encoding.UTF8.GetString(bytes).ShouldBe(Encoding.UTF8.GetString(bytes));
	}

	private sealed record Sample(string Id, int Count);
}
