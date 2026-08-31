// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Excalibur.Dispatch;

using Excalibur.Compliance.Encryption.Decorators;
using Excalibur.Compliance.Configuration;

using Excalibur.Compliance;namespace Excalibur.Compliance.Tests.Encryption.Decorators;

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

	// ── ADMIN CAPABILITY, INVERTED FOR THE l0qpxo DENY-BY-DEFAULT SEAM ──────────────────────────────────────────
	//
	// The four arms this block replaces — Return_empty_for_failed_messages_when_inner_not_admin,
	// Return_empty_for_scheduled_messages…, Return_zero_for_cleanup…, Return_default_stats… — called
	// sut.GetAllTenantsFailedMessagesAsync / GetAllTenantsScheduledMessagesAsync / CleanupAllTenantsSentMessagesAsync / GetAllTenantsStatisticsAsync
	// directly on the decorator, and asserted that a NON-admin inner degraded to empty / zero / a default struct.
	//
	// That contract is the exact defect l0qpxo fixes. It CERTIFIED the silent fail-open: a capability going missing
	// returned an empty result with a descriptive name, so the missing capability read as "no data" instead of
	// "unsupported." Return_default_stats… asserted only ShouldNotBeNull() — inert, satisfied by any object.
	//
	// Under the ruled seam the decorator no longer declares IOutboxStoreAdmin (the methods are gone from its type,
	// which is why the old arms no longer compile). IOutboxStoreAdmin is PAYLOAD-BEARING, so it is WRAPPED, never
	// forwarded raw: it is discovered through GetService and resolves to a DECRYPTING view, or to null when the
	// inner is not an admin. Recorded not deleted; the capability is pinned in both directions, each with its
	// non-vacuity half.

	[Fact]
	public void ResolveNull_ForAdmin_WhenInnerIsNotAdmin()
	{
		// SAFETY / deny-by-default. A non-admin inner must resolve the admin capability to a HONEST null, not a
		// degraded empty result. Null tells the consumer the capability is genuinely absent; an empty list lies.
		var inner = A.Fake<IOutboxStore>(); // plain IOutboxStore — NOT IOutboxStoreAdmin
		var sut = CreateSut(inner);

		sut.GetService(typeof(IOutboxStoreAdmin)).ShouldBeNull(
			"A non-admin inner must resolve IOutboxStoreAdmin to null through the encrypting decorator. The old " +
			"contract returned an empty result set here, which silently reported 'no failed messages' when the " +
			"real answer is 'this store cannot report failed messages.'");
	}

	[Fact]
	public async Task ResolveMediatingAdmin_NeverTheRawInner_OverAnAdminInner()
	{
		// LIVENESS + the core security property, and the non-vacuity half of ResolveNull_… above.
		//
		// An admin inner must resolve to a WORKING admin view — and it must NOT be the raw inner instance. If
		// GetService forwarded the inner's own IOutboxStoreAdmin, GetAllTenantsFailedMessagesAsync would return ciphertext
		// undecrypted: a plaintext-bypass through the sixth-most-obscure capability. The resolved view must be a
		// mediating (decrypting) wrapper that still delegates the fetch to the inner.
		var inner = A.Fake<IOutboxStore>(o => o.Implements<IOutboxStoreAdmin>());
		A.CallTo(() => inner.GetService(typeof(IOutboxStoreAdmin)))
			.ReturnsLazily((Type _) => inner); // honest store: returns itself for a capability it implements
		A.CallTo(() => ((IOutboxStoreAdmin)inner).GetAllTenantsFailedMessagesAsync(A<int>._, A<DateTimeOffset?>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>([]));
		var sut = CreateSut(inner);

		var admin = sut.GetService(typeof(IOutboxStoreAdmin)) as IOutboxStoreAdmin;

		admin.ShouldNotBeNull(
			"An admin inner must resolve IOutboxStoreAdmin to a non-null view, or the ResolveNull arm is vacuous.");
		admin.ShouldNotBeSameAs(
			inner,
			"The resolved admin MUST be a mediating decrypting view, never the raw inner. Returning the inner would " +
			"hand the consumer undecrypted ciphertext through the admin surface — a plaintext bypass of the whole " +
			"encryption boundary.");

		// Delegation still works: the mediating view fetches through the inner's admin.
		_ = await admin.GetAllTenantsFailedMessagesAsync(3, null, 10, CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => ((IOutboxStoreAdmin)inner)
				.GetAllTenantsFailedMessagesAsync(3, null, 10, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Delegate_try_mark_sent_and_received_to_inner()
	{
		// Arrange -- inner must implement IOutboxStoreBatch for the decorator to delegate
		var inner = A.Fake<IOutboxStore>(o => o.Implements<IOutboxStoreBatch>());

		// FIXTURE HONESTY (l0qpxo migration). TryMarkSentAndReceivedAsync is an extension over IOutboxStore that now
		// resolves the batch capability via store.GetService(typeof(IOutboxStoreBatch)) — the `as`→GetService
		// migration. A bare FakeItEasy fake answers GetService with null even for interfaces it implements, so the
		// decorator's WrapCapability sees a non-batch inner and mediation resolves null → the extension returns
		// false. A real store returns itself for a capability it implements; the fake must too, or this arm
		// false-REDs on a fixture defect rather than the decorator.
		A.CallTo(() => inner.GetService(typeof(IOutboxStoreBatch))).Returns(inner);

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

		A.CallTo(() => ((IOutboxStoreBatch)inner).TryMarkSentAndReceivedAsync("m1", inboxEntry, A<CancellationToken>._))
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
