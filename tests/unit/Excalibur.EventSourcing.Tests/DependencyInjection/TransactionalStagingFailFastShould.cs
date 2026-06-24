// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Implementation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

// bd-us5tfv (S841, ADR-336 clause 2): explicit OutboxStagingStrategy.Transactional WITHOUT the required
// transactional infrastructure used to SILENTLY degrade to non-atomic eventually-consistent staging
// (integration events lost on a crash between append and stage, no diagnostic). TransactionalStagingCapability-
// Validator makes that inexpressible — fail-fast at startup naming exactly what is missing. Independent engage-
// test (author≠impl): the guard FAILS for explicit-Transactional-without-infra (AC-7), SUCCEEDS for
// Transactional+infra (AC-7a), and NEVER trips for Auto/EventuallyConsistent/Deferred (EC-6). RED on the pre-fix
// silent degrade (the validator did not exist; explicit Transactional fell through to non-atomic with no throw).
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class TransactionalStagingFailFastShould
{
	// AC-7 — explicit Transactional with NO transactional infrastructure → fail, naming both missing pieces.
	[Fact]
	public void Fail_WhenTransactionalExplicit_WithoutInfrastructure()
	{
		using var provider = new ServiceCollection().BuildServiceProvider();
		var validator = new TransactionalStagingCapabilityValidator(provider);

		var result = validator.Validate(name: null, new EventSourcedRepositoryOptions
		{
			OutboxStagingStrategy = OutboxStagingStrategy.Transactional,
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldNotBeNull();
		result.FailureMessage.ShouldContain("ITransactionalOutboxWriter");
		result.FailureMessage.ShouldContain("transactional event store");
	}

	// AC-7 (partial infra) — a writer but no transactional event store is still a fail (no silent degrade).
	[Fact]
	public void Fail_WhenTransactionalExplicit_WithWriterButNoTransactionalEventStore()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(A.Fake<ITransactionalOutboxWriter>());
		using var provider = services.BuildServiceProvider();
		var validator = new TransactionalStagingCapabilityValidator(provider);

		var result = validator.Validate(null, new EventSourcedRepositoryOptions
		{
			OutboxStagingStrategy = OutboxStagingStrategy.Transactional,
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage!.ShouldContain("transactional event store");
	}

	// AC-7a — explicit Transactional WITH both pieces registered → starts (the guard is precise, not over-broad).
	[Fact]
	public void Succeed_WhenTransactionalExplicit_WithBothInfrastructurePieces()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(A.Fake<ITransactionalOutboxWriter>());
		_ = services.AddSingleton<IEventStore>(new FakeTransactionalEventStore());
		using var provider = services.BuildServiceProvider();
		var validator = new TransactionalStagingCapabilityValidator(provider);

		var result = validator.Validate(null, new EventSourcedRepositoryOptions
		{
			OutboxStagingStrategy = OutboxStagingStrategy.Transactional,
		});

		result.Succeeded.ShouldBeTrue();
	}

	// EC-6 / AC-7a — Auto, EventuallyConsistent, Deferred never trip the guard, even without infrastructure.
	[Theory]
	[InlineData(OutboxStagingStrategy.Auto)]
	[InlineData(OutboxStagingStrategy.EventuallyConsistent)]
	[InlineData(OutboxStagingStrategy.Deferred)]
	public void Succeed_WhenStrategyIsNotTransactional_EvenWithoutInfrastructure(OutboxStagingStrategy strategy)
	{
		using var provider = new ServiceCollection().BuildServiceProvider();
		var validator = new TransactionalStagingCapabilityValidator(provider);

		var result = validator.Validate(null, new EventSourcedRepositoryOptions
		{
			OutboxStagingStrategy = strategy,
		});

		result.Succeeded.ShouldBeTrue();
	}

	// Wiring — the validator is actually registered as an IValidateOptions for EventSourcedRepositoryOptions by
	// AddExcaliburEventSourcing (which also calls ValidateOnStart), so the guard runs at startup, not just in
	// this unit. Without this, the (correct) validator logic above would be dead code.
	[Fact]
	public void BeRegistered_AsAnOptionsValidator_ByAddExcaliburEventSourcing()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburEventSourcing();
		using var provider = services.BuildServiceProvider();

		provider.GetServices<IValidateOptions<EventSourcedRepositoryOptions>>()
			.ShouldContain(v => v is TransactionalStagingCapabilityValidator);
	}

	/// <summary>
	/// Minimal hand-written stub implementing the internal <see cref="ITransactionalEventStore"/> (FakeItEasy
	/// cannot proxy it). The validator only checks <c>eventStore is ITransactionalEventStore</c>; the store
	/// methods are never invoked.
	/// </summary>
	private sealed class FakeTransactionalEventStore : ITransactionalEventStore
	{
		public Task<IDbTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
			Task.FromResult<IDbTransaction?>(null);

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
			throw new NotSupportedException();

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, long fromVersion, CancellationToken cancellationToken) =>
			throw new NotSupportedException();

		public ValueTask<AppendResult> AppendAsync(
			string aggregateId, string aggregateType, IEnumerable<IDomainEvent> events, long expectedVersion,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException();
	}
}
