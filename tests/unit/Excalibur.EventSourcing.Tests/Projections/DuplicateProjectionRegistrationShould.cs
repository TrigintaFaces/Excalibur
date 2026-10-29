// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Two registrations for one projection type are refused, in whatever mode.
/// </summary>
/// <remarks>
/// <para>
/// Registering a projection type twice used to be a bare indexer assignment keyed by type, so the second
/// call silently displaced the first: a consumer asking for the same type both inline and async got one
/// of the two, with no error on either call order. The displacement was not symmetric either -- the
/// inline path also writes container descriptors that cannot be withdrawn, so the inline coordinator
/// stayed wired for a mode the registry no longer held.
/// </para>
/// <para>
/// The arms pair the refusals with the composition that must still WORK: a registry that refuses every
/// second call is satisfied by an application that can only ever hold one projection, which is a more
/// expensive failure than the one being fixed.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DuplicateProjectionRegistrationShould
{
	/// <summary>SAFETY. The incoherent cross-mode composition is refused and the message is actionable.</summary>
	[Fact]
	public void RefuseOneProjectionTypeRegisteredInTwoModes()
	{
		using var provider = Build(es =>
		{
			_ = es.AddProjection<OrderSummary>(p => p.Inline().When<TestOrderPlaced>((s, _) => s.EventCount++));
			_ = es.AddProjection<OrderSummary>(p => p.Async().When<TestOrderPlaced>((s, _) => s.EventCount++));
		});

		var message = Should.Throw<InvalidOperationException>(() => Populate(provider)).Message;

		message.ShouldContain(nameof(OrderSummary), Case.Sensitive);
		message.ShouldContain(nameof(ProjectionMode.Inline), Case.Sensitive);
		message.ShouldContain(nameof(ProjectionMode.Async), Case.Sensitive);
	}

	/// <summary>SAFETY. The reverse call order is refused too -- neither order is the supported one.</summary>
	[Fact]
	public void RefuseTheReverseCallOrderAsWell()
	{
		using var provider = Build(es =>
		{
			_ = es.AddProjection<OrderSummary>(p => p.Async().When<TestOrderPlaced>((s, _) => s.EventCount++));
			_ = es.AddProjection<OrderSummary>(p => p.Inline().When<TestOrderPlaced>((s, _) => s.EventCount++));
		});

		var message = Should.Throw<InvalidOperationException>(() => Populate(provider)).Message;

		message.ShouldContain(nameof(OrderSummary), Case.Sensitive);
		message.ShouldContain(nameof(ProjectionMode.Inline), Case.Sensitive);
		message.ShouldContain(nameof(ProjectionMode.Async), Case.Sensitive);
	}

	/// <summary>
	/// The boundary of the refusal, pinned so it is deliberate rather than incidental: a SAME-mode
	/// re-registration still replaces. It cannot be incoherent -- there is one mode and one cursor
	/// either way -- and it is relied on as a reconfiguration point, including by arms that need the
	/// registry to hold a registration the builder cannot produce.
	/// </summary>
	[Fact]
	public void ReplaceASameModeReRegistrationRatherThanRefuseIt()
	{
		using var provider = Build(es =>
		{
			_ = es.AddProjection<OrderSummary>(p => p.Async().When<TestOrderPlaced>((s, _) => s.EventCount++));
			_ = es.AddProjection<OrderSummary>(p => p.Async().When<TestOrderPlaced>((s, _) => s.EventCount += 2));
		});

		Populate(provider);

		var registry = provider.GetRequiredService<IProjectionRegistry>();
		registry.GetRegistration(typeof(OrderSummary)).ShouldNotBeNull().Mode.ShouldBe(ProjectionMode.Async);
		registry.GetAll().Count.ShouldBe(1);
	}

	/// <summary>
	/// LIVENESS, and the arm that matters: one registration per type still works, and several DIFFERENT
	/// types still register together. Without it, a registry refusing every second AddProjection call
	/// passes every arm above while making a multi-projection application impossible.
	/// </summary>
	[Fact]
	public void RegisterSeveralDifferentProjectionTypesTogether()
	{
		using var provider = Build(es =>
		{
			_ = es.AddProjection<OrderSummary>(p => p.Async().When<TestOrderPlaced>((s, _) => s.EventCount++));
			_ = es.AddProjection<InventoryView>(p => p.Inline().When<TestOrderPlaced>((s, _) => s.EventCount++));
			_ = es.AddProjection<CustomerSummary>(p => p.Ephemeral().When<TestOrderPlaced>((s, _) => s.Id = "c-1"));
		});

		Populate(provider);

		var registry = provider.GetRequiredService<IProjectionRegistry>();
		registry.GetRegistration(typeof(OrderSummary)).ShouldNotBeNull().Mode.ShouldBe(ProjectionMode.Async);
		registry.GetRegistration(typeof(InventoryView)).ShouldNotBeNull().Mode.ShouldBe(ProjectionMode.Inline);
		registry.GetRegistration(typeof(CustomerSummary)).ShouldNotBeNull().Mode.ShouldBe(ProjectionMode.Ephemeral);
		registry.GetAll().Count.ShouldBe(3);
	}

	/// <summary>
	/// Drives the deferred registrations into the registry the way the framework's own consumers do --
	/// the projection host and the notification broker each run this loop over every registered
	/// configuration before the registry is read.
	/// </summary>
	private static void Populate(IServiceProvider provider)
	{
		foreach (var configuration in
			provider.GetServices<EventNotificationServiceCollectionExtensions.IConfigureProjection>())
		{
			configuration.Configure();
		}
	}

	/// <summary>
	/// Builds the container through the REAL composition path, so the arms bind what a consumer's
	/// Program.cs binds rather than a hand-assembled copy of it. The defect lived in the composition:
	/// calling Register twice by hand would not have reproduced it.
	/// </summary>
	private static ServiceProvider Build(Action<IEventSourcingBuilder> configure)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(x => x.AddEventSourcing(es =>
		{
			_ = es.UseInMemory();
			_ = es.RegisterEventTypes(typeof(TestOrderPlaced));
			configure(es);
		}));

		return services.BuildServiceProvider();
	}
}
