// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Messaging.Delivery.Inbox.Deduplication;

/// <summary>
///     Unit tests for CompositeDeduplicationStrategy to verify composite deduplication functionality.
/// </summary>
[Trait("Category", "Unit")]
public class CompositeDeduplicationStrategyShould
{
	private readonly IDeduplicationStrategy _primaryStrategy;
	private readonly IDeduplicationStrategy _secondaryStrategy;
	private readonly DeduplicationOptions _options;

	public CompositeDeduplicationStrategyShould()
	{
		_primaryStrategy = A.Fake<IDeduplicationStrategy>();
		_secondaryStrategy = A.Fake<IDeduplicationStrategy>();
		_options = new DeduplicationOptions { DeduplicationWindow = TimeSpan.FromMinutes(30) };
	}

	[Fact]
	public void ConstructorShouldInitializeWithMultipleStrategies()
	{
		// Arrange
		var secondary = new[] { _secondaryStrategy };

		// Act
		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, secondary, _options);

		// Assert
		_ = composite.ShouldNotBeNull();
	}

	[Fact]
	public void ConstructorShouldThrowArgumentNullExceptionForNullPrimary() =>
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new CompositeDeduplicationStrategy(null!, new[] { _secondaryStrategy }, _options));

	[Fact]
	public void ConstructorShouldThrowArgumentNullExceptionForNullOptions()
	{
		// Arrange
		var secondary = new[] { _secondaryStrategy };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new CompositeDeduplicationStrategy(_primaryStrategy, secondary, null!));
	}

	[Fact]
	public void ConstructorShouldAcceptEmptySecondaryStrategies()
	{
		// Arrange
		var emptySecondary = Array.Empty<IDeduplicationStrategy>();

		// Act
		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, emptySecondary, _options);

		// Assert
		_ = composite.ShouldNotBeNull();
	}

	[Fact]
	public void ConstructorShouldAcceptNullSecondaryStrategies()
	{
		// Act
		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, null!, _options);

		// Assert
		_ = composite.ShouldNotBeNull();
	}

	[Fact]
	public void DefaultExpirationShouldReturnDeduplicationWindow()
	{
		// Arrange
		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, Array.Empty<IDeduplicationStrategy>(), _options);

		// Assert
		composite.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void GenerateDeduplicationIdShouldDelegateToPrimaryStrategy()
	{
		// Arrange
		var messageBody = "{\"test\": \"data\"}";
		_ = A.CallTo(() => _primaryStrategy.GenerateDeduplicationId(messageBody, A<IDictionary<string, object>?>._))
			.Returns("primary-id");

		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, Array.Empty<IDeduplicationStrategy>(), _options);

		// Act
		var result = composite.GenerateDeduplicationId(messageBody);

		// Assert
		result.ShouldBe("primary-id");
		_ = A.CallTo(() => _primaryStrategy.GenerateDeduplicationId(messageBody, A<IDictionary<string, object>?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GenerateIdAsyncShouldCombineIdsFromAllStrategies()
	{
		// Arrange
		var messageBody = "{\"test\": \"data\"}";
		_ = A.CallTo(() => _primaryStrategy.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>._, A<CancellationToken>._))
			.Returns("id1");
		_ = A.CallTo(() => _secondaryStrategy.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>._, A<CancellationToken>._))
			.Returns("id2");

		var secondary = new[] { _secondaryStrategy };
		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, secondary, _options);

		// Act
		var result = await composite.GenerateIdAsync(messageBody, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("id1:id2");
	}

	[Fact]
	public async Task GenerateIdAsyncShouldReturnPrimaryIdWhenNoSecondaryStrategies()
	{
		// Arrange
		var messageBody = "{\"test\": \"data\"}";
		_ = A.CallTo(() => _primaryStrategy.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>._, A<CancellationToken>._))
			.Returns("primary-only");

		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, Array.Empty<IDeduplicationStrategy>(), _options);

		// Act
		var result = await composite.GenerateIdAsync(messageBody, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("primary-only");
	}

	[Fact]
	public async Task GenerateIdAsyncShouldCallAllStrategies()
	{
		// Arrange
		var messageBody = "{\"test\": \"data\"}";
		_ = A.CallTo(() => _primaryStrategy.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>._, A<CancellationToken>._))
			.Returns("id1");
		_ = A.CallTo(() => _secondaryStrategy.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>._, A<CancellationToken>._))
			.Returns("id2");

		var secondary = new[] { _secondaryStrategy };
		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, secondary, _options);

		// Act
		_ = await composite.GenerateIdAsync(messageBody, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => _primaryStrategy.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _secondaryStrategy.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GenerateIdAsyncShouldHandleManyStrategies()
	{
		// Arrange
		var messageBody = "{\"test\": \"data\"}";
		var primary = A.Fake<IDeduplicationStrategy>();
		_ = A.CallTo(() => primary.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>._, A<CancellationToken>._))
			.Returns("key1");

		var manyStrategies = Enumerable.Range(2, 9)
			.Select(i =>
			{
				var strategy = A.Fake<IDeduplicationStrategy>();
				_ = A.CallTo(() => strategy.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>._, A<CancellationToken>._))
					.Returns($"key{i}");
				return strategy;
			})
			.ToArray();

		var composite = new CompositeDeduplicationStrategy(primary, manyStrategies, _options);

		// Act
		var result = await composite.GenerateIdAsync(messageBody, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("key1:key2:key3:key4:key5:key6:key7:key8:key9:key10");
	}

	[Fact]
	public async Task IsDuplicateAsyncShouldDelegateToPrimaryStrategy()
	{
		// Arrange
		var deduplicationId = "test-id";
		_ = A.CallTo(() => _primaryStrategy.IsDuplicateAsync(deduplicationId, A<CancellationToken>._))
			.Returns(true);

		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, Array.Empty<IDeduplicationStrategy>(), _options);

		// Act
		var result = await composite.IsDuplicateAsync(deduplicationId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		_ = A.CallTo(() => _primaryStrategy.IsDuplicateAsync(deduplicationId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IsDuplicateAsyncShouldReturnFalseWhenNotDuplicate()
	{
		// Arrange
		var deduplicationId = "test-id";
		_ = A.CallTo(() => _primaryStrategy.IsDuplicateAsync(deduplicationId, A<CancellationToken>._))
			.Returns(false);

		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, Array.Empty<IDeduplicationStrategy>(), _options);

		// Act
		var result = await composite.IsDuplicateAsync(deduplicationId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task IsDuplicateAsyncShouldPassCancellationToken()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var deduplicationId = "test-id";
		_ = A.CallTo(() => _primaryStrategy.IsDuplicateAsync(deduplicationId, cts.Token))
			.Returns(false);

		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, Array.Empty<IDeduplicationStrategy>(), _options);

		// Act
		_ = await composite.IsDuplicateAsync(deduplicationId, cts.Token).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => _primaryStrategy.IsDuplicateAsync(deduplicationId, cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkAsProcessedAsyncShouldCallPrimaryAndSecondaryStrategies()
	{
		// Arrange
		var deduplicationId = "test-id";
		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, new[] { _secondaryStrategy }, _options);

		// Act
		await composite.MarkAsProcessedAsync(deduplicationId, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => _primaryStrategy.MarkAsProcessedAsync(deduplicationId, A<TimeSpan?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _secondaryStrategy.MarkAsProcessedAsync(deduplicationId, A<TimeSpan?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkAsProcessedAsyncShouldPassCancellationToken()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var deduplicationId = "test-id";
		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, Array.Empty<IDeduplicationStrategy>(), _options);

		// Act
		await composite.MarkAsProcessedAsync(deduplicationId, null, cts.Token).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => _primaryStrategy.MarkAsProcessedAsync(deduplicationId, A<TimeSpan?>._, cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkAsProcessedAsyncShouldPassExpiration()
	{
		// Arrange
		var deduplicationId = "test-id";
		var expiration = TimeSpan.FromHours(2);
		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, Array.Empty<IDeduplicationStrategy>(), _options);

		// Act
		await composite.MarkAsProcessedAsync(deduplicationId, expiration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => _primaryStrategy.MarkAsProcessedAsync(deduplicationId, expiration, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RemoveAsyncShouldCallAllStrategies()
	{
		// Arrange
		var deduplicationId = "test-id";
		_ = A.CallTo(() => _primaryStrategy.RemoveAsync(deduplicationId, A<CancellationToken>._))
			.Returns(true);
		_ = A.CallTo(() => _secondaryStrategy.RemoveAsync(deduplicationId, A<CancellationToken>._))
			.Returns(true);

		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, new[] { _secondaryStrategy }, _options);

		// Act
		var result = await composite.RemoveAsync(deduplicationId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		_ = A.CallTo(() => _primaryStrategy.RemoveAsync(deduplicationId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _secondaryStrategy.RemoveAsync(deduplicationId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RemoveAsyncShouldReturnPrimaryResult()
	{
		// Arrange
		var deduplicationId = "test-id";
		_ = A.CallTo(() => _primaryStrategy.RemoveAsync(deduplicationId, A<CancellationToken>._))
			.Returns(false);
		_ = A.CallTo(() => _secondaryStrategy.RemoveAsync(deduplicationId, A<CancellationToken>._))
			.Returns(true);

		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, new[] { _secondaryStrategy }, _options);

		// Act
		var result = await composite.RemoveAsync(deduplicationId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse(); // Returns primary result
	}

	[Fact]
	public async Task MethodsShouldHandleConcurrentAccess()
	{
		// Arrange
		_ = A.CallTo(() => _primaryStrategy.GenerateDeduplicationId(A<string>._, A<IDictionary<string, object>?>._))
			.Returns("concurrent-id");
		_ = A.CallTo(() => _primaryStrategy.IsDuplicateAsync(A<string>._, A<CancellationToken>._))
			.Returns(false);

		var composite = new CompositeDeduplicationStrategy(_primaryStrategy, Array.Empty<IDeduplicationStrategy>(), _options);
		var tasks = new List<Task>();

		// Act
		for (var i = 0; i < 10; i++)
		{
			tasks.Add(Task.Run(async () =>
			{
				var id = composite.GenerateDeduplicationId("{\"test\": \"data\"}");
				_ = await composite.IsDuplicateAsync(id, CancellationToken.None).ConfigureAwait(false);
				await composite.MarkAsProcessedAsync(id, null, CancellationToken.None).ConfigureAwait(false);
			}));
		}

		// Assert
		await Task.WhenAll(tasks).ConfigureAwait(false);
		// Should complete without throwing
	}
}
