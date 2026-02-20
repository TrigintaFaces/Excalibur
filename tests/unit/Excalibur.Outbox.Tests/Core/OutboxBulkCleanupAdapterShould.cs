// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Outbox.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxBulkCleanupAdapterShould
{
	private readonly IOutboxStoreAdmin _admin = A.Fake<IOutboxStoreAdmin>();
	private readonly NullLogger<OutboxBulkCleanupAdapter> _logger = NullLogger<OutboxBulkCleanupAdapter>.Instance;

	[Fact]
	public void ThrowWhenAdminIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new OutboxBulkCleanupAdapter(null!, _logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new OutboxBulkCleanupAdapter(_admin, null!));
	}

	[Fact]
	public async Task BulkCleanupSentMessagesDeletesAllBatches()
	{
		// Arrange — two batches of 10 + final batch of 5
		var callCount = 0;
#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy pattern
		A.CallTo(() => _admin.CleanupSentMessagesAsync(A<DateTimeOffset>._, A<int>._, CancellationToken.None))
			.ReturnsLazily(() =>
			{
				callCount++;
				return new ValueTask<int>(callCount <= 1 ? 10 : 5);
			});
#pragma warning restore CA2012
		var sut = new OutboxBulkCleanupAdapter(_admin, _logger);
		var olderThan = DateTimeOffset.UtcNow.AddDays(-1);

		// Act
		var total = await sut.BulkCleanupSentMessagesAsync(olderThan, 10, CancellationToken.None);

		// Assert
		total.ShouldBe(15);
	}

	[Fact]
	public async Task BulkCleanupSentMessagesReturnsZeroWhenNothingToClean()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _admin.CleanupSentMessagesAsync(A<DateTimeOffset>._, A<int>._, CancellationToken.None))
			.Returns(new ValueTask<int>(0));
#pragma warning restore CA2012
		var sut = new OutboxBulkCleanupAdapter(_admin, _logger);

		// Act
		var total = await sut.BulkCleanupSentMessagesAsync(DateTimeOffset.UtcNow, 100, CancellationToken.None);

		// Assert
		total.ShouldBe(0);
	}

	[Fact]
	public async Task BulkCleanupSentMessagesThrowsOnZeroBatchSize()
	{
		// Arrange
		var sut = new OutboxBulkCleanupAdapter(_admin, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await sut.BulkCleanupSentMessagesAsync(DateTimeOffset.UtcNow, 0, CancellationToken.None));
	}

	[Fact]
	public async Task BulkCleanupFailedMessagesThrowsOnZeroBatchSize()
	{
		// Arrange
		var sut = new OutboxBulkCleanupAdapter(_admin, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await sut.BulkCleanupFailedMessagesAsync(3, DateTimeOffset.UtcNow, 0, CancellationToken.None));
	}

	[Fact]
	public async Task BulkCleanupFailedMessagesReturnsZeroWhenNoFailed()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _admin.GetFailedMessagesAsync(A<int>._, A<DateTimeOffset?>._, A<int>._, CancellationToken.None))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>()));
#pragma warning restore CA2012
		var sut = new OutboxBulkCleanupAdapter(_admin, _logger);

		// Act
		var total = await sut.BulkCleanupFailedMessagesAsync(3, DateTimeOffset.UtcNow, 10, CancellationToken.None);

		// Assert
		total.ShouldBe(0);
	}

	[Fact]
	public void ImplementIOutboxBulkCleanup()
	{
		// Arrange & Act
		var sut = new OutboxBulkCleanupAdapter(_admin, _logger);

		// Assert
		sut.ShouldBeAssignableTo<IOutboxBulkCleanup>();
	}
}
