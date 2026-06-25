// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// Sprint 847 / Lane B (bead 7cc9tu) — author≠impl regression lock asserting the consumer-facing
/// <see cref="IEventSerializer"/> interface carries NO AOT/trimmer-hostile attributes (MS-B).
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule:</b> <c>.claude/rules/quality/microsoft-first.md</c> — "Consumer-facing APIs: NEVER
/// <c>[RequiresUnreferencedCode]</c>" and "Interfaces: NEVER AOT attributes." On the true pre-fix HEAD
/// (<c>301b4aa62</c>) <c>SerializeEvent</c>/<c>DeserializeEvent</c> carried both
/// <see cref="RequiresUnreferencedCodeAttribute"/> and <see cref="RequiresDynamicCodeAttribute"/>, and
/// <c>ResolveType</c> carried <see cref="RequiresUnreferencedCodeAttribute"/> — so every implementer
/// (including the AOT-safe <c>AotJsonEventSerializer</c>) inherited IL2026/IL3050 with no escape hatch.
/// </para>
/// <para>
/// <b>Fix (FR-B2):</b> no member of the public interface carries either attribute; any genuinely
/// reflection-based impl keeps the annotation on its internal concrete type only (FR-B3), not the
/// interface.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> this lock is driven off the real member symbols via reflection. It is RED on the
/// pre-fix HEAD (the attributes are present) and GREEN after their removal. It holds under either
/// SoftwareArchitect resolution (Option A remove / Option B refactor-then-remove) because both leave the
/// interface attribute-free. The assertion is intentionally exhaustive — NO interface member may carry
/// the attributes — which also covers the already-clean <c>GetTypeName</c> member (EC-B2).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Feature", "AOT")]
public sealed class EventSerializerAotAttributeAbsenceShould
{
	[Fact]
	public void HaveNoRequiresUnreferencedCodeAttributeOnAnyInterfaceMember()
	{
		foreach (var method in typeof(IEventSerializer).GetMethods())
		{
			method.GetCustomAttributes(typeof(RequiresUnreferencedCodeAttribute), inherit: false)
				.ShouldBeEmpty(
					$"IEventSerializer.{method.Name} is a consumer-facing interface member and MUST NOT " +
					"carry [RequiresUnreferencedCode] (microsoft-first AOT rule).");
		}
	}

	[Fact]
	public void HaveNoRequiresDynamicCodeAttributeOnAnyInterfaceMember()
	{
		foreach (var method in typeof(IEventSerializer).GetMethods())
		{
			method.GetCustomAttributes(typeof(RequiresDynamicCodeAttribute), inherit: false)
				.ShouldBeEmpty(
					$"IEventSerializer.{method.Name} is a consumer-facing interface member and MUST NOT " +
					"carry [RequiresDynamicCode] (microsoft-first AOT rule).");
		}
	}
}
