// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Messaging.Delivery.Inbox.Deduplication;

/// <summary>
///     Unit tests for ContentHashDeduplicationStrategy to verify content hash deduplication functionality.
/// </summary>
[Trait("Category", "Unit")]
public class ContentHashDeduplicationStrategyShould
{
	private readonly IDeduplicationStore _store;
	private readonly DeduplicationOptions _options;
	private readonly ContentHashDeduplicationStrategy _strategy;

	public ContentHashDeduplicationStrategyShould()
	{
		_store = A.Fake<IDeduplicationStore>();
		_options = new DeduplicationOptions { DeduplicationWindow = TimeSpan.FromMinutes(30) };
		_strategy = new ContentHashDeduplicationStrategy(_store, _options);
	}

	[Fact]
	public void ConstructorShouldInitializeWithStoreAndOptions()
	{
		// Arrange & Act
		var store = A.Fake<IDeduplicationStore>();
		var options = new DeduplicationOptions();
		var strategy = new ContentHashDeduplicationStrategy(store, options);

		// Assert
		_ = strategy.ShouldNotBeNull();
	}

	[Fact]
	public void ConstructorShouldThrowArgumentNullExceptionForNullStore() =>
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(static () =>
			new ContentHashDeduplicationStrategy(null!, new DeduplicationOptions()));

	[Fact]
	public void ConstructorShouldThrowArgumentNullExceptionForNullOptions() =>
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ContentHashDeduplicationStrategy(A.Fake<IDeduplicationStore>(), null!));

	[Fact]
	public void DefaultExpirationShouldReturnDeduplicationWindow()
	{
		// Assert
		_strategy.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void GenerateDeduplicationIdShouldCreateHashFromMessageBody()
	{
		// Act
		var result = _strategy.GenerateDeduplicationId("{\"property\": \"value\"}");

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.Length.ShouldBe(64); // SHA256 produces 64 hex characters
	}

	[Fact]
	public void GenerateDeduplicationIdShouldThrowArgumentNullExceptionForNullMessageBody() =>
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_strategy.GenerateDeduplicationId(null!));

	[Fact]
	public void GenerateDeduplicationIdShouldThrowArgumentNullExceptionForEmptyMessageBody() =>
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_strategy.GenerateDeduplicationId(string.Empty));

	[Fact]
	public void GenerateDeduplicationIdShouldGenerateConsistentHashForSameContent()
	{
		// Arrange
		var messageBody = "{\"data\": \"value\"}";

		// Act
		var hash1 = _strategy.GenerateDeduplicationId(messageBody);
		var hash2 = _strategy.GenerateDeduplicationId(messageBody);

		// Assert
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void GenerateDeduplicationIdShouldGenerateDifferentHashForDifferentContent()
	{
		// Arrange
		var messageBody1 = "{\"data1\": \"value1\"}";
		var messageBody2 = "{\"data2\": \"value2\"}";

		// Act
		var hash1 = _strategy.GenerateDeduplicationId(messageBody1);
		var hash2 = _strategy.GenerateDeduplicationId(messageBody2);

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void GenerateDeduplicationIdShouldHandleSpecialCharactersInContent()
	{
		// Arrange
		var messageBody = "{\"unicode\": \"æµ‹è¯•\", \"emoji\": \"ðŸŽ¯\"}";

		// Act
		var result = _strategy.GenerateDeduplicationId(messageBody);

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.Length.ShouldBe(64);
	}

	[Fact]
	public void GenerateDeduplicationIdShouldHandleLargeContent()
	{
		// Arrange
		var largeBody = new string('A', 10000);

		// Act
		var result = _strategy.GenerateDeduplicationId(largeBody);

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.Length.ShouldBe(64);
	}

	[Fact]
	public void GenerateDeduplicationIdShouldHandleWhitespaceOnlyContent()
	{
		// Arrange
		var messageBody = "   \t\n\r   ";

		// Act
		var result = _strategy.GenerateDeduplicationId(messageBody);

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.Length.ShouldBe(64);
	}

	[Fact]
	public async Task GenerateIdAsyncShouldReturnSameAsGenerateDeduplicationId()
	{
		// Arrange
		var messageBody = "{\"test\": \"data\"}";

		// Act
		var syncResult = _strategy.GenerateDeduplicationId(messageBody);
		var asyncResult = await _strategy.GenerateIdAsync(messageBody, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		asyncResult.ShouldBe(syncResult);
	}

	[Fact]
	public async Task IsDuplicateAsyncShouldReturnTrueWhenStoreContainsDuplicate()
	{
		// Arrange
		var deduplicationId = "test-id";
		var duplicateResult = new DeduplicationResult { IsDuplicate = true };
		_ = A.CallTo(() => _store.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>._, A<CancellationToken>._))
			.Returns(duplicateResult);

		// Act
		var result = await _strategy.IsDuplicateAsync(deduplicationId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		_ = A.CallTo(() => _store.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IsDuplicateAsyncShouldReturnFalseWhenNotDuplicate()
	{
		// Arrange
		var deduplicationId = "test-id";
		var notDuplicateResult = new DeduplicationResult { IsDuplicate = false };
		_ = A.CallTo(() => _store.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>._, A<CancellationToken>._))
			.Returns(notDuplicateResult);

		// Act
		var result = await _strategy.IsDuplicateAsync(deduplicationId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task IsDuplicateAsyncShouldPassCancellationToken()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var deduplicationId = "test-id";
		var result = new DeduplicationResult { IsDuplicate = false };
		_ = A.CallTo(() => _store.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>._, cts.Token))
			.Returns(result);

		// Act
		_ = await _strategy.IsDuplicateAsync(deduplicationId, cts.Token).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => _store.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>._, cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkAsProcessedAsyncShouldCallStoreCheckAndMark()
	{
		// Arrange
		var deduplicationId = "test-id";
		var result = new DeduplicationResult { IsDuplicate = false };
		_ = A.CallTo(() => _store.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>._, A<CancellationToken>._))
			.Returns(result);

		// Act
		await _strategy.MarkAsProcessedAsync(deduplicationId, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => _store.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkAsProcessedAsyncShouldPassCancellationToken()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var deduplicationId = "test-id";
		var result = new DeduplicationResult { IsDuplicate = false };
		_ = A.CallTo(() => _store.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>._, cts.Token))
			.Returns(result);

		// Act
		await _strategy.MarkAsProcessedAsync(deduplicationId, null, cts.Token).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => _store.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>._, cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RemoveAsyncShouldCallStoreRemove()
	{
		// Arrange
		var deduplicationId = "test-id";
		_ = A.CallTo(() => _store.RemoveAsync(deduplicationId, A<CancellationToken>._))
			.Returns(true);

		// Act
		var result = await _strategy.RemoveAsync(deduplicationId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		_ = A.CallTo(() => _store.RemoveAsync(deduplicationId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RemoveAsyncShouldReturnFalseWhenStoreReturnsFalse()
	{
		// Arrange
		var deduplicationId = "test-id";
		_ = A.CallTo(() => _store.RemoveAsync(deduplicationId, A<CancellationToken>._))
			.Returns(false);

		// Act
		var result = await _strategy.RemoveAsync(deduplicationId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void GenerateDeduplicationIdShouldBeThreadSafe()
	{
		// Arrange
		var messageBodies = Enumerable.Range(1, 100)
			.Select(i => $"{{\"id\": {i}}}")
			.ToList();

		var tasks = new List<Task<string>>();

		// Act
		foreach (var body in messageBodies)
		{
			tasks.Add(Task.Run(() => _strategy.GenerateDeduplicationId(body)));
		}

		var results = Task.WhenAll(tasks).Result;

		// Assert
		results.Length.ShouldBe(100);
		results.All(r => r.Length == 64).ShouldBeTrue();
		results.Distinct().Count().ShouldBe(100); // All should be unique
	}

	[Fact]
	public void GenerateDeduplicationIdShouldProduceDeterministicResults()
	{
		// Arrange
		var messageBody = "{\"test\": \"value\"}";
		var results = new List<string>();

		// Act - Generate the same ID multiple times
		for (var i = 0; i < 10; i++)
		{
			results.Add(_strategy.GenerateDeduplicationId(messageBody));
		}

		// Assert
		results.Distinct().Count().ShouldBe(1); // All results should be identical
	}

	[Fact]
	public void GenerateDeduplicationIdShouldHandleJsonWithDifferentFormatting()
	{
		// Arrange
		var messageBody1 = "{\"key\":\"value\"}";
		var messageBody2 = "{ \"key\": \"value\" }";

		// Act
		var hash1 = _strategy.GenerateDeduplicationId(messageBody1);
		var hash2 = _strategy.GenerateDeduplicationId(messageBody2);

		// Assert
		// Note: Different formatting should produce different hashes as we hash the exact string content, not parsed JSON
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public async Task MethodsShouldHandleConcurrentAccess()
	{
		// Arrange
		var result = new DeduplicationResult { IsDuplicate = false };
		_ = A.CallTo(() => _store.CheckAndMarkAsync(A<string>._, A<DeduplicationContext>._, A<CancellationToken>._))
			.Returns(result);
		var tasks = new List<Task>();

		// Act
		for (var i = 0; i < 50; i++)
		{
			var index = i;
			tasks.Add(Task.Run(async () =>
			{
				var body = $"{{\"id\": {index}}}";
				var id = _strategy.GenerateDeduplicationId(body);
				_ = await _strategy.IsDuplicateAsync(id, CancellationToken.None).ConfigureAwait(false);
				await _strategy.MarkAsProcessedAsync(id, null, CancellationToken.None).ConfigureAwait(false);
			}));
		}

		// Assert
		await Task.WhenAll(tasks).ConfigureAwait(false);
		// Should complete without throwing
	}
}
