using Microsoft.Extensions.Logging.Abstractions;
// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Serialization;

using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Regression tests for InboxProcessor deduplication integration (Sprint 670, T.6).
/// Verifies that the InboxProcessor correctly uses IDeduplicationStore when configured,
/// and operates normally without it.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Feature", "Deduplication")]
public sealed class InboxProcessorDeduplicationShould
{
	[Fact]
	public void AcceptOptionalDeduplicationStore()
	{
		// Arrange
		var options = CreateDefaultOptions();
		var inboxStore = A.Fake<IInboxStore>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var serializer = new DispatchJsonSerializer();
		var logger = NullLogger<InboxProcessor>.Instance;
		var dedupStore = A.Fake<IDeduplicationStore>();

		// Act -- should not throw with dedup store provided
		var processor = new InboxProcessor(
			options, inboxStore, serviceProvider, serializer, logger,
			deduplicationStore: dedupStore);

		// Assert
		processor.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptNullDeduplicationStore()
	{
		// Arrange
		var options = CreateDefaultOptions();
		var inboxStore = A.Fake<IInboxStore>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var serializer = new DispatchJsonSerializer();
		var logger = NullLogger<InboxProcessor>.Instance;

		// Act -- should not throw without dedup store (opt-in behavior)
		var processor = new InboxProcessor(
			options, inboxStore, serviceProvider, serializer, logger);

		// Assert
		processor.ShouldNotBeNull();
	}

	[Fact]
	public void StoreDeduplicationStoreReference_WhenProvided()
	{
		// Arrange
		var options = CreateDefaultOptions();
		var inboxStore = A.Fake<IInboxStore>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var serializer = new DispatchJsonSerializer();
		var logger = NullLogger<InboxProcessor>.Instance;
		var dedupStore = A.Fake<IDeduplicationStore>();

		// Act
		var processor = new InboxProcessor(
			options, inboxStore, serviceProvider, serializer, logger,
			deduplicationStore: dedupStore);

		// Assert -- verify the store is actually stored (via reflection)
		var field = typeof(InboxProcessor)
			.GetField("_deduplicationStore", BindingFlags.NonPublic | BindingFlags.Instance);
		field.ShouldNotBeNull();
		field.GetValue(processor).ShouldBe(dedupStore);
	}

	[Fact]
	public async Task IsDuplicate_ReturnFalse_WhenNoDeduplicationStore()
	{
		// Arrange
		var options = CreateDefaultOptions();
		var inboxStore = A.Fake<IInboxStore>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var serializer = new DispatchJsonSerializer();
		var logger = NullLogger<InboxProcessor>.Instance;

		var processor = new InboxProcessor(
			options, inboxStore, serviceProvider, serializer, logger);

		// Access the private IsDuplicateAsync method
		var method = typeof(InboxProcessor)
			.GetMethod("IsDuplicateAsync", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("Expected IsDuplicateAsync method");

		// Act
		var result = await (Task<bool>)method.Invoke(processor, ["msg-123", CancellationToken.None])!;

		// Assert -- without dedup store, nothing is a duplicate
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task IsDuplicate_CheckStore_WhenDeduplicationStoreProvided()
	{
		// Arrange
		var options = CreateDefaultOptions();
		var inboxStore = A.Fake<IInboxStore>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var serializer = new DispatchJsonSerializer();
		var logger = NullLogger<InboxProcessor>.Instance;
		var dedupStore = A.Fake<IDeduplicationStore>();
		A.CallTo(() => dedupStore.ContainsAsync("msg-duplicate", A<CancellationToken>._))
			.Returns(true);
		A.CallTo(() => dedupStore.ContainsAsync("msg-new", A<CancellationToken>._))
			.Returns(false);

		var processor = new InboxProcessor(
			options, inboxStore, serviceProvider, serializer, logger,
			deduplicationStore: dedupStore);

		var method = typeof(InboxProcessor)
			.GetMethod("IsDuplicateAsync", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("Expected IsDuplicateAsync method");

		// Act & Assert -- duplicate message detected
		var isDuplicate = await (Task<bool>)method.Invoke(processor, ["msg-duplicate", CancellationToken.None])!;
		isDuplicate.ShouldBeTrue();

		// Act & Assert -- new message not flagged as duplicate
		var isNew = await (Task<bool>)method.Invoke(processor, ["msg-new", CancellationToken.None])!;
		isNew.ShouldBeFalse();
	}

	[Fact]
	public async Task MarkDeduplicated_CallAddAsync_WhenStoreProvided()
	{
		// Arrange
		var options = CreateDefaultOptions();
		var inboxStore = A.Fake<IInboxStore>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var serializer = new DispatchJsonSerializer();
		var logger = NullLogger<InboxProcessor>.Instance;
		var dedupStore = A.Fake<IDeduplicationStore>();

		var processor = new InboxProcessor(
			options, inboxStore, serviceProvider, serializer, logger,
			deduplicationStore: dedupStore);

		var method = typeof(InboxProcessor)
			.GetMethod("MarkDeduplicatedAsync", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("Expected MarkDeduplicatedAsync method");

		// Act
		await (Task)method.Invoke(processor, ["msg-processed", CancellationToken.None])!;

		// Assert
		A.CallTo(() => dedupStore.AddAsync("msg-processed", null, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkDeduplicated_NoOp_WhenNoStore()
	{
		// Arrange
		var options = CreateDefaultOptions();
		var inboxStore = A.Fake<IInboxStore>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var serializer = new DispatchJsonSerializer();
		var logger = NullLogger<InboxProcessor>.Instance;

		var processor = new InboxProcessor(
			options, inboxStore, serviceProvider, serializer, logger);

		var method = typeof(InboxProcessor)
			.GetMethod("MarkDeduplicatedAsync", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("Expected MarkDeduplicatedAsync method");

		// Act & Assert -- should not throw, just silently skip
		await Should.NotThrowAsync(async () =>
			await (Task)method.Invoke(processor, ["msg-123", CancellationToken.None])!);
	}

	private static IOptions<DeliveryInboxOptions> CreateDefaultOptions()
	{
		return Options.Create(new DeliveryInboxOptions
		{
			QueueCapacity = 100,
			ProducerBatchSize = 10,
		});
	}
}
