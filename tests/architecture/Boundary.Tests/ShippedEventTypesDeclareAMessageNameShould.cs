// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;

namespace Boundary.Tests;

/// <summary>
/// Every event type the framework itself ships declares a message name, so no consumer can be handed an
/// <see cref="InvalidOperationException"/> from <see cref="MessageNameHelper.GetName(Type)"/> about an
/// attribute they cannot add because the type is ours.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MessageNameHelper.GetName(Type)"/> has no fallback by design -- a derived name would embed
/// the namespace and assembly version. That is correct for a consumer's own events, which they can
/// annotate. It is a defect for a type we ship and dispatch on their behalf: the CloudEvents conversion,
/// the event serializers and the outbox all call <c>GetName</c>, and the consumer has no seam to fix it.
/// </para>
/// <para>
/// Open generic definitions are included, not skipped. A definition cannot be dispatched, so a scan of
/// closed types alone cannot see it -- and the construction that IS dispatched exists only in consumer
/// code, where no guard of ours runs. Requiring the declaration at the definition is the only place the
/// omission is visible to us at all.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> <see cref="FlagAnEventTypeThatDeclaresNoName"/> runs the same predicate over a
/// deliberately unnamed type and requires a hit, so a green here cannot come from a scan that examines
/// nothing or from a predicate that can never match.
/// </para>
/// </remarks>
[Trait("Category", "Architecture")]
[Trait("Component", "Core")]
public sealed class ShippedEventTypesDeclareAMessageNameShould
{
	private sealed record UnnamedProbeEvent : IDispatchEvent;

	[MessageName("Excalibur.Dispatch.NamedProbeEvent")]
	private sealed record NamedProbeEvent : IDispatchEvent;

	[MessageName("Excalibur.Dispatch.GenericProbeEvent")]
	private sealed record GenericProbeEvent<T> : IDispatchEvent;

	[Fact]
	public void DeclareANameOnEveryShippedEventType()
	{
		var shipped = ShippedEventTypes().ToList();

		// Scope anchor: a named type from each of the two assemblies this guard exists for. Without it a
		// green could come from a run where the framework assemblies never loaded and the scan saw nothing.
		shipped.ShouldContain(typeof(Excalibur.Dispatch.Transport.CronTimerTriggerMessage));
		shipped.ShouldContain(typeof(Excalibur.Dispatch.CloudEvents.CloudEventMessage));
		shipped.ShouldContain(
			typeof(Excalibur.Dispatch.Transport.CronTimerTriggerMessage<>),
			"open generic definitions are in scope -- the closed form only ever exists in consumer code");

		var undeclared = shipped.Where(IsUndeclared).Select(static t => t.FullName).Order(StringComparer.Ordinal);

		string.Join(Environment.NewLine, undeclared).ShouldBeEmpty(
			"a shipped event type without [MessageName] throws from MessageNameHelper.GetName the first "
			+ "time a consumer serializes, routes or CloudEvent-converts it, and they cannot add the "
			+ "attribute to a type we own");
	}

	[Fact]
	public void FlagAnEventTypeThatDeclaresNoName()
	{
		// Liveness. Same predicate, a type known to be unnamed -- so the empty set above is caused by the
		// attributes being present and by nothing else.
		IsUndeclared(typeof(UnnamedProbeEvent)).ShouldBeTrue();
		IsUndeclared(typeof(NamedProbeEvent)).ShouldBeFalse();

		_ = Should.Throw<InvalidOperationException>(
			() => MessageNameHelper.GetName(typeof(UnnamedProbeEvent)));
	}

	[Fact]
	public void GiveEveryConstructionOfAGenericEventItsOwnName()
	{
		// What the guard above cannot reach: the definition declares once, and each construction composes
		// a distinct name from it. Two constructions sharing one name would resolve to neither.
		MessageNameHelper.GetName(typeof(GenericProbeEvent<int>))
			.ShouldBe("Excalibur.Dispatch.GenericProbeEventOfInt32");
		MessageNameHelper.GetName(typeof(GenericProbeEvent<NamedProbeEvent>))
			.ShouldBe("Excalibur.Dispatch.GenericProbeEventOfExcalibur.Dispatch.NamedProbeEvent");

		MessageNameHelper.GetName(typeof(GenericProbeEvent<int>))
			.ShouldNotBe(MessageNameHelper.GetName(typeof(GenericProbeEvent<long>)));

		// And the shipped one the ruling was about.
		MessageNameHelper.GetName(typeof(Excalibur.Dispatch.Transport.CronTimerTriggerMessage<ProbeTimer>))
			.ShouldBe("Excalibur.Dispatch.CronTimerTriggerMessageOfProbeTimer");
	}

	private readonly record struct ProbeTimer : Excalibur.Dispatch.Transport.ICronTimerMarker;

	private static bool IsUndeclared(Type type) => MessageNameHelper.GetDeclaredName(type) is null;

	private static IEnumerable<Type> ShippedEventTypes() =>
		AppDomain.CurrentDomain.GetAssemblies()
			.Where(static a => a.GetName().Name?.StartsWith("Excalibur", StringComparison.Ordinal) == true)
			.SelectMany(static a =>
			{
				try
				{
					return a.GetTypes();
				}
				catch (ReflectionTypeLoadException ex)
				{
					return Array.FindAll(ex.Types, static t => t is not null)!;
				}
			})
			.Where(static t =>
				t.IsPublic
				&& !t.IsAbstract
				&& !t.IsInterface
				&& typeof(IDispatchEvent).IsAssignableFrom(t));
}
