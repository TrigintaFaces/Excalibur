// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Outbox;
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
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
