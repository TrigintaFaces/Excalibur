// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Encryption.Decorators;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Encryption.Decorators;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class EncryptingOutboxCallerOwnershipShould(ITestOutputHelper output)
{
	[Theory]
	[InlineData(0, EncryptionMode.EncryptAndDecrypt)]
	[InlineData(1, EncryptionMode.EncryptAndDecrypt)]
	[InlineData(2, EncryptionMode.EncryptAndDecrypt)]
	[InlineData(0, EncryptionMode.EncryptNewDecryptAll)]
	[InlineData(1, EncryptionMode.EncryptNewDecryptAll)]
	[InlineData(2, EncryptionMode.EncryptNewDecryptAll)]
	public async Task KeepTheCallerPlaintextDuringAnInnerWaitAndAllowTheSameObjectToBeRetried(int route, EncryptionMode mode)
	{
		using var harness = new Harness(mode);
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var message = Message(harness.Marker);
		var inbox = Inbox(harness.Marker);
		var external = new List<OutboundMessageTransport> { Transport("external") };
		var payload = route == 2 ? inbox.Payload : message.Payload;
		var innerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		harness.BeforeWrite = () => innerEntered.TrySetResult();
		harness.WriteTask = release.Task;
		harness.BatchTask = WaitForReleaseAsync(release.Task);
		var operation = harness.InvokeAsync(route, message, inbox, external, deadline.Token);
		Exception? primary = null;
		Exception? operationFailure = null;
		try
		{
			await innerEntered.Task.WaitAsync(deadline.Token);
			CallerPayload(route, message, inbox).ShouldBeSameAs(payload);
			payload.ShouldBe(new byte[] { 1, 2, 255, 0 });
			AssertStoredFields(harness, route, message, inbox);
			if (route == 1)
			{
				harness.PassedTransports.ShouldNotBeSameAs(external);
				var passed = harness.StoredTransports.ShouldHaveSingleItem();
				passed.ShouldNotBeSameAs(external[0]);
				AssertTransport(external[0], passed);
			}
			EncryptedData.IsFieldEncrypted(harness.StoredPayload(route)).ShouldBeTrue();
		}
		catch (Exception error) { primary = error; }
		finally { operationFailure = await ReleaseAndJoinAsync(release, operation, harness); }
		RethrowPreservingPrimary(primary, operationFailure);
		harness.LastBatchResult.ShouldBe(route == 2);
		(await harness.DecryptAsync(harness.StoredPayload(route), deadline.Token)).ShouldBe(payload);
		CallerPayload(route, message, inbox).ShouldBeSameAs(payload);
		harness.WriteTask = Task.CompletedTask;
		harness.BatchTask = Task.FromResult(true);
		await harness.InvokeAsync(route, message, inbox, external, deadline.Token);
		harness.Writes.ShouldBe(2);
		(await harness.DecryptAsync(harness.StoredPayload(route), deadline.Token)).ShouldBe(new byte[] { 1, 2, 255, 0 },
			"retrying the same caller envelope must not encrypt an already encrypted envelope again");
		CallerPayload(route, message, inbox).ShouldBeSameAs(payload);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task SnapshotMutableInputContainersAndEveryFieldBeforeEncryptionAwaits(int route)
	{
		using var harness = new Harness();
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var message = Message(harness.Marker);
		var inbox = Inbox(harness.Marker);
		var external = new List<OutboundMessageTransport> { Transport("external") };
		var originalPayload = CallerPayload(route, message, inbox);
		var source = new SingleUseTransports(external);
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		harness.Encrypt = async (bytes, context, token) =>
		{
			entered.TrySetResult();
			await release.Task.WaitAsync(token);
			return await harness.RealProvider.EncryptAsync(bytes, context, token);
		};
		var operation = harness.InvokeAsync(route, message, inbox, source, deadline.Token);
		Exception? primary = null;
		Exception? operationFailure = null;
		try
		{
			await entered.Task.WaitAsync(deadline.Token);
			if (route == 1) { source.EnumerationCount.ShouldBe(1, "the consumer sequence is consumed exactly once BEFORE encryption suspends"); }
			originalPayload[0] = 77;
			message.Id = "changed";
			message.TenantId = "changed-tenant";
			message.SequenceNumber = 999;
			message.Headers["custom"] = "changed";
			message.TransportDeliveries.Single().Destination = "changed";
			message.TransportDeliveries.Clear();
			inbox.MessageId = "changed";
			inbox.TenantId = "changed-tenant";
			inbox.Source = "changed";
			inbox.Metadata["custom"] = "changed";
			external[0].Destination = "changed";
			external.Clear();
		}
		catch (Exception error) { primary = error; }
		finally { operationFailure = await ReleaseAndJoinAsync(release, operation, harness); }
		RethrowPreservingPrimary(primary, operationFailure);
		AssertStoredFields(harness, route, Message(harness.Marker), Inbox(harness.Marker));
		(await harness.DecryptAsync(harness.StoredPayload(route), deadline.Token)).ShouldBe(new byte[] { 1, 2, 255, 0 });
		CallerPayload(route, message, inbox).ShouldBeSameAs(originalPayload);
		originalPayload[0].ShouldBe((byte)77, "only this caller's deliberate mutation may affect its buffer");
		if (route == 1)
		{
			harness.PassedTransports.ShouldNotBeSameAs(source);
			source.EnumerationCount.ShouldBe(1);
			AssertTransport(Transport("external"), harness.StoredTransports.ShouldHaveSingleItem());
		}
	}

	[Theory]
	[InlineData(0, false, false)]
	[InlineData(1, false, false)]
	[InlineData(2, false, false)]
	[InlineData(0, true, false)]
	[InlineData(1, true, false)]
	[InlineData(2, true, false)]
	[InlineData(0, false, true)]
	[InlineData(1, false, true)]
	[InlineData(2, false, true)]
	[InlineData(0, true, true)]
	[InlineData(1, true, true)]
	[InlineData(2, true, true)]
	public async Task PreserveCallerOwnershipAndExactFailureOrCancellationAtEachBoundary(int route, bool innerFailure, bool cancel)
	{
		using var harness = new Harness();
		using var cancellation = new CancellationTokenSource();
		var message = Message(harness.Marker);
		var inbox = Inbox(harness.Marker);
		var payload = CallerPayload(route, message, inbox);
		var expected = new IOException("arranged exact failure");
		if (innerFailure)
		{
			// Encryption succeeds independently; arrange the inner task's exact fault/cancellation.
			harness.Encrypt = (bytes, context, _) => harness.RealProvider.EncryptAsync(bytes, context, CancellationToken.None);
			if (cancel)
			{
				harness.BeforeWrite = () =>
				{
					cancellation.Cancel();
					harness.WriteTask = Task.FromCanceled(cancellation.Token);
					harness.BatchTask = Task.FromCanceled<bool>(cancellation.Token);
				};
			}
			else
			{
				if (route == 2) { harness.BatchTask = Task.FromException<bool>(expected); }
				else { harness.WriteTask = Task.FromException(expected); }
			}
		}
		else
		{
			harness.Encrypt = (_, _, _) =>
			{
				if (!cancel) { return Task.FromException<EncryptedData>(expected); }
				cancellation.Cancel();
				return Task.FromCanceled<EncryptedData>(cancellation.Token);
			};
		}
		if (cancel)
		{
			var error = await Should.ThrowAsync<OperationCanceledException>(() => harness.InvokeAsync(route, message, inbox,
				[Transport("external")], cancellation.Token));
			error.CancellationToken.ShouldBe(cancellation.Token);
		}
		else
		{
			(await Should.ThrowAsync<IOException>(() => harness.InvokeAsync(route, message, inbox,
				[Transport("external")], cancellation.Token))).ShouldBeSameAs(expected);
		}
		CallerPayload(route, message, inbox).ShouldBeSameAs(payload);
		payload.ShouldBe(new byte[] { 1, 2, 255, 0 });
		harness.Writes.ShouldBe(innerFailure ? 1 : 0);
		if (innerFailure) { harness.ForwardedToken.ShouldBe(cancellation.Token); }
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task RefuseReadOnlyWritesWithoutTouchingCallerOrInvokingInner(int route)
	{
		using var harness = new Harness(EncryptionMode.DecryptOnlyReadOnly);
		var message = Message(harness.Marker);
		var inbox = Inbox(harness.Marker);
		var payload = CallerPayload(route, message, inbox);
		var enumerationCalls = 0;
		var forbiddenSequence = Enumerable.Range(0, 1).Select<int, OutboundMessageTransport>(_ =>
		{
			enumerationCalls++;
			throw new IOException("read-only refusal must precede consumer transport enumeration");
		});
		await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync(route, message, inbox,
			forbiddenSequence, CancellationToken.None));
		enumerationCalls.ShouldBe(0);
		CallerPayload(route, message, inbox).ShouldBeSameAs(payload);
		payload.ShouldBe(new byte[] { 1, 2, 255, 0 });
		harness.Writes.ShouldBe(0);
		harness.EncryptCalls.ShouldBe(0);
	}

	[Fact]
	public async Task PreserveAFalseBatchOutcomeWithoutMutatingTheInboxCaller()
	{
		using var harness = new Harness();
		var message = Message(harness.Marker);
		var inbox = Inbox(harness.Marker);
		var payload = inbox.Payload;
		harness.BatchTask = Task.FromResult(false);
		await harness.InvokeAsync(2, message, inbox, [], CancellationToken.None);
		harness.LastBatchResult.ShouldBeFalse();
		harness.Writes.ShouldBe(1);
		inbox.Payload.ShouldBeSameAs(payload);
		payload.ShouldBe(new byte[] { 1, 2, 255, 0 });
		AssertStoredFields(harness, 2, message, inbox);
	}

	[Theory]
	[InlineData(0, EncryptionMode.Disabled)]
	[InlineData(1, EncryptionMode.Disabled)]
	[InlineData(2, EncryptionMode.Disabled)]
	[InlineData(0, EncryptionMode.DecryptOnlyWritePlaintext)]
	[InlineData(1, EncryptionMode.DecryptOnlyWritePlaintext)]
	[InlineData(2, EncryptionMode.DecryptOnlyWritePlaintext)]
	public async Task PreserveDisabledModePassThroughReferencesWithoutEncryption(int route, EncryptionMode mode)
	{
		using var harness = new Harness(mode);
		var message = Message(harness.Marker);
		var inbox = Inbox(harness.Marker);
		var payload = CallerPayload(route, message, inbox);
		var external = new List<OutboundMessageTransport> { Transport("external") };
		await harness.InvokeAsync(route, message, inbox, external, CancellationToken.None);
		CallerPayload(route, message, inbox).ShouldBeSameAs(payload);
		harness.StoredPayload(route).ShouldBeSameAs(payload);
		if (route == 2) { harness.StoredInbox.ShouldBeSameAs(inbox); }
		else { harness.StoredMessage.ShouldBeSameAs(message); }
		if (route == 1) { harness.PassedTransports.ShouldBeSameAs(external); }
		harness.EncryptCalls.ShouldBe(0);
		harness.Writes.ShouldBe(1);
	}

	[Theory]
	[InlineData(0, -1)]
	[InlineData(1, -1)]
	[InlineData(2, -1)]
	[InlineData(0, 999)]
	[InlineData(1, 999)]
	[InlineData(2, 999)]
	public async Task RefuseUnknownModesBeforeProviderEnumerationOrInnerWrites(int route, int unknown)
	{
		using var harness = new Harness((EncryptionMode)unknown);
		var message = Message(harness.Marker);
		var inbox = Inbox(harness.Marker);
		var payload = CallerPayload(route, message, inbox);
		var enumerationCalls = 0;
		var forbidden = Enumerable.Range(0, 1).Select<int, OutboundMessageTransport>(_ =>
		{
			enumerationCalls++;
			throw new IOException("invalid configuration must not enumerate caller transports");
		});
		var error = await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync(route, message, inbox, forbidden, CancellationToken.None));
		error.Message.ShouldContain("mode", Case.Insensitive);
		CallerPayload(route, message, inbox).ShouldBeSameAs(payload);
		payload.ShouldBe(new byte[] { 1, 2, 255, 0 });
		harness.EncryptCalls.ShouldBe(0);
		harness.Writes.ShouldBe(0);
		enumerationCalls.ShouldBe(0);
	}

	private static async Task<Exception?> ReleaseAndJoinAsync(TaskCompletionSource release, Task operation, Harness harness)
	{
		release.TrySetResult();
		// An independent cleanup deadline still allows a join when the assertion deadline expired.
		using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		try { await operation.WaitAsync(cleanup.Token); return null; }
		catch (Exception error)
		{
			// If the bounded join times out, keep AES/keys alive until the actual child completes.
			// The test still fails; it must never dispose resources underneath an unfinished operation.
			if (!operation.IsCompleted) { harness.DeferDisposalUntil(operation); }
			return error;
		}
	}

	private void RethrowPreservingPrimary(Exception? primary, Exception? operationFailure)
	{
		if (primary is not null)
		{
			if (operationFailure is not null) { output.WriteLine("SECONDARY_OPERATION_FAILURE: " + operationFailure.GetType().FullName); }
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
		}
		if (operationFailure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(operationFailure).Throw(); }
	}

	private sealed class SingleUseTransports(IReadOnlyList<OutboundMessageTransport> items) : IEnumerable<OutboundMessageTransport>
	{
		internal int EnumerationCount { get; private set; }
		public IEnumerator<OutboundMessageTransport> GetEnumerator()
		{
			EnumerationCount++;
			if (EnumerationCount != 1) { throw new InvalidOperationException("Caller transport sequence enumerated more than once."); }
			return items.GetEnumerator();
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private static async Task<bool> WaitForReleaseAsync(Task release) { await release; return true; }
	private static byte[] CallerPayload(int route, OutboundMessage message, InboxEntry inbox) => route == 2 ? inbox.Payload : message.Payload;
	private static readonly DateTimeOffset Instant = new(2026, 11, 13, 4, 5, 6, TimeSpan.FromHours(5.5));
	private static OutboundMessage Message(object marker)
	{
		var message = new OutboundMessage("message-type", [1, 2, 255, 0], "destination",
			new Dictionary<string, object>(StringComparer.Ordinal) { ["custom"] = "value", ["opaque"] = marker })
		{
			Id = "message", CreatedAt = Instant, ScheduledAt = Instant.AddMinutes(1), SentAt = Instant.AddMinutes(2),
			Status = OutboxStatus.Failed, RetryCount = 3, LastError = "last-error", LastAttemptAt = Instant.AddMinutes(3),
			DispatcherId = "opaque-receipt", CorrelationId = "correlation", CausationId = "causation", TenantId = "tenant",
			Priority = 7, PartitionKey = "partition", GroupKey = "group", SequenceNumber = 42,
			TargetTransports = "first,second", IsMultiTransport = true,
		};
		message.TransportDeliveries.Add(Transport("embedded"));
		return message;
	}
	private static InboxEntry Inbox(object marker) => new("inbox", "handler", "inbox-type", [1, 2, 255, 0],
		new Dictionary<string, object>(StringComparer.Ordinal) { ["custom"] = "value", ["opaque"] = marker })
	{
		ReceivedAt = Instant, ProcessedAt = Instant.AddMinutes(1), Status = InboxStatus.Failed,
		LastError = "inbox-error", RetryCount = 4, LastAttemptAt = Instant.AddMinutes(2), NextAttemptAt = Instant.AddMinutes(3),
		CorrelationId = "inbox-correlation", CausationId = "inbox-causation", TenantId = "tenant", Source = "source",
	};
	private static OutboundMessageTransport Transport(string id) => new()
	{
		Id = id, MessageId = "message", TransportName = "transport", Destination = "transport-destination",
		Status = TransportDeliveryStatus.Failed, CreatedAt = Instant, AttemptedAt = Instant.AddMinutes(1),
		SentAt = Instant.AddMinutes(2), RetryCount = 5, LastError = "transport-error", TransportMetadata = "metadata",
	};

	private static void AssertStoredFields(Harness harness, int route, OutboundMessage expected, InboxEntry inbox)
	{
		if (route == 2)
		{
			var actual = harness.StoredInbox.ShouldNotBeNull();
			actual.ShouldNotBeSameAs(inbox);
			actual.MessageId.ShouldBe(inbox.MessageId); actual.HandlerType.ShouldBe(inbox.HandlerType); actual.MessageType.ShouldBe(inbox.MessageType);
			AssertInstant(inbox.ReceivedAt, actual.ReceivedAt); AssertInstant(inbox.ProcessedAt, actual.ProcessedAt);
			actual.Status.ShouldBe(inbox.Status); actual.LastError.ShouldBe(inbox.LastError); actual.RetryCount.ShouldBe(inbox.RetryCount);
			AssertInstant(inbox.LastAttemptAt, actual.LastAttemptAt); AssertInstant(inbox.NextAttemptAt, actual.NextAttemptAt);
			actual.CorrelationId.ShouldBe(inbox.CorrelationId); actual.CausationId.ShouldBe(inbox.CausationId);
			actual.TenantId.ShouldBe(inbox.TenantId); actual.Source.ShouldBe(inbox.Source);
			actual.Metadata.ShouldNotBeSameAs(inbox.Metadata); actual.Metadata.Count.ShouldBe(2);
			actual.Metadata["custom"].ShouldBe("value"); actual.Metadata["opaque"].ShouldBeSameAs(harness.Marker);
			harness.BatchMessageId.ShouldBe("outbox-target");
			return;
		}
		var stored = harness.StoredMessage.ShouldNotBeNull();
		stored.ShouldNotBeSameAs(expected);
		stored.Id.ShouldBe(expected.Id); stored.MessageType.ShouldBe(expected.MessageType); stored.Destination.ShouldBe(expected.Destination);
		AssertInstant(expected.CreatedAt, stored.CreatedAt); AssertInstant(expected.ScheduledAt, stored.ScheduledAt); AssertInstant(expected.SentAt, stored.SentAt);
		stored.Status.ShouldBe(expected.Status); stored.RetryCount.ShouldBe(expected.RetryCount); stored.LastError.ShouldBe(expected.LastError);
		AssertInstant(expected.LastAttemptAt, stored.LastAttemptAt); stored.DispatcherId.ShouldBe(expected.DispatcherId);
		stored.CorrelationId.ShouldBe(expected.CorrelationId); stored.CausationId.ShouldBe(expected.CausationId); stored.TenantId.ShouldBe(expected.TenantId);
		stored.Priority.ShouldBe(expected.Priority); stored.PartitionKey.ShouldBe(expected.PartitionKey); stored.GroupKey.ShouldBe(expected.GroupKey);
		stored.SequenceNumber.ShouldBe(expected.SequenceNumber); stored.TargetTransports.ShouldBe(expected.TargetTransports); stored.IsMultiTransport.ShouldBe(expected.IsMultiTransport);
		stored.Headers.ShouldNotBeSameAs(expected.Headers); stored.Headers.Count.ShouldBe(2);
		stored.Headers["custom"].ShouldBe("value"); stored.Headers["opaque"].ShouldBeSameAs(harness.Marker);
		var originalDelivery = expected.TransportDeliveries.ShouldHaveSingleItem();
		var actualDelivery = stored.TransportDeliveries.ShouldHaveSingleItem();
		actualDelivery.ShouldNotBeSameAs(originalDelivery); AssertTransport(originalDelivery, actualDelivery);
	}
	private static void AssertInstant(DateTimeOffset? expected, DateTimeOffset? actual)
	{
		actual.ShouldBe(expected);
		actual!.Value.Offset.ShouldBe(expected!.Value.Offset);
	}
	private static void AssertTransport(OutboundMessageTransport expected, OutboundMessageTransport actual)
	{
		actual.Id.ShouldBe(expected.Id); actual.MessageId.ShouldBe(expected.MessageId); actual.TransportName.ShouldBe(expected.TransportName);
		actual.Destination.ShouldBe(expected.Destination); actual.Status.ShouldBe(expected.Status); AssertInstant(expected.CreatedAt, actual.CreatedAt);
		AssertInstant(expected.AttemptedAt, actual.AttemptedAt); AssertInstant(expected.SentAt, actual.SentAt);
		actual.RetryCount.ShouldBe(expected.RetryCount); actual.LastError.ShouldBe(expected.LastError); actual.TransportMetadata.ShouldBe(expected.TransportMetadata);
	}

	private sealed class Harness : IDisposable
	{
		private readonly InMemoryKeyManagementProvider _keys = new(NullLogger<InMemoryKeyManagementProvider>.Instance,
			new InMemoryKeyManagementOptions { AutoGenerateDefaultKey = true });
		private readonly EncryptingOutboxStoreDecorator _sut;
		internal AesGcmEncryptionProvider RealProvider { get; }
		internal object Marker { get; } = new();
		internal Func<byte[], EncryptionContext, CancellationToken, Task<EncryptedData>> Encrypt { get; set; }
		internal Task WriteTask { get; set; } = Task.CompletedTask;
		internal Task<bool> BatchTask { get; set; } = Task.FromResult(true);
		internal Action? BeforeWrite { get; set; }
		internal OutboundMessage? StoredMessage { get; private set; }
		internal InboxEntry? StoredInbox { get; private set; }
		internal IReadOnlyList<OutboundMessageTransport> StoredTransports { get; private set; } = [];
		internal IEnumerable<OutboundMessageTransport>? PassedTransports { get; private set; }
		internal int Writes { get; private set; }
		internal int EncryptCalls { get; private set; }
		internal bool LastBatchResult { get; private set; }
		internal string? BatchMessageId { get; private set; }
		internal CancellationToken ForwardedToken { get; private set; }
		internal Harness(EncryptionMode mode = EncryptionMode.EncryptAndDecrypt)
		{
			RealProvider = new AesGcmEncryptionProvider(_keys, NullLogger<AesGcmEncryptionProvider>.Instance);
			Encrypt = RealProvider.EncryptAsync;
			var encryption = A.Fake<IEncryptionProvider>();
			A.CallTo(() => encryption.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
				.ReturnsLazily((byte[] bytes, EncryptionContext context, CancellationToken token) =>
				{
					EncryptCalls++;
					return Encrypt(bytes, context, token);
				});
			var registry = A.Fake<IEncryptionProviderRegistry>();
			A.CallTo(() => registry.GetPrimary()).Returns(encryption);
			A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(RealProvider);
			var inner = A.Fake<IOutboxStore>(options => options.Implements<IOutboxStoreBatch>().Implements<IMultiTransportOutboxStore>());
			A.CallTo(() => inner.GetService(A<Type>._)).ReturnsLazily((Type type) => type.IsInstanceOfType(inner) ? inner : null);
			A.CallTo(() => inner.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
				.ReturnsLazily((OutboundMessage message, CancellationToken token) =>
				{
					StoredMessage = message; ForwardedToken = token; Writes++; BeforeWrite?.Invoke();
					return new ValueTask(WriteTask);
				});
			var multi = (IMultiTransportOutboxStore)inner;
			A.CallTo(() => multi.StageMessageWithTransportsAsync(A<OutboundMessage>._, A<IEnumerable<OutboundMessageTransport>>._, A<CancellationToken>._))
				.ReturnsLazily((OutboundMessage message, IEnumerable<OutboundMessageTransport> transports, CancellationToken token) =>
				{
					StoredMessage = message; PassedTransports = transports; ForwardedToken = token; Writes++;
					StoredTransports = transports.ToArray(); BeforeWrite?.Invoke();
					return WriteTask;
				});
			var batch = (IOutboxStoreBatch)inner;
			A.CallTo(() => batch.TryMarkSentAndReceivedAsync(A<string>._, A<InboxEntry>._, A<CancellationToken>._))
				.ReturnsLazily((string id, InboxEntry entry, CancellationToken token) =>
				{
					BatchMessageId = id; StoredInbox = entry; ForwardedToken = token; Writes++; BeforeWrite?.Invoke();
					return new ValueTask<bool>(BatchTask);
				});
			_sut = new EncryptingOutboxStoreDecorator(inner, registry, Microsoft.Extensions.Options.Options.Create(new EncryptionOptions { Mode = mode }));
		}
		internal async Task InvokeAsync(int route, OutboundMessage message, InboxEntry inbox,
			IEnumerable<OutboundMessageTransport> external, CancellationToken token)
		{
			switch (route)
			{
				case 0: await _sut.StageMessageAsync(message, token); break;
				case 1:
					var multi = _sut.GetService(typeof(IMultiTransportOutboxStore)).ShouldBeAssignableTo<IMultiTransportOutboxStore>();
					await multi.StageMessageWithTransportsAsync(message, external, token); break;
				case 2:
					var batch = _sut.GetService(typeof(IOutboxStoreBatch)).ShouldBeAssignableTo<IOutboxStoreBatch>();
					LastBatchResult = await batch.TryMarkSentAndReceivedAsync("outbox-target", inbox, token); break;
				default: throw new ArgumentOutOfRangeException(nameof(route));
			}
		}
		internal byte[] StoredPayload(int route) => route == 2 ? StoredInbox.ShouldNotBeNull().Payload : StoredMessage.ShouldNotBeNull().Payload;
		internal async Task<byte[]> DecryptAsync(byte[] ciphertext, CancellationToken token)
		{
			// Reuse the public decorator read path and real AES; no copied encryption format or private serializer.
			var readStore = A.Fake<IOutboxStore>();
			A.CallTo(() => readStore.GetUnsentMessagesAsync(1, token)).ReturnsLazily(() =>
				new ValueTask<IEnumerable<OutboundMessage>>([new OutboundMessage("read", ciphertext.ToArray(), "destination") { TenantId = "tenant" }]));
			var registry = A.Fake<IEncryptionProviderRegistry>();
			A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(RealProvider);
			var reader = new EncryptingOutboxStoreDecorator(readStore, registry, Microsoft.Extensions.Options.Options.Create(new EncryptionOptions()));
			return (await reader.GetUnsentMessagesAsync(1, token)).ShouldHaveSingleItem().Payload;
		}
		private Task? _deferredDisposal;
		internal void DeferDisposalUntil(Task operation) => _deferredDisposal = operation;
		public void Dispose()
		{
			if (_deferredDisposal is { IsCompleted: false } pending) { _ = DisposeAfterOperationAsync(pending); }
			else { DisposeCore(); }
		}
		private async Task DisposeAfterOperationAsync(Task operation)
		{
			try { await operation.ConfigureAwait(false); }
			catch (Exception error) { System.Diagnostics.Trace.WriteLine("Late operation failure after bounded join: " + error.GetType().FullName); }
			finally { DisposeCore(); }
		}
		private void DisposeCore() { RealProvider.Dispose(); _keys.Dispose(); }
	}
}
