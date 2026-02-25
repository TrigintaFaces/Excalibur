// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;

using FakeItEasy;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="ContentHashDeduplicationStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ContentHashDeduplicationStrategyShould : UnitTestBase
{
	private readonly IDeduplicationStore _fakeStore;
	private readonly DeduplicationOptions _options;
	private readonly ContentHashDeduplicationStrategy _strategy;

	public ContentHashDeduplicationStrategyShould()
	{
		_fakeStore = A.Fake<IDeduplicationStore>();
		_options = new DeduplicationOptions
		{
			DeduplicationWindow = TimeSpan.FromMinutes(5)
		};
		_strategy = new ContentHashDeduplicationStrategy(_fakeStore, _options);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsOnNullStore()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ContentHashDeduplicationStrategy(null!, _options));
	}

	[Fact]
	public void Constructor_ThrowsOnNullOptions()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ContentHashDeduplicationStrategy(_fakeStore, null!));
	}

	#endregion

	#region DefaultExpiration Tests

	[Fact]
	public void DefaultExpiration_ReturnsDeduplicationWindow()
	{
		// Assert
		_strategy.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void DefaultExpiration_ReflectsOptionsChange()
	{
		// Arrange
		var options = new DeduplicationOptions { DeduplicationWindow = TimeSpan.FromHours(1) };
		var strategy = new ContentHashDeduplicationStrategy(_fakeStore, options);

		// Assert
		strategy.DefaultExpiration.ShouldBe(TimeSpan.FromHours(1));
	}

	#endregion

	#region GenerateDeduplicationId Tests

	[Fact]
	public void GenerateDeduplicationId_ReturnsConsistentHashForSameInput()
	{
		// Arrange
		const string messageBody = "{\"orderId\": 123}";

		// Act
		var hash1 = _strategy.GenerateDeduplicationId(messageBody);
		var hash2 = _strategy.GenerateDeduplicationId(messageBody);

		// Assert
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void GenerateDeduplicationId_ReturnsDifferentHashForDifferentInput()
	{
		// Arrange
		const string messageBody1 = "{\"orderId\": 123}";
		const string messageBody2 = "{\"orderId\": 456}";

		// Act
		var hash1 = _strategy.GenerateDeduplicationId(messageBody1);
		var hash2 = _strategy.GenerateDeduplicationId(messageBody2);

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void GenerateDeduplicationId_ReturnsHexString()
	{
		// Arrange
		const string messageBody = "{\"test\": true}";

		// Act
		var hash = _strategy.GenerateDeduplicationId(messageBody);

		// Assert - SHA256 produces 64 hex characters
		hash.Length.ShouldBe(64);
		hash.All(c => char.IsLetterOrDigit(c)).ShouldBeTrue();
	}

	[Fact]
	public void GenerateDeduplicationId_ThrowsOnNullMessageBody()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_strategy.GenerateDeduplicationId(null!));
	}

	[Fact]
	public void GenerateDeduplicationId_ThrowsOnEmptyMessageBody()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_strategy.GenerateDeduplicationId(""));
	}

	[Fact]
	public void GenerateDeduplicationId_IgnoresMessageAttributes()
	{
		// Arrange
		const string messageBody = "{\"test\": true}";
		var attributes1 = new Dictionary<string, object> { ["key"] = "value1" };
		var attributes2 = new Dictionary<string, object> { ["key"] = "value2" };

		// Act
		var hash1 = _strategy.GenerateDeduplicationId(messageBody, attributes1);
		var hash2 = _strategy.GenerateDeduplicationId(messageBody, attributes2);

		// Assert - hash is based on body only
		hash1.ShouldBe(hash2);
	}

	#endregion

	#region GenerateIdAsync Tests

	[Fact]
	public async Task GenerateIdAsync_ReturnsConsistentHash()
	{
		// Arrange
		const string messageBody = "{\"orderId\": 123}";

		// Act
		var hash1 = await _strategy.GenerateIdAsync(messageBody, null, CancellationToken.None);
		var hash2 = await _strategy.GenerateIdAsync(messageBody, null, CancellationToken.None);

		// Assert
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public async Task GenerateIdAsync_ReturnsSameAsSync()
	{
		// Arrange
		const string messageBody = "{\"orderId\": 123}";

		// Act
		var syncHash = _strategy.GenerateDeduplicationId(messageBody);
		var asyncHash = await _strategy.GenerateIdAsync(messageBody, null, CancellationToken.None);

		// Assert
		syncHash.ShouldBe(asyncHash);
	}

	#endregion

	#region IsDuplicateAsync Tests

	[Fact]
	public async Task IsDuplicateAsync_ReturnsTrue_WhenStoreIndicatesDuplicate()
	{
		// Arrange
		const string deduplicationId = "abc123";
		var duplicateResult = new DeduplicationResult { IsDuplicate = true };
		_ = A.CallTo(() => _fakeStore.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>.Ignored, A<CancellationToken>.Ignored))
			.Returns(duplicateResult);

		// Act
		var result = await _strategy.IsDuplicateAsync(deduplicationId, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task IsDuplicateAsync_ReturnsFalse_WhenStoreIndicatesNew()
	{
		// Arrange
		const string deduplicationId = "abc123";
		var newResult = new DeduplicationResult { IsDuplicate = false };
		_ = A.CallTo(() => _fakeStore.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>.Ignored, A<CancellationToken>.Ignored))
			.Returns(newResult);

		// Act
		var result = await _strategy.IsDuplicateAsync(deduplicationId, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task IsDuplicateAsync_CallsStoreWithCorrectId()
	{
		// Arrange
		const string deduplicationId = "test-id-123";
		var result = new DeduplicationResult();
		_ = A.CallTo(() => _fakeStore.CheckAndMarkAsync(A<string>.Ignored, A<DeduplicationContext>.Ignored, A<CancellationToken>.Ignored))
			.Returns(result);

		// Act
		_ = await _strategy.IsDuplicateAsync(deduplicationId, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _fakeStore.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>.Ignored, A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region MarkAsProcessedAsync Tests

	[Fact]
	public async Task MarkAsProcessedAsync_CallsStore()
	{
		// Arrange
		const string deduplicationId = "test-id";
		_ = A.CallTo(() => _fakeStore.CheckAndMarkAsync(A<string>.Ignored, A<DeduplicationContext>.Ignored, A<CancellationToken>.Ignored))
			.Returns(new DeduplicationResult());

		// Act
		await _strategy.MarkAsProcessedAsync(deduplicationId, null, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _fakeStore.CheckAndMarkAsync(deduplicationId, A<DeduplicationContext>.Ignored, A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkAsProcessedAsync_PassesContext()
	{
		// Arrange
		const string deduplicationId = "test-id";
		DeduplicationContext? capturedContext = null;
		_ = A.CallTo(() => _fakeStore.CheckAndMarkAsync(A<string>.Ignored, A<DeduplicationContext>.Ignored, A<CancellationToken>.Ignored))
			.Invokes((string _, DeduplicationContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(new DeduplicationResult());

		// Act
		await _strategy.MarkAsProcessedAsync(deduplicationId, null, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.Source.ShouldBe("ContentHash");
	}

	#endregion

	#region RemoveAsync Tests

	[Fact]
	public async Task RemoveAsync_CallsStoreRemove()
	{
		// Arrange
		const string deduplicationId = "test-id";
		_ = A.CallTo(() => _fakeStore.RemoveAsync(deduplicationId, A<CancellationToken>.Ignored))
			.Returns(true);

		// Act
		var result = await _strategy.RemoveAsync(deduplicationId, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		_ = A.CallTo(() => _fakeStore.RemoveAsync(deduplicationId, A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RemoveAsync_ReturnsFalse_WhenStoreReturnsFalse()
	{
		// Arrange
		const string deduplicationId = "non-existent-id";
		_ = A.CallTo(() => _fakeStore.RemoveAsync(deduplicationId, A<CancellationToken>.Ignored))
			.Returns(false);

		// Act
		var result = await _strategy.RemoveAsync(deduplicationId, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion
}
