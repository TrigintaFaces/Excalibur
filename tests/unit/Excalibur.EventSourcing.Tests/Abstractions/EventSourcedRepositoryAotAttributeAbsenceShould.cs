// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Sprint 847 / Lane B (bead ul45wp) — author≠impl regression lock asserting the consumer-facing
/// <see cref="IEventSourcedRepository{TAggregate, TKey}"/> interface (and its string-key derivative)
/// carry NO AOT/trimmer-hostile attributes (MS-B).
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule:</b> <c>.claude/rules/quality/microsoft-first.md</c> — interfaces NEVER carry AOT attributes.
/// On the true pre-fix HEAD (<c>301b4aa62</c>) <c>GetByIdAsync</c> and both <c>SaveAsync</c> overloads
/// carried <see cref="RequiresUnreferencedCodeAttribute"/> and <see cref="RequiresDynamicCodeAttribute"/>,
/// so a consumer calling the write-side contract through the interface inherited IL2026/IL3050.
/// </para>
/// <para>
/// <b>Fix (FR-B1):</b> no member of the public interface carries either attribute; any reflection-based
/// rehydration/persistence lives behind an AOT-safe seam with the annotation on the internal impl only
/// (FR-B3).
/// </para>
/// <para>
/// <b>Non-vacuity:</b> driven off the real member symbols via reflection — RED on the pre-fix HEAD
/// (attributes present), GREEN after removal. Exhaustive (NO member may carry the attributes), which also
/// covers the already-clean <c>ExistsAsync</c>/<c>DeleteAsync</c> members (EC-B2) and the string-key
/// derivative <c>IEventSourcedRepository&lt;TAggregate&gt;</c> acquiring no transitive requirement (EC-B1).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Feature", "AOT")]
public sealed class EventSourcedRepositoryAotAttributeAbsenceShould
{
	[Theory]
	[MemberData(nameof(RepositoryInterfaces))]
	public void HaveNoRequiresUnreferencedCodeAttributeOnAnyInterfaceMember(Type interfaceType)
	{
		foreach (var method in interfaceType.GetMethods())
		{
			method.GetCustomAttributes(typeof(RequiresUnreferencedCodeAttribute), inherit: false)
				.ShouldBeEmpty(
					$"{interfaceType.Name}.{method.Name} is a consumer-facing interface member and MUST NOT " +
					"carry [RequiresUnreferencedCode] (microsoft-first AOT rule).");
		}
	}

	[Theory]
	[MemberData(nameof(RepositoryInterfaces))]
	public void HaveNoRequiresDynamicCodeAttributeOnAnyInterfaceMember(Type interfaceType)
	{
		foreach (var method in interfaceType.GetMethods())
		{
			method.GetCustomAttributes(typeof(RequiresDynamicCodeAttribute), inherit: false)
				.ShouldBeEmpty(
					$"{interfaceType.Name}.{method.Name} is a consumer-facing interface member and MUST NOT " +
					"carry [RequiresDynamicCode] (microsoft-first AOT rule).");
		}
	}

	public static TheoryData<Type> RepositoryInterfaces() =>
		new()
		{
			typeof(IEventSourcedRepository<,>),
			typeof(IEventSourcedRepository<>),
		};
}
