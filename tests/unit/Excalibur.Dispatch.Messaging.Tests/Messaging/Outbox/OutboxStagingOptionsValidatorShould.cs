// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Middleware.Transaction;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Messaging.Tests.Messaging.Outbox;

/// <summary>
/// Unit tests for <see cref="OutboxStagingOptionsValidator"/>.
/// Validates startup configuration checks for outbox consistency modes.
/// </summary>
/// <remarks>
/// Sprint 697: Updated to use ServiceCollection.BuildServiceProvider() because
/// the validator now uses keyed service resolution (GetKeyedService) for IOutboxStore.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class OutboxStagingOptionsValidatorShould : UnitTestBase
{
	[Fact]
	public void PassEventuallyConsistentWithoutOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();
		var sp = services.BuildServiceProvider();
		var sut = new OutboxStagingOptionsValidator(sp);
		var options = new OutboxStagingOptions
		{
			ConsistencyMode = OutboxConsistencyMode.EventuallyConsistent,
		};

		// Act
		var result = sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void PassEventuallyConsistentWithOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddKeyedSingleton<IOutboxStore>("default", A.Fake<IOutboxStore>());
		var sp = services.BuildServiceProvider();
		var sut = new OutboxStagingOptionsValidator(sp);
		var options = new OutboxStagingOptions
		{
			ConsistencyMode = OutboxConsistencyMode.EventuallyConsistent,
		};

		// Act
		var result = sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	/// <summary>
	/// The guard's verdict is INVARIANT to whether the event store can stage transactionally, because this
	/// package cannot see that capability. A pass therefore means "the dispatch-side prerequisites are
	/// present", never "your writes will be transactional".
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Requirement:</b> a consumer must not be able to read a green from this validator as a guarantee
	/// about the write path. <b>Predicate this arm tests:</b> the validator returns the same result for two
	/// configurations that differ only in whether a transactional event-store capability is registered.
	/// </para>
	/// <para>
	/// <b>Why the gap cannot be closed here.</b> The capability that decides the write path is declared in
	/// the event-sourcing packages, and the dependency runs one way -- event sourcing depends on dispatch,
	/// never the reverse. This validator therefore structurally cannot check it, which is why the limit is
	/// stated in its messages rather than fixed in its logic. If someone later teaches it to check, this arm
	/// goes RED and points at the documentation that says it does not.
	/// </para>
	/// <para>
	/// The registered capability is a locally-declared marker rather than the real interface, for the same
	/// reason: referencing the real one from this project would be the very dependency the design forbids.
	/// </para>
	/// </remarks>
	[Fact]
	public void ReachTheSameVerdict_WhetherOrNotATransactionalEventStoreCapabilityIsRegistered()
	{
		// Arrange -- identical except for the capability registration.
		static OutboxStagingOptionsValidator Build(bool withTransactionalEventStoreCapability)
		{
			var services = new ServiceCollection();
			services.AddKeyedSingleton<IOutboxStore>("default", A.Fake<IOutboxStore>());
			var transactionMiddleware = new TransactionMiddleware(
				Microsoft.Extensions.Options.Options.Create(new TransactionOptions()),
				A.Fake<ITransactionService>(),
				NullLogger<TransactionMiddleware>.Instance);
			services.AddSingleton<IDispatchMiddleware>(transactionMiddleware);

			if (withTransactionalEventStoreCapability)
			{
				services.AddSingleton(new TransactionalEventStoreCapabilityStandIn());
			}

			return new OutboxStagingOptionsValidator(services.BuildServiceProvider());
		}

		var options = new OutboxStagingOptions
		{
			ConsistencyMode = OutboxConsistencyMode.Transactional,
		};

		// Act
		var withCapability = Build(withTransactionalEventStoreCapability: true).Validate(null, options);
		var withoutCapability = Build(withTransactionalEventStoreCapability: false).Validate(null, options);

		// Assert -- both pass, and that they agree is the point: the verdict carries no information about
		// the write path a consumer will actually get.
		withCapability.Succeeded.ShouldBeTrue();
		withoutCapability.Succeeded.ShouldBeTrue(
			"a configuration with no transactional event-store capability still satisfies this guard, so a "
			+ "consumer who reads its success as 'my writes are transactional' has been misled by a control "
			+ "that never checked it");
		withoutCapability.Succeeded.ShouldBe(
			withCapability.Succeeded,
			"the guard must not appear to discriminate on a capability it cannot observe");
	}

	/// <summary>
	/// Stands in for a transactional event-store capability. Declared here rather than referencing the real
	/// interface because that type lives in the event-sourcing packages, which this project must not depend
	/// on -- the same boundary that prevents the validator from checking it.
	/// </summary>
	private sealed class TransactionalEventStoreCapabilityStandIn;

	[Fact]
	public void PassTransactionalWithOutboxStoreAndTransactionMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddKeyedSingleton<IOutboxStore>("default", A.Fake<IOutboxStore>());
		var transactionMiddleware = new TransactionMiddleware(
			Microsoft.Extensions.Options.Options.Create(new TransactionOptions()),
			A.Fake<ITransactionService>(),
			NullLogger<TransactionMiddleware>.Instance);
		services.AddSingleton<IDispatchMiddleware>(transactionMiddleware);
		var sp = services.BuildServiceProvider();
		var sut = new OutboxStagingOptionsValidator(sp);
		var options = new OutboxStagingOptions
		{
			ConsistencyMode = OutboxConsistencyMode.Transactional,
		};

		// Act
		var result = sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void RejectTransactionalWithoutTransactionMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddKeyedSingleton<IOutboxStore>("default", A.Fake<IOutboxStore>());
		var sp = services.BuildServiceProvider();
		var sut = new OutboxStagingOptionsValidator(sp);
		var options = new OutboxStagingOptions
		{
			ConsistencyMode = OutboxConsistencyMode.Transactional,
		};

		// Act
		var result = sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TransactionMiddleware");
		result.FailureMessage.ShouldContain("UseTransaction");
	}

	[Fact]
	public void RejectTransactionalWithoutOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();
		var sp = services.BuildServiceProvider();
		var sut = new OutboxStagingOptionsValidator(sp);
		var options = new OutboxStagingOptions
		{
			ConsistencyMode = OutboxConsistencyMode.Transactional,
		};

		// Act
		var result = sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("IOutboxStore");
	}

	[Fact]
	public void IncludeRegistrationGuidanceInFailureMessage()
	{
		// Arrange
		var services = new ServiceCollection();
		var sp = services.BuildServiceProvider();
		var sut = new OutboxStagingOptionsValidator(sp);
		var options = new OutboxStagingOptions
		{
			ConsistencyMode = OutboxConsistencyMode.Transactional,
		};

		// Act
		var result = sut.Validate(null, options);

		// Assert
		result.FailureMessage.ShouldContain("AddCosmosDbOutbox");
	}

	[Fact]
	public void AcceptNamedOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddKeyedSingleton<IOutboxStore>("default", A.Fake<IOutboxStore>());
		var transactionMiddleware = new TransactionMiddleware(
			Microsoft.Extensions.Options.Options.Create(new TransactionOptions()),
			A.Fake<ITransactionService>(),
			NullLogger<TransactionMiddleware>.Instance);
		services.AddSingleton<IDispatchMiddleware>(transactionMiddleware);
		var sp = services.BuildServiceProvider();
		var sut = new OutboxStagingOptionsValidator(sp);
		var options = new OutboxStagingOptions
		{
			ConsistencyMode = OutboxConsistencyMode.Transactional,
		};

		// Act -- pass a named options instance
		var result = sut.Validate("CustomPipeline", options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}
}
