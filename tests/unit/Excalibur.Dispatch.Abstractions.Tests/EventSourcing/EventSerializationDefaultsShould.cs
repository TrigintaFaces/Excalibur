// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// Regression lock for the frozen canonical serializer singleton.
/// </summary>
/// <remarks>
/// Non-vacuous: RED against a <c>CreateFrozenCanonicalOptions</c> that calls the parameterless
/// <see cref="JsonSerializerOptions.MakeReadOnly()"/> — that overload throws
/// <see cref="InvalidOperationException"/> ("no TypeInfoResolver specified") from the static constructor,
/// so every access to <see cref="EventSerializationDefaults.Canonical"/> surfaces a
/// <see cref="TypeInitializationException"/> at runtime (build stays green — a cctor failure is runtime-only).
/// This lock actually TOUCHES the singleton, so it fails the moment the canonical serializer is inert.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSerializationDefaultsShould : UnitTestBase
{
	[Fact]
	public void Canonical_IsConstructableAndFrozen()
	{
		// Act — merely accessing the property runs the static cctor + MakeReadOnly.
		var options = EventSerializationDefaults.Canonical;

		// Assert — the singleton exists and is frozen (safe to share without defensive copying).
		_ = options.ShouldNotBeNull();
		options.IsReadOnly.ShouldBeTrue("the canonical options must be frozen so they can be shared safely");
		// NOTE: the reflection-DISABLED (AOT/trimmed) construction failure is locked separately in the
		// integration suite (EventSerializationDefaultsCanonicalConstructionShould) — this reflection-enabled
		// unit project cannot reproduce it. Here we lock the emitted event contract below.
	}

	[Fact]
	[RequiresDynamicCode("Canonical options use the reflection-based serialization path.")]
	public void Canonical_HonoursTheEventContract_CamelCaseAndStringEnums()
	{
		// Act — round-trip a value through the real canonical singleton.
		var json = JsonSerializer.Serialize(
			new Sample(Kind.SecondValue, RemovedWhenNull: null),
			EventSerializationDefaults.Canonical);

		// Assert — the canonical event contract: camelCase property names, enums as strings, nulls omitted.
		// (camelCase properties + enum-as-string; null values omitted via WhenWritingNull.)
		json.ShouldContain("\"kind\":\"secondValue\"");
		json.ShouldNotContain("removedWhenNull");
	}

	private sealed record Sample(Kind Kind, string? RemovedWhenNull);

	[JsonConverter(typeof(JsonStringEnumConverter))]
	private enum Kind
	{
		FirstValue = 0,
		SecondValue = 1,
	}
}
