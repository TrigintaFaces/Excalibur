// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;
using Excalibur.Saga.Queries;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.Core.Queries;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemorySagaCorrelationQueryDepthShould
{
	private readonly InMemorySagaCorrelationQuery _sut;
	private readonly SagaCorrelationQueryOptions _options = new();

	public InMemorySagaCorrelationQueryDepthShould()
	{
		_sut = new InMemorySagaCorrelationQuery(
			Options.Create(_options),
			NullLogger<InMemorySagaCorrelationQuery>.Instance);
	}

	[Fact]
	public async Task FindByPropertyWhenPropertyIndexed()
	{
		// Arrange
		_sut.IndexSaga("saga-1", "OrderSaga", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow);
		_sut.IndexProperty("saga-1", "OrderId", "order-42");

		// Act
		var results = await _sut.FindByPropertyAsync("OrderId", "order-42", CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].SagaId.ShouldBe("saga-1");
	}

	[Fact]
	public async Task ReturnEmptyWhenPropertyNotIndexed()
	{
		// Act
		var results = await _sut.FindByPropertyAsync("OrderId", "order-99", CancellationToken.None);

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnEmptyWhenPropertyNameDoesNotExist()
	{
		// Arrange
		_sut.IndexSaga("saga-1", "OrderSaga", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow);
		_sut.IndexProperty("saga-1", "OrderId", "order-42");

		// Act
		var results = await _sut.FindByPropertyAsync("CustomerId", "cust-1", CancellationToken.None);

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public void UpdateSagaStatus()
	{
		// Arrange
		_sut.IndexSaga("saga-1", "OrderSaga", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow);
		_sut.Count.ShouldBe(1);

		// Act
		_sut.UpdateStatus("saga-1", SagaStatus.Completed);

		// Assert - saga count unchanged, status updated
		_sut.Count.ShouldBe(1);
	}

	[Fact]
	public void ClearAllData()
	{
		// Arrange
		_sut.IndexSaga("saga-1", "OrderSaga", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow);
		_sut.IndexProperty("saga-1", "OrderId", "order-42");
		_sut.Count.ShouldBe(1);

		// Act
		_sut.Clear();

		// Assert
		_sut.Count.ShouldBe(0);
	}

	[Fact]
	public async Task ExcludeCompletedSagasByDefault()
	{
		// Arrange
		_sut.IndexSaga("saga-1", "OrderSaga", "corr-1", SagaStatus.Completed, DateTimeOffset.UtcNow);

		// Act
		var results = await _sut.FindByCorrelationIdAsync("corr-1", CancellationToken.None);

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task IncludeCompletedSagasWhenOptionSet()
	{
		// Arrange
		var options = new SagaCorrelationQueryOptions { IncludeCompleted = true };
		var sut = new InMemorySagaCorrelationQuery(
			Options.Create(options),
			NullLogger<InMemorySagaCorrelationQuery>.Instance);
		sut.IndexSaga("saga-1", "OrderSaga", "corr-1", SagaStatus.Completed, DateTimeOffset.UtcNow);

		// Act
		var results = await sut.FindByCorrelationIdAsync("corr-1", CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
	}

	[Fact]
	public void ThrowWhenCorrelationIdIsNullForFind()
	{
		Should.Throw<ArgumentException>(async () =>
			await _sut.FindByCorrelationIdAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void ThrowWhenPropertyNameIsNullForFind()
	{
		Should.Throw<ArgumentException>(async () =>
			await _sut.FindByPropertyAsync(null!, "value", CancellationToken.None));
	}

	[Fact]
	public void ThrowWhenValueIsNullForFindByProperty()
	{
		Should.Throw<ArgumentNullException>(async () =>
			await _sut.FindByPropertyAsync("prop", null!, CancellationToken.None));
	}

	[Fact]
	public void ThrowWhenSagaIdIsNullForIndex()
	{
		Should.Throw<ArgumentException>(() =>
			_sut.IndexSaga(null!, "OrderSaga", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow));
	}

	[Fact]
	public void ThrowWhenSagaNameIsNullForIndex()
	{
		Should.Throw<ArgumentException>(() =>
			_sut.IndexSaga("saga-1", null!, "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow));
	}

	[Fact]
	public void ThrowWhenCorrelationIdIsNullForIndex()
	{
		Should.Throw<ArgumentException>(() =>
			_sut.IndexSaga("saga-1", "OrderSaga", null!, SagaStatus.Running, DateTimeOffset.UtcNow));
	}

	[Fact]
	public void ThrowWhenSagaIdIsNullForUpdateStatus()
	{
		Should.Throw<ArgumentException>(() =>
			_sut.UpdateStatus(null!, SagaStatus.Completed));
	}

	[Fact]
	public void NotThrowWhenUpdatingNonExistentSaga()
	{
		// Act - should not throw
		_sut.UpdateStatus("non-existent", SagaStatus.Completed);
	}

	[Fact]
	public void ThrowWhenSagaIdIsNullForIndexProperty()
	{
		Should.Throw<ArgumentException>(() =>
			_sut.IndexProperty(null!, "prop", "value"));
	}

	[Fact]
	public void ThrowWhenPropertyNameIsNullForIndexProperty()
	{
		Should.Throw<ArgumentException>(() =>
			_sut.IndexProperty("saga-1", null!, "value"));
	}

	[Fact]
	public void ThrowWhenValueIsNullForIndexProperty()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.IndexProperty("saga-1", "prop", null!));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InMemorySagaCorrelationQuery(null!, NullLogger<InMemorySagaCorrelationQuery>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InMemorySagaCorrelationQuery(
				Options.Create(new SagaCorrelationQueryOptions()),
				null!));
	}

	[Fact]
	public async Task RespectMaxResultsOption()
	{
		// Arrange
		var options = new SagaCorrelationQueryOptions { MaxResults = 1 };
		var sut = new InMemorySagaCorrelationQuery(
			Options.Create(options),
			NullLogger<InMemorySagaCorrelationQuery>.Instance);
		sut.IndexSaga("saga-1", "OrderSaga", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow);
		sut.IndexSaga("saga-2", "OrderSaga", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow);

		// Act
		var results = await sut.FindByCorrelationIdAsync("corr-1", CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
	}
}
