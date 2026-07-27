// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Middleware.Validation;

namespace Excalibur.Dispatch.Middleware.Tests;

/// <summary>
/// Policy conformance guard (nyb2jo / S899): a security middleware's message-kind applicability must be
/// stated consistently and must not be silently narrower than the family it protects.
/// </summary>
/// <remarks>
/// <para>
/// Two structural invariants, both made RED-detectable rather than left to review:
/// </para>
/// <list type="number">
/// <item>
/// <b>[AppliesTo] must not disagree with the property.</b> The runtime pipeline resolves applicability from
/// each middleware's <see cref="IDispatchMiddleware.ApplicableMessageKinds"/> property (the single source of
/// truth), never from the <c>[AppliesTo]</c> attribute directly. If a middleware's attribute and property
/// disagree, the attribute is a lie: it advertises one scope while the pipeline applies another. This arm
/// enumerates every concrete framework middleware carrying <c>[AppliesTo]</c> and binds the two together.
/// </item>
/// <item>
/// <b>A security middleware must not be silently narrower than its family.</b> Authentication, authorization,
/// validation, input-sanitization and tenant-identity all protect both <see cref="MessageKinds.Action"/> and
/// <see cref="MessageKinds.Event"/> dispatches; a security control that quietly applies to only one of them
/// lets the other bypass it (the S899 / 4yasc6 class). This arm requires each to cover both kinds.
/// </item>
/// </list>
/// <para>
/// The <see cref="IDispatchMiddleware.ApplicableMessageKinds"/> values under test are constant expressions, so
/// they are read from an uninitialized instance (<see cref="RuntimeHelpers.GetUninitializedObject"/>) without
/// constructing the middleware's dependencies — the constant does not touch any constructor-assigned state.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SecurityMiddlewareApplicabilityPolicyShould
{
	/// <summary>
	/// The security-enforcing family: controls whose omission for a message kind is a security hole, so they
	/// must apply to every kind the family protects (Action and Event).
	/// </summary>
	private static readonly Type[] SecurityFamily =
	[
		typeof(AuthenticationMiddleware),
		typeof(AuthorizationMiddleware),
		typeof(ValidationMiddleware),
		typeof(InputSanitizationMiddleware),
		typeof(TenantIdentityMiddleware),
	];

	/// <summary>
	/// ARM 1 — every framework middleware carrying <c>[AppliesTo]</c> must declare the SAME message kinds on
	/// its <see cref="IDispatchMiddleware.ApplicableMessageKinds"/> property (the production source of truth).
	/// RED if any attribute disagrees with its property.
	/// </summary>
	[Fact]
	public void HaveAppliesToAttributeThatAgreesWithTheApplicableMessageKindsProperty()
	{
		var offenders = new List<string>();

		foreach (var type in MiddlewareTypesWithAppliesTo())
		{
			var attributeKinds = type.GetCustomAttribute<AppliesToAttribute>()!.MessageKinds;
			var propertyKinds = ((IDispatchMiddleware)RuntimeHelpers.GetUninitializedObject(type)).ApplicableMessageKinds;

			if (attributeKinds != propertyKinds)
			{
				offenders.Add($"{type.Name}: [AppliesTo]={attributeKinds} but ApplicableMessageKinds={propertyKinds}");
			}
		}

		offenders.ShouldBeEmpty(
			"a middleware's [AppliesTo] attribute must equal its ApplicableMessageKinds property — the pipeline "
			+ "resolves applicability from the property, so a disagreement silently changes which kinds the "
			+ "middleware applies to:\n" + string.Join("\n", offenders));
	}

	/// <summary>
	/// Non-vacuity guard for ARM 1: the reflection enumeration must actually discover middleware (otherwise the
	/// agreement arm passes over an empty set). Binds the enumeration to the known security family.
	/// </summary>
	[Fact]
	public void EnumerateAtLeastTheKnownSecurityMiddleware()
	{
		var discovered = MiddlewareTypesWithAppliesTo()
			.Select(t => t.Name)
			.ToHashSet(StringComparer.Ordinal);

		foreach (var securityType in SecurityFamily)
		{
			discovered.ShouldContain(
				securityType.Name,
				$"{securityType.Name} carries [AppliesTo] and must appear in the enumerated middleware set — "
				+ "if the enumeration misses it, the agreement arm is vacuous.");
		}
	}

	/// <summary>
	/// ARM 2 — a security-enforcing middleware must apply to BOTH <see cref="MessageKinds.Action"/> and
	/// <see cref="MessageKinds.Event"/>. RED if one (e.g. authorization) is silently narrowed to a single kind,
	/// letting the other bypass the control.
	/// </summary>
	[Theory]
	[MemberData(nameof(SecurityFamilyData))]
	public void ApplyToBothActionAndEvent_ForEachSecurityMiddleware(Type middlewareType)
	{
		var kinds = ((IDispatchMiddleware)RuntimeHelpers.GetUninitializedObject(middlewareType)).ApplicableMessageKinds;

		const MessageKinds Family = MessageKinds.Action | MessageKinds.Event;

		(kinds & Family).ShouldBe(
			Family,
			$"{middlewareType.Name} is a security-enforcing middleware and must apply to BOTH Action and Event "
			+ $"(the kinds its family protects), but applies to {kinds} — a narrower scope silently bypasses it "
			+ "for the missing kind.");
	}

	public static IEnumerable<object[]> SecurityFamilyData() => SecurityFamily.Select(t => new object[] { t });

	private static IEnumerable<Type> MiddlewareTypesWithAppliesTo() =>
		typeof(AuthorizationMiddleware).Assembly
			.GetTypes()
			.Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
				&& typeof(IDispatchMiddleware).IsAssignableFrom(t)
				&& t.GetCustomAttribute<AppliesToAttribute>() is not null);
}
