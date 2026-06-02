// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Audit.Internal;
using Excalibur.Dispatch;

namespace Excalibur.Tests.A3.Audit.Internal;

/// <summary>
/// Unit tests for <see cref="DefaultOutboxDispatcher"/> — the sentinel no-op
/// dispatcher registered by AddExcaliburAudit via TryAdd when no real outbox is present.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class DefaultOutboxDispatcherShould : IAsyncDisposable
{
	private readonly DefaultOutboxDispatcher _sut = new();

	[Fact]
	public async Task GetPendingMessagesAsync_ReturnsEmptyCollection()
	{
		// Act
		var result = await _sut.GetPendingMessagesAsync(CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetPendingMessagesAsync_RespectsEnumerableContract()
	{
		// Act — call twice to ensure it's a stable empty enumerable
		var result1 = await _sut.GetPendingMessagesAsync(CancellationToken.None);
		var result2 = await _sut.GetPendingMessagesAsync(CancellationToken.None);

		// Assert
		result1.ShouldBeEmpty();
		result2.ShouldBeEmpty();
	}

	[Fact]
	public async Task RunOutboxDispatchAsync_ThrowsInvalidOperationException()
	{
		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.RunOutboxDispatchAsync("test-dispatcher", CancellationToken.None));

		ex.Message.ShouldContain("AddExcaliburAudit");
		ex.Message.ShouldContain("AddExcaliburOutbox");
	}

	[Fact]
	public async Task SaveEventsAsync_ThrowsInvalidOperationException()
	{
		// Arrange
		var events = A.Fake<IReadOnlyCollection<IIntegrationEvent>>();
		var metadata = A.Fake<IMessageMetadata>();

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SaveEventsAsync(events, metadata, CancellationToken.None));

		ex.Message.ShouldContain("AddExcaliburOutbox");
	}

	[Fact]
	public async Task SaveMessagesAsync_ThrowsInvalidOperationException()
	{
		// Arrange
		var messages = A.Fake<ICollection<IOutboxMessage>>();

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SaveMessagesAsync(messages, CancellationToken.None));

		ex.Message.ShouldContain("AddExcaliburOutbox");
	}

	[Fact]
	public async Task DisposeAsync_CompletesWithoutException()
	{
		// Act & Assert — should be a no-op
		await _sut.DisposeAsync();
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();
}
