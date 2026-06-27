// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.TypeResolution;

namespace Excalibur.Dispatch.Tests.TypeResolution;

/// <summary>
/// Author≠impl regression lock for S852 · <c>6v2z7q</c> (HEADLINE security) — <see cref="TypeResolver"/>'s
/// unbounded JIT assembly scan (the gadget-chain vector for an untrusted type name) is <b>OFF by default</b>:
/// the secure default resolves only registry-registered types; the scan is a per-call <c>allowAssemblyScan</c>
/// opt-in for trusted callers only. Mirrors the shipped c6wd6f <c>JsonEventSerializer</c> secure default.
/// </summary>
/// <remarks>
/// Authored independently of the implementer (BackendDeveloper) against the committed seam
/// (<c>ResolveType(name, bool allowAssemblyScan = false)</c>, scan gated on the flag, registry-first unchanged).
/// <b>Non-vacuity:</b> <see cref="UntrustedScannableType_ResolvesNull_WhenScanOffByDefault"/> (null) and
/// <see cref="TrustedOptIn_ScansAndResolves_WhenExplicitlyEnabled"/> (resolves) use the <em>same</em> loaded,
/// scannable, unregistered type, differing only by the flag — so they bind the scan-gate as load-bearing:
/// removing the <c>if (!allowAssemblyScan) return null</c> gate (the pre-6v2z7q unconditional scan) makes the
/// untrusted case resolve the attacker-chosen type ⇒ RED.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TypeResolverScanGateShould
{
	// A real, loaded, scannable type that is NOT registered — rejection scan-off is by-policy, not by-absence.
	private static readonly string ScannableUnregisteredName = typeof(string).AssemblyQualifiedName!;

	[Fact]
	[RequiresDynamicCode("Exercises the JIT assembly-scan fallback path")]
	public void UntrustedScannableType_ResolvesNull_WhenScanOffByDefault()
	{
		// Default allowAssemblyScan:false ⇒ the unbounded scan is gated off ⇒ an unregistered (attacker-chosen)
		// type name resolves to null even though it IS loaded/scannable. The gadget-chain vector is closed.
		TypeResolver.ResolveType(ScannableUnregisteredName).ShouldBeNull();
	}

	[Fact]
	[RequiresDynamicCode("Exercises the JIT assembly-scan fallback path")]
	public void TrustedOptIn_ScansAndResolves_WhenExplicitlyEnabled()
	{
		// A trusted caller (e.g. SerializerMigrationService on the consumer's own store) opts in ⇒ the JIT scan
		// still resolves a real loaded type. Preserves the migration/CloudEvent callers.
		TypeResolver.ResolveType(ScannableUnregisteredName, allowAssemblyScan: true).ShouldBe(typeof(string));
	}

	[Fact]
	public void RegisteredType_Resolves_ScanOff()
	{
		// Registry-first: a registered name resolves WITHOUT any scan (secure AND functional).
		const string registeredName = "Test.SixV2z7q.RegisteredMarker";
		var resolver = new FixedTypeResolver(registeredName, typeof(RegisteredMarker));
		TypeResolverRegistry.Register(resolver);
		try
		{
			TypeResolver.ResolveType(registeredName).ShouldBe(typeof(RegisteredMarker));
		}
		finally
		{
			TypeResolverRegistry.Clear(); // isolate: static registry must not leak to other tests
		}
	}

	[Fact]
	[RequiresDynamicCode("Exercises the JIT assembly-scan fallback path")]
	public void ResolveTypeRequired_Throws_OnUntrustedUnregistered_ScanOff()
	{
		// The throwing variant is secure too: an unregistered name scan-off cannot be resolved ⇒ TypeLoadException,
		// never a silently scan-resolved attacker type.
		Should.Throw<TypeLoadException>(() => TypeResolver.ResolveTypeRequired(ScannableUnregisteredName));
	}

	private sealed class RegisteredMarker;

	private sealed class FixedTypeResolver(string name, Type type) : ITypeResolver
	{
		public bool TryGetType(string typeName, [NotNullWhen(true)] out Type? resolved)
		{
			if (string.Equals(typeName, name, StringComparison.Ordinal))
			{
				resolved = type;
				return true;
			}

			resolved = null;
			return false;
		}
	}
}
