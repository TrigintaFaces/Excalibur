// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// wpynky security lock (author&#8800;impl): event-type resolution is allow-list-bounded /
/// secure-by-default. An unregistered type name — including a real, loaded, scannable type that an
/// attacker could name to drive a gadget chain — MUST be rejected with
/// <see cref="UnknownEventTypeException"/> rather than resolved by an unbounded assembly scan. The
/// reflection assembly scan is available only as an explicit, trusted-environment opt-in.
/// </summary>
/// <remarks>
/// <para><b>Non-vacuity / RED-proof:</b> the load-bearing assertions resolve a name that the unbounded
/// <c>AppDomain.GetAssemblies()</c> scan <i>would</i> resolve (e.g. <c>typeof(string)</c>'s
/// assembly-qualified name). If a default scan / <c>Type.GetType</c> fallback were reintroduced, those
/// names would resolve and these tests would go RED — proving the lock binds the secure default, not an
/// always-throwing path. (A genuinely non-existent name would throw under either design and is therefore
/// vacuous as a security lock.)</para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed partial class EventSerializerUnregisteredTypeRejectionShould
{
	// ---------------------------------------------------------------------------------------------
	// JsonEventSerializer — the reflection serializer: assembly scan is OFF by default (the fix).
	// ---------------------------------------------------------------------------------------------

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void JsonEventSerializer_RejectScannableUnregisteredType_WhenScanDisabledByDefault()
	{
		// Arrange — default constructor => allowAssemblyScan == false.
		var serializer = new JsonEventSerializer();

		// typeof(string) is loaded and WOULD be found by the unbounded scan — rejection here is
		// by-policy, not by-absence. This is the non-vacuous RED-proof anchor for wpynky.
		var scannableButUnregistered = typeof(string).AssemblyQualifiedName!;

		// Act & Assert — secure default rejects it instead of resolving via the gadget-chain scan.
		Should.Throw<UnknownEventTypeException>(() => serializer.ResolveType(scannableButUnregistered));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void JsonEventSerializer_RejectUnknownType_WhenScanDisabledByDefault()
	{
		// Arrange
		var serializer = new JsonEventSerializer();

		// Act & Assert
		Should.Throw<UnknownEventTypeException>(
			() => serializer.ResolveType("Definitely.Not.A.Real.Type, No.Such.Assembly"));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void JsonEventSerializer_ResolveScannableType_WhenScanExplicitlyEnabled()
	{
		// Arrange — explicit, trusted-environment opt-in restores the reflection assembly scan.
		var serializer = new JsonEventSerializer(allowAssemblyScan: true);

		// Act
		var resolved = serializer.ResolveType(typeof(string).AssemblyQualifiedName!);

		// Assert — the opt-in escape hatch still resolves a real loaded type.
		resolved.ShouldBe(typeof(string));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void JsonEventSerializer_RejectGenuinelyUnknownType_EvenWhenScanEnabled()
	{
		// Arrange — even with the scan enabled, a name no loaded assembly exposes is rejected.
		var serializer = new JsonEventSerializer(allowAssemblyScan: true);

		// Act & Assert
		Should.Throw<UnknownEventTypeException>(
			() => serializer.ResolveType("Definitely.Not.A.Real.Type, No.Such.Assembly"));
	}

	// ---------------------------------------------------------------------------------------------
	// AotJsonEventSerializer — registry-only resolution: a registry miss throws (never scans).
	// ---------------------------------------------------------------------------------------------

	[Fact]
	public void AotJsonEventSerializer_RejectScannableUnregisteredType()
	{
		// Arrange — empty registry; typeof(string) is loaded/scannable but NOT registered.
		var sut = new AotJsonEventSerializer(new EmptyEventTypeRegistry(), new RejectionJsonContext());

		// Act & Assert — resolution is allow-list-bounded; no fallback scan.
		Should.Throw<UnknownEventTypeException>(
			() => sut.ResolveType(typeof(string).AssemblyQualifiedName!));
	}

	[Fact]
	public void AotJsonEventSerializer_RejectUnknownType()
	{
		// Arrange
		var sut = new AotJsonEventSerializer(new EmptyEventTypeRegistry(), new RejectionJsonContext());

		// Act & Assert
		Should.Throw<UnknownEventTypeException>(() => sut.ResolveType("NonExistent.Event"));
	}

	// --- Test helpers ---

	/// <summary>An event-type registry that resolves nothing (every name is unregistered).</summary>
	private sealed class EmptyEventTypeRegistry : IEventTypeRegistry
	{
		public Type? ResolveType(string eventTypeName) => null;

		public string? GetTypeName(Type eventType) => null;
	}

	[JsonSerializable(typeof(object))]
	private sealed partial class RejectionJsonContext : JsonSerializerContext;
}
