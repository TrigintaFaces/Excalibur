// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;

using FakeItEasy;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="CompositeDeduplicationStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CompositeDeduplicationStrategyShould : UnitTestBase
{
	private readonly IDeduplicationStrategy _fakePrimary;
	private readonly IDeduplicationStrategy _fakeSecondary1;
	private readonly IDeduplicationStrategy _fakeSecondary2;
	private readonly DeduplicationOptions _options;

	public CompositeDeduplicationStrategyShould()
	{
		_fakePrimary = A.Fake<IDeduplicationStrategy>();
		_fakeSecondary1 = A.Fake<IDeduplicationStrategy>();
		_fakeSecondary2 = A.Fake<IDeduplicationStrategy>();
		_options = new DeduplicationOptions
		{
			DeduplicationWindow = TimeSpan.FromMinutes(5)
		};
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsOnNullPrimary()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new CompositeDeduplicationStrategy(null!, [], _options));
	}

	[Fact]
	public void Constructor_ThrowsOnNullOptions()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new CompositeDeduplicationStrategy(_fakePrimary, [], null!));
	}

	[Fact]
	public void Constructor_AcceptsNullSecondary()
	{
		// Act - should not throw
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, null!, _options);

		// Assert
		_ = strategy.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptsEmptySecondary()
	{
		// Act
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [], _options);

		// Assert
		_ = strategy.ShouldNotBeNull();
	}

	#endregion

	#region DefaultExpiration Tests

	[Fact]
	public void DefaultExpiration_ReturnsDeduplicationWindow()
	{
		// Arrange
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [], _options);

		// Assert
		strategy.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region GenerateDeduplicationId Tests

	[Fact]
	public void GenerateDeduplicationId_DelegatesToPrimary()
	{
		// Arrange
		const string messageBody = "{\"test\": true}";
		const string expectedId = "primary-id";
		_ = A.CallTo(() => _fakePrimary.GenerateDeduplicationId(messageBody, A<IDictionary<string, object>?>.Ignored))
			.Returns(expectedId);
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [], _options);

		// Act
		var result = strategy.GenerateDeduplicationId(messageBody);

		// Assert
		result.ShouldBe(expectedId);
	}

	[Fact]
	public void GenerateDeduplicationId_PassesAttributesToPrimary()
	{
		// Arrange
		const string messageBody = "{\"test\": true}";
		var attributes = new Dictionary<string, object> { ["key"] = "value" };
		_ = A.CallTo(() => _fakePrimary.GenerateDeduplicationId(messageBody, attributes))
			.Returns("id");
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [], _options);

		// Act
		_ = strategy.GenerateDeduplicationId(messageBody, attributes);

		// Assert
		_ = A.CallTo(() => _fakePrimary.GenerateDeduplicationId(messageBody, attributes))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region GenerateIdAsync Tests

	[Fact]
	public async Task GenerateIdAsync_ReturnsPrimaryIdOnly_WhenNoSecondary()
	{
		// Arrange
		const string messageBody = "{\"test\": true}";
		const string primaryId = "primary-id";
		_ = A.CallTo(() => _fakePrimary.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>.Ignored, A<CancellationToken>.Ignored))
			.Returns(primaryId);
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [], _options);

		// Act
		var result = await strategy.GenerateIdAsync(messageBody, null, CancellationToken.None);

		// Assert
		result.ShouldBe(primaryId);
	}

	[Fact]
	public async Task GenerateIdAsync_CombinesIdsWithColon_WhenSecondaryExists()
	{
		// Arrange
		const string messageBody = "{\"test\": true}";
		_ = A.CallTo(() => _fakePrimary.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>.Ignored, A<CancellationToken>.Ignored))
			.Returns("primary");
		_ = A.CallTo(() => _fakeSecondary1.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>.Ignored, A<CancellationToken>.Ignored))
			.Returns("secondary1");
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [_fakeSecondary1], _options);

		// Act
		var result = await strategy.GenerateIdAsync(messageBody, null, CancellationToken.None);

		// Assert
		result.ShouldBe("primary:secondary1");
	}

	[Fact]
	public async Task GenerateIdAsync_CombinesAllSecondaryIds()
	{
		// Arrange
		const string messageBody = "{\"test\": true}";
		_ = A.CallTo(() => _fakePrimary.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>.Ignored, A<CancellationToken>.Ignored))
			.Returns("primary");
		_ = A.CallTo(() => _fakeSecondary1.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>.Ignored, A<CancellationToken>.Ignored))
			.Returns("secondary1");
		_ = A.CallTo(() => _fakeSecondary2.GenerateIdAsync(messageBody, A<IDictionary<string, object>?>.Ignored, A<CancellationToken>.Ignored))
			.Returns("secondary2");
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [_fakeSecondary1, _fakeSecondary2], _options);

		// Act
		var result = await strategy.GenerateIdAsync(messageBody, null, CancellationToken.None);

		// Assert
		result.ShouldBe("primary:secondary1:secondary2");
	}

	#endregion

	#region IsDuplicateAsync Tests

	[Fact]
	public async Task IsDuplicateAsync_DelegatesToPrimary()
	{
		// Arrange
		const string deduplicationId = "test-id";
		_ = A.CallTo(() => _fakePrimary.IsDuplicateAsync(deduplicationId, A<CancellationToken>.Ignored))
			.Returns(true);
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [_fakeSecondary1], _options);

		// Act
		var result = await strategy.IsDuplicateAsync(deduplicationId, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		_ = A.CallTo(() => _fakePrimary.IsDuplicateAsync(deduplicationId, A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IsDuplicateAsync_DoesNotCheckSecondary()
	{
		// Arrange
		const string deduplicationId = "test-id";
		_ = A.CallTo(() => _fakePrimary.IsDuplicateAsync(deduplicationId, A<CancellationToken>.Ignored))
			.Returns(false);
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [_fakeSecondary1], _options);

		// Act
		_ = await strategy.IsDuplicateAsync(deduplicationId, CancellationToken.None);

		// Assert - secondary should not be called
		A.CallTo(() => _fakeSecondary1.IsDuplicateAsync(A<string>.Ignored, A<CancellationToken>.Ignored))
			.MustNotHaveHappened();
	}

	#endregion

	#region MarkAsProcessedAsync Tests

	[Fact]
	public async Task MarkAsProcessedAsync_MarksInPrimary()
	{
		// Arrange
		const string deduplicationId = "test-id";
		var expiration = TimeSpan.FromHours(1);
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [], _options);

		// Act
		await strategy.MarkAsProcessedAsync(deduplicationId, expiration, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _fakePrimary.MarkAsProcessedAsync(deduplicationId, expiration, A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkAsProcessedAsync_MarksInAllSecondary()
	{
		// Arrange
		const string deduplicationId = "test-id";
		var expiration = TimeSpan.FromHours(1);
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [_fakeSecondary1, _fakeSecondary2], _options);

		// Act
		await strategy.MarkAsProcessedAsync(deduplicationId, expiration, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _fakeSecondary1.MarkAsProcessedAsync(deduplicationId, expiration, A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _fakeSecondary2.MarkAsProcessedAsync(deduplicationId, expiration, A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region RemoveAsync Tests

	[Fact]
	public async Task RemoveAsync_RemovesFromPrimary()
	{
		// Arrange
		const string deduplicationId = "test-id";
		_ = A.CallTo(() => _fakePrimary.RemoveAsync(deduplicationId, A<CancellationToken>.Ignored))
			.Returns(true);
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [], _options);

		// Act
		var result = await strategy.RemoveAsync(deduplicationId, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task RemoveAsync_RemovesFromAllSecondary()
	{
		// Arrange
		const string deduplicationId = "test-id";
		_ = A.CallTo(() => _fakePrimary.RemoveAsync(deduplicationId, A<CancellationToken>.Ignored))
			.Returns(true);
		_ = A.CallTo(() => _fakeSecondary1.RemoveAsync(deduplicationId, A<CancellationToken>.Ignored))
			.Returns(true);
		_ = A.CallTo(() => _fakeSecondary2.RemoveAsync(deduplicationId, A<CancellationToken>.Ignored))
			.Returns(false);
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [_fakeSecondary1, _fakeSecondary2], _options);

		// Act
		_ = await strategy.RemoveAsync(deduplicationId, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _fakeSecondary1.RemoveAsync(deduplicationId, A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _fakeSecondary2.RemoveAsync(deduplicationId, A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RemoveAsync_ReturnsPrimaryResult()
	{
		// Arrange
		const string deduplicationId = "test-id";
		_ = A.CallTo(() => _fakePrimary.RemoveAsync(deduplicationId, A<CancellationToken>.Ignored))
			.Returns(false);
		_ = A.CallTo(() => _fakeSecondary1.RemoveAsync(deduplicationId, A<CancellationToken>.Ignored))
			.Returns(true); // Secondary returns true, but primary returns false
		var strategy = new CompositeDeduplicationStrategy(_fakePrimary, [_fakeSecondary1], _options);

		// Act
		var result = await strategy.RemoveAsync(deduplicationId, CancellationToken.None);

		// Assert - primary result is returned
		result.ShouldBeFalse();
	}

	#endregion
}
