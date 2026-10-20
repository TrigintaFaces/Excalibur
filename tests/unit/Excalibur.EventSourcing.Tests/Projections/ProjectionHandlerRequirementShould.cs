// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// A projection handler whose dependencies the container cannot supply must stop the host at start-up.
/// </summary>
/// <remarks>
/// <para>
/// The store check one level out asks whether each registered projection has somewhere to be stored. It
/// does not ask whether the handlers that fold into that projection can be CONSTRUCTED. A handler
/// depending on a store belonging to some OTHER projection -- a common shape, because one handler often
/// reads several projections to build the one it writes -- therefore passed start-up and failed on the
/// first event instead.
/// </para>
/// <para>
/// Where that failure lands is the whole problem. An async projection folds inside a background host, so
/// the resolution failure surfaces with no request to fail, nobody watching, and events already durable.
/// The handler type is known at the moment it is registered, so the question is answerable at start-up
/// and is asked there.
/// </para>
/// <para>
/// Each refusal below is paired with a host that must still START. A check that rejects everything is
/// satisfied by a host that never runs, and would make every legal handler shape unusable.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionHandlerRequirementShould
{
	[Fact]
	public void RefuseToStartWhenAHandlerDependencyIsNotRegistered()
	{
		using var provider = BuildWithStoredProjection(p =>
			p.WhenHandledBy<TestOrderPlaced, HandlerNeedingAnotherProjectionsStore>());

		var error = Should.Throw<InvalidOperationException>(() => Validate(provider));

		error.Message.ShouldContain(
			nameof(HandlerNeedingAnotherProjectionsStore),
			Case.Sensitive,
			"a host with twenty handlers cannot act on 'a handler is not constructible'");
	}

	/// <summary>
	/// The message has to name the dependency, not only the handler: the handler is where the symptom is,
	/// the dependency is where the missing registration is.
	/// </summary>
	[Fact]
	public void NameTheDependencyTheContainerCannotSupply()
	{
		using var provider = BuildWithStoredProjection(p =>
			p.WhenHandledBy<TestOrderPlaced, HandlerNeedingAnotherProjectionsStore>());

		var message = Should.Throw<InvalidOperationException>(() => Validate(provider)).Message;

		message.ShouldContain("IProjectionStore<CustomerSummary>", Case.Sensitive);
	}

	/// <summary>
	/// LIVENESS. Without this arm the refusal above is satisfied by a check that rejects every handler.
	/// </summary>
	[Fact]
	public void StartWhenEveryHandlerDependencyIsRegistered()
	{
		using var provider = BuildWithStoredProjection(
			p => p.WhenHandledBy<TestOrderPlaced, HandlerNeedingAnotherProjectionsStore>(),
			services => services.AddSingleton<IProjectionStore<CustomerSummary>>(
				new FakeProjectionStore<CustomerSummary>()));

		Validate(provider);
	}

	/// <summary>
	/// LIVENESS. The container manufactures some dependencies without a registration -- a provider, a
	/// scope factory, an empty <see cref="IEnumerable{T}"/>. Asking the container whether each parameter
	/// is a service, rather than scanning the registrations, is what keeps those legal and is why this
	/// check needs no exemption list of its own.
	/// </summary>
	[Fact]
	public void StartWhenAHandlerNeedsOnlyDependenciesTheContainerManufactures()
	{
		using var provider = BuildWithStoredProjection(p =>
			p.WhenHandledBy<TestOrderPlaced, HandlerNeedingContainerSuppliedDependencies>());

		Validate(provider);
	}

	/// <summary>
	/// LIVENESS. An optional dependency is a supported shape: the container omits it and the handler
	/// still constructs, so an absent registration is not a defect here.
	/// </summary>
	[Fact]
	public void StartWhenAnAbsentDependencyHasADefaultValue()
	{
		using var provider = BuildWithStoredProjection(p =>
			p.WhenHandledBy<TestOrderPlaced, HandlerWithAnOptionalDependency>());

		Validate(provider);
	}

	/// <summary>
	/// A host with one mistake among many is told which handler has it.
	/// </summary>
	[Fact]
	public void NameOnlyTheHandlersThatCannotBeConstructed()
	{
		using var provider = BuildWithStoredProjection(p => p
			.WhenHandledBy<TestOrderPlaced, HandlerNeedingAnotherProjectionsStore>()
			.WhenHandledBy<TestOrderShipped, HandlerNeedingNothing>());

		var message = Should.Throw<InvalidOperationException>(() => Validate(provider)).Message;

		message.ShouldContain(nameof(HandlerNeedingAnotherProjectionsStore), Case.Sensitive);
		message.ShouldNotContain(nameof(HandlerNeedingNothing), Case.Sensitive);
	}

	private static void Validate(IServiceProvider provider)
	{
		foreach (var validator in provider.GetServices<IStartupPrerequisiteValidator>())
		{
			validator.Validate();
		}
	}

	/// <summary>
	/// Builds through the REAL registration path, with the projection's own store present so the
	/// storeless check cannot fire and mask what these arms are about.
	/// </summary>
	private static ServiceProvider BuildWithStoredProjection(
		Action<IProjectionBuilder<HandlerFixtureProjection>> configure,
		Action<IServiceCollection>? alsoRegister = null)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var builder = new ExcaliburEventSourcingBuilder(services);
		_ = builder.AddProjection<HandlerFixtureProjection>(p =>
		{
			_ = p.Inline();
			configure(p);
		});
		_ = services.AddSingleton<IProjectionStore<HandlerFixtureProjection>>(new FakeProjectionStore<HandlerFixtureProjection>());
		alsoRegister?.Invoke(services);

		return services.BuildServiceProvider();
	}
}

/// <summary>
/// The live shape: a handler that folds one projection while reading another projection's store. Nothing
/// registers the store it asks for, so this handler cannot be constructed.
/// </summary>
internal sealed class HandlerNeedingAnotherProjectionsStore
	: IProjectionEventHandler<HandlerFixtureProjection, TestOrderPlaced>
{
	public HandlerNeedingAnotherProjectionsStore(IProjectionStore<CustomerSummary> customers) =>
		Customers = customers;

	public IProjectionStore<CustomerSummary> Customers { get; }

	public Task HandleAsync(
		HandlerFixtureProjection projection,
		TestOrderPlaced @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class HandlerNeedingNothing : IProjectionEventHandler<HandlerFixtureProjection, TestOrderShipped>
{
	public Task HandleAsync(
		HandlerFixtureProjection projection,
		TestOrderShipped @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class HandlerNeedingContainerSuppliedDependencies
	: IProjectionEventHandler<HandlerFixtureProjection, TestOrderPlaced>
{
	public HandlerNeedingContainerSuppliedDependencies(
		IServiceProvider services,
		IServiceScopeFactory scopes,
		IEnumerable<IProjectionStore<CustomerSummary>> unregisteredButEnumerable)
	{
		Services = services;
		Scopes = scopes;
		Stores = unregisteredButEnumerable;
	}

	public IServiceProvider Services { get; }

	public IServiceScopeFactory Scopes { get; }

	public IEnumerable<IProjectionStore<CustomerSummary>> Stores { get; }

	public Task HandleAsync(
		HandlerFixtureProjection projection,
		TestOrderPlaced @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class HandlerWithAnOptionalDependency
	: IProjectionEventHandler<HandlerFixtureProjection, TestOrderPlaced>
{
	public HandlerWithAnOptionalDependency(IProjectionStore<CustomerSummary>? customers = null) =>
		Customers = customers;

	public IProjectionStore<CustomerSummary>? Customers { get; }

	public Task HandleAsync(
		HandlerFixtureProjection projection,
		TestOrderPlaced @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// A projection type private to these arms.
/// </summary>
/// <remarks>
/// Deliberately NOT one of the shared fixture projections. The handler fixtures below deliberately
/// include several handlers for the same event, which is legal here because each arm registers one of
/// them -- but <c>AddProjectionHandlersFromAssembly</c> scans the whole test assembly and refuses two
/// handlers for the same (projection, event) pair. Pointing these at their own projection keeps them
/// invisible to every scan-based arm instead of breaking one.
/// </remarks>
internal sealed class HandlerFixtureProjection
{
	public int EventCount { get; set; }
}
