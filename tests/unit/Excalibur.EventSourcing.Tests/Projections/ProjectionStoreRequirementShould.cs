// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// A projection registered with no store must stop the host at start-up.
/// </summary>
/// <remarks>
/// <para>
/// A stored projection takes TWO registrations: one saying what it folds, one saying where it is stored.
/// Nothing connected them, so omitting the second produced a projection that folds into nothing. For an
/// inline projection that surfaced as a resolution failure on the first save — after the events were
/// already durable, wrapped in a message about rebuilding from the event stream, which is the wrong
/// remedy when the fix is one registration line. For an async projection it surfaced inside a background
/// host, where nobody was looking.
/// </para>
/// <para>
/// So the check is made once, out loud, at start-up, against the container the apply path actually
/// resolves from. The arms below pair each refusal with the case that must still START: a check that
/// rejects everything is satisfied by a host that never runs.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionStoreRequirementShould
{
	[Fact]
	public void RefuseToStartWhenAProjectionHasNoStore()
	{
		using var provider = Build(es => es.AddProjection<OrderSummary>(p => p.Inline()));

		var error = Should.Throw<InvalidOperationException>(
			() => Validate(provider));

		error.Message.ShouldContain(
			nameof(OrderSummary),
			Case.Sensitive,
			"the message has to name the projection -- a host with twenty projections cannot act on "
			+ "'a projection has no store'");
	}

	/// <summary>
	/// LIVENESS. Without this arm the refusal above is satisfied by a check that rejects every host.
	/// </summary>
	[Fact]
	public void StartWhenEveryProjectionHasAStore()
	{
		using var provider = Build(es =>
		{
			_ = es.AddProjection<OrderSummary>(p => p.Inline());
			_ = es.Services.AddSingleton<IProjectionStore<OrderSummary>>(new FakeProjectionStore<OrderSummary>());
		});

		Validate(provider);
	}

	/// <summary>
	/// An ephemeral projection is rebuilt on demand and never stored, so storelessness is correct for it
	/// and must not be reported. This is the arm that keeps the check from punishing a supported choice.
	/// </summary>
	[Fact]
	public void StartWhenAStorelessProjectionIsEphemeral()
	{
		using var provider = Build(es => es.AddProjection<OrderSummary>(p => p.Ephemeral()));

		Validate(provider);
	}

	/// <summary>
	/// The message names the projections that are actually missing a store and no others, so a host with
	/// one mistake among many is told which one.
	/// </summary>
	[Fact]
	public void NameOnlyTheProjectionsThatAreMissingAStore()
	{
		using var provider = Build(es =>
		{
			_ = es.AddProjection<OrderSummary>(p => p.Inline());
			_ = es.AddProjection<CustomerSummary>(p => p.Inline());
			_ = es.Services.AddSingleton<IProjectionStore<CustomerSummary>>(
				new FakeProjectionStore<CustomerSummary>());
		});

		var error = Should.Throw<InvalidOperationException>(() => Validate(provider));

		error.Message.ShouldContain(nameof(OrderSummary), Case.Sensitive);
		error.Message.ShouldNotContain(nameof(CustomerSummary), Case.Sensitive);
	}

	/// <summary>
	/// The message has to tell the reader what to type. Asserting on the remedy rather than only on the
	/// throw is what keeps a future edit from reducing it to "projection misconfigured".
	/// </summary>
	[Fact]
	public void SayWhichSecondRegistrationIsMissingAndOfferTheEphemeralAlternative()
	{
		using var provider = Build(es => es.AddProjection<OrderSummary>(p => p.Inline()));

		var message = Should.Throw<InvalidOperationException>(() => Validate(provider)).Message;

		message.ShouldContain("AddProjection", Case.Sensitive);
		message.ShouldContain("Ephemeral", Case.Sensitive);
	}

	private static void Validate(IServiceProvider provider)
	{
		foreach (var validator in provider.GetServices<IStartupPrerequisiteValidator>())
		{
			validator.Validate();
		}
	}

	/// <summary>
	/// Builds the container through the REAL registration path, so the arms bind what a host binds
	/// rather than a hand-assembled copy of it.
	/// </summary>
	private static ServiceProvider Build(Action<IEventSourcingBuilder> configure)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		configure(new ExcaliburEventSourcingBuilder(services));

		return services.BuildServiceProvider();
	}
}

internal sealed class CustomerSummary
{
	public string Id { get; set; } = string.Empty;
}

internal sealed class FakeProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
		Task.FromResult<TProjection?>(null);

	public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken) =>
		Task.CompletedTask;

	public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

	public Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken) =>
		Task.FromResult<IReadOnlyList<TProjection>>([]);

	public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
		Task.FromResult(0L);
}
