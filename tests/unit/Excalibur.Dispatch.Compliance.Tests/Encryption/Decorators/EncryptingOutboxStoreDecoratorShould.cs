// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption.Decorators;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingOutboxStoreDecoratorShould
{
	private readonly IEncryptionProviderRegistry _registry = A.Fake<IEncryptionProviderRegistry>();
	private readonly IEncryptionProvider _provider = A.Fake<IEncryptionProvider>();

	[Fact]
	public async Task Encrypt_payload_on_stage_message_when_encrypt_mode()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner, EncryptionMode.EncryptAndDecrypt);

		A.CallTo(() => _registry.GetPrimary()).Returns(_provider);
		A.CallTo(() => _provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = [99],
				Iv = new byte[12],
				KeyId = "k1",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
			}));

		var message = new OutboundMessage
		{
			Id = "m1",
			MessageType = "type",
			Payload = [1, 2, 3],
			CreatedAt = DateTimeOffset.UtcNow,
		};

		// Act
		await sut.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert — encryption was invoked
		A.CallTo(() => _provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => inner.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Throw_on_stage_message_in_read_only_mode()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner, EncryptionMode.DecryptOnlyReadOnly);

		var message = new OutboundMessage
		{
			Id = "m1",
			MessageType = "type",
			Payload = [1, 2, 3],
			CreatedAt = DateTimeOffset.UtcNow,
		};

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await sut.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_enqueue_in_read_only_mode()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner, EncryptionMode.DecryptOnlyReadOnly);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await sut.EnqueueAsync(message, context, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Delegate_enqueue_to_inner_when_encrypt_mode()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner, EncryptionMode.EncryptAndDecrypt);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		await sut.EnqueueAsync(message, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => inner.EnqueueAsync(message, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Return_unsent_messages_unchanged_when_not_encrypted()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner, EncryptionMode.EncryptAndDecrypt);

		var messages = new[]
		{
			new OutboundMessage
			{
				Id = "m1",
				MessageType = "type",
				Payload = [1, 2, 3], // Not encrypted (no magic prefix)
				CreatedAt = DateTimeOffset.UtcNow,
			},
		};

		A.CallTo(() => inner.GetUnsentMessagesAsync(10, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(messages));

		// Act
		var result = await sut.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var resultList = result.ToList();
		resultList.Count.ShouldBe(1);
		resultList[0].Payload.ShouldBe(new byte[] { 1, 2, 3 });
	}

	[Fact]
	public async Task Return_messages_unmodified_when_disabled_mode()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner, EncryptionMode.Disabled);

		var messages = new[]
		{
			new OutboundMessage
			{
				Id = "m1",
				MessageType = "type",
				Payload = [1, 2, 3],
				CreatedAt = DateTimeOffset.UtcNow,
			},
		};

		A.CallTo(() => inner.GetUnsentMessagesAsync(10, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(messages));

		// Act
		var result = await sut.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(messages);
	}

	[Fact]
	public async Task Delegate_mark_sent_to_inner()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner);

		// Act
		await sut.MarkSentAsync("m1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => inner.MarkSentAsync("m1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Delegate_mark_failed_to_inner()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner);

		// Act
		await sut.MarkFailedAsync("m1", "error", 1, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => inner.MarkFailedAsync("m1", "error", 1, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Return_empty_for_failed_messages_when_inner_not_admin()
	{
		// Arrange — use a plain IOutboxStore (not IOutboxStoreAdmin)
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner);

		// Act
		var result = await sut.GetFailedMessagesAsync(3, null, 10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task Return_empty_for_scheduled_messages_when_inner_not_admin()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner);

		// Act
		var result = await sut.GetScheduledMessagesAsync(DateTimeOffset.UtcNow, 10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task Return_zero_for_cleanup_when_inner_not_admin()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner);

		// Act
		var result = await sut.CleanupSentMessagesAsync(DateTimeOffset.UtcNow, 10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task Return_default_stats_when_inner_not_admin()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner);

		// Act
		var result = await sut.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task Delegate_try_mark_sent_and_received_to_inner()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner);
		var inboxEntry = new InboxEntry
		{
			MessageId = "m1",
			HandlerType = "handler",
			MessageType = "type",
			Payload = [1],
			Status = InboxStatus.Received,
			ReceivedAt = DateTimeOffset.UtcNow,
		};

		A.CallTo(() => inner.TryMarkSentAndReceivedAsync("m1", inboxEntry, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(true));

		// Act
		var result = await sut.TryMarkSentAndReceivedAsync("m1", inboxEntry, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void Throw_for_null_inner()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new EncryptionOptions());
		Should.Throw<ArgumentNullException>(() =>
			new EncryptingOutboxStoreDecorator(null!, _registry, options));
	}

	[Fact]
	public void Throw_for_null_registry()
	{
		var inner = A.Fake<IOutboxStore>();
		var options = Microsoft.Extensions.Options.Options.Create(new EncryptionOptions());
		Should.Throw<ArgumentNullException>(() =>
			new EncryptingOutboxStoreDecorator(inner, null!, options));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		var inner = A.Fake<IOutboxStore>();
		Should.Throw<ArgumentNullException>(() =>
			new EncryptingOutboxStoreDecorator(inner, _registry, null!));
	}

	[Fact]
	public async Task Throw_on_null_message_for_stage()
	{
		// Arrange
		var inner = A.Fake<IOutboxStore>();
		var sut = CreateSut(inner);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.StageMessageAsync(null!, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public void Have_default_encryption_options_values()
	{
		var options = new EncryptionOptions();
		options.DefaultPurpose.ShouldBe("default");
		options.RequireFipsCompliance.ShouldBeFalse();
		options.DefaultTenantId.ShouldBeNull();
		options.IncludeTimingMetadata.ShouldBeTrue();
		options.EncryptionAgeWarningThreshold.ShouldBeNull();
		options.Mode.ShouldBe(EncryptionMode.EncryptAndDecrypt);
		options.LazyMigrationEnabled.ShouldBeFalse();
		options.LazyMigrationMode.ShouldBe(LazyMigrationMode.Both);
	}

	[Fact]
	public void Have_default_migration_options_values()
	{
		var options = new EncryptionMigrationOptions();
		options.BatchSize.ShouldBe(100);
		options.MaxDegreeOfParallelism.ShouldBe(4);
		options.ContinueOnError.ShouldBeFalse();
		options.DelayBetweenBatches.ShouldBe(TimeSpan.Zero);
		options.SourceProviderId.ShouldBeNull();
		options.TargetProviderId.ShouldBeNull();
		options.VerifyBeforeReEncrypt.ShouldBeTrue();
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	private EncryptingOutboxStoreDecorator CreateSut(
																					IOutboxStore inner,
		EncryptionMode mode = EncryptionMode.EncryptAndDecrypt)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new EncryptionOptions
		{
			Mode = mode,
			DefaultPurpose = "test",
		});
		return new EncryptingOutboxStoreDecorator(inner, _registry, options);
	}
}
