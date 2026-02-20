// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;
using Excalibur.Saga.Queries;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.Core.Queries;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemorySagaCorrelationQueryShould
{
	private readonly SagaCorrelationQueryOptions _options;
	private readonly InMemorySagaCorrelationQuery _sut;

	public InMemorySagaCorrelationQueryShould()
	{
		_options = new SagaCorrelationQueryOptions
		{
			MaxResults = 100,
			IncludeCompleted = false
		};
		_sut = new InMemorySagaCorrelationQuery(
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<InMemorySagaCorrelationQuery>.Instance);
	}

	[Fact]
	public async Task FindByCorrelationId_ReturnMatchingSagas()
	{
		// Arrange
		_sut.IndexSaga("saga-1", "OrderSaga", "corr-abc", SagaStatus.Running, DateTimeOffset.UtcNow);
		_sut.IndexSaga("saga-2", "OrderSaga", "corr-abc", SagaStatus.Running, DateTimeOffset.UtcNow);
		_sut.IndexSaga("saga-3", "OrderSaga", "corr-xyz", SagaStatus.Running, DateTimeOffset.UtcNow);

		// Act
		var results = await _sut.FindByCorrelationIdAsync("corr-abc", CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
	}

	[Fact]
	public async Task FindByCorrelationId_ReturnEmpty_WhenNoMatch()
	{
		var results = await _sut.FindByCorrelationIdAsync("nonexistent", CancellationToken.None);
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task FindByCorrelationId_ExcludeCompleted_ByDefault()
	{
		// Arrange
		_sut.IndexSaga("saga-1", "OrderSaga", "corr-abc", SagaStatus.Completed, DateTimeOffset.UtcNow);

		// Act
		var results = await _sut.FindByCorrelationIdAsync("corr-abc", CancellationToken.None);

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task FindByCorrelationId_IncludeCompleted_WhenOptionSet()
	{
		// Arrange
		_options.IncludeCompleted = true;
		_sut.IndexSaga("saga-1", "OrderSaga", "corr-abc", SagaStatus.Completed, DateTimeOffset.UtcNow);

		// Act
		var results = await _sut.FindByCorrelationIdAsync("corr-abc", CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
	}

	[Fact]
	public async Task FindByProperty_ReturnMatchingSagas()
	{
		// Arrange
		_sut.IndexSaga("saga-1", "OrderSaga", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow);
		_sut.IndexProperty("saga-1", "OrderId", "ORD-123");

		// Act
		var results = await _sut.FindByPropertyAsync("OrderId", "ORD-123", CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].SagaId.ShouldBe("saga-1");
	}

	[Fact]
	public async Task FindByProperty_ReturnEmpty_WhenPropertyNotFound()
	{
		var results = await _sut.FindByPropertyAsync("Unknown", "value", CancellationToken.None);
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task FindByProperty_ReturnEmpty_WhenValueNotFound()
	{
		// Arrange
		_sut.IndexSaga("saga-1", "OrderSaga", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow);
		_sut.IndexProperty("saga-1", "OrderId", "ORD-123");

		// Act
		var results = await _sut.FindByPropertyAsync("OrderId", "ORD-999", CancellationToken.None);

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public void UpdateStatus_ChangesSagaStatus()
	{
		// Arrange
		_sut.IndexSaga("saga-1", "OrderSaga", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow);

		// Act
		_sut.UpdateStatus("saga-1", SagaStatus.Completed);

		// The status changed to Completed, and IncludeCompleted = false
		var result = _sut.FindByCorrelationIdAsync("corr-1", CancellationToken.None).Result;
		result.ShouldBeEmpty();
	}

	[Fact]
	public void UpdateStatus_NoOp_WhenSagaNotFound()
	{
		// Act & Assert (should not throw)
		_sut.UpdateStatus("nonexistent", SagaStatus.Failed);
	}

	[Fact]
	public void Clear_RemoveAllData()
	{
		// Arrange
		_sut.IndexSaga("saga-1", "OrderSaga", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow);
		_sut.IndexProperty("saga-1", "OrderId", "ORD-123");
		_sut.Count.ShouldBe(1);

		// Act
		_sut.Clear();

		// Assert
		_sut.Count.ShouldBe(0);
	}

	[Fact]
	public async Task ThrowOnNullOrEmptyArgs()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.FindByCorrelationIdAsync(null!, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.FindByPropertyAsync(null!, "val", CancellationToken.None));
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.FindByPropertyAsync("prop", null!, CancellationToken.None));
	}

	[Fact]
	public void IndexSaga_ThrowOnInvalidArgs()
	{
		Should.Throw<ArgumentException>(() =>
			_sut.IndexSaga(null!, "OrderSaga", "corr", SagaStatus.Running, DateTimeOffset.UtcNow));
		Should.Throw<ArgumentException>(() =>
			_sut.IndexSaga("id", null!, "corr", SagaStatus.Running, DateTimeOffset.UtcNow));
		Should.Throw<ArgumentException>(() =>
			_sut.IndexSaga("id", "OrderSaga", null!, SagaStatus.Running, DateTimeOffset.UtcNow));
	}

	[Fact]
	public void IndexProperty_ThrowOnInvalidArgs()
	{
		Should.Throw<ArgumentException>(() => _sut.IndexProperty(null!, "prop", "val"));
		Should.Throw<ArgumentException>(() => _sut.IndexProperty("id", null!, "val"));
		Should.Throw<ArgumentNullException>(() => _sut.IndexProperty("id", "prop", null!));
	}

	[Fact]
	public void UpdateStatus_ThrowOnInvalidArgs()
	{
		Should.Throw<ArgumentException>(() => _sut.UpdateStatus(null!, SagaStatus.Running));
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		var opts = Microsoft.Extensions.Options.Options.Create(new SagaCorrelationQueryOptions());
		var logger = NullLogger<InMemorySagaCorrelationQuery>.Instance;

		Should.Throw<ArgumentNullException>(() => new InMemorySagaCorrelationQuery(null!, logger));
		Should.Throw<ArgumentNullException>(() => new InMemorySagaCorrelationQuery(opts, null!));
	}

	[Fact]
	public async Task RespectMaxResults()
	{
		// Arrange
		_options.MaxResults = 1;

		_sut.IndexSaga("saga-1", "S", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow);
		_sut.IndexSaga("saga-2", "S", "corr-1", SagaStatus.Running, DateTimeOffset.UtcNow);

		// Act
		var results = await _sut.FindByCorrelationIdAsync("corr-1", CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
	}
}
