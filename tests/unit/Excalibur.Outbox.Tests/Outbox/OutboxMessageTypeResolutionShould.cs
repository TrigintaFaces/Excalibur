// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- FakeItEasy fakes do not require disposal

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DispatchOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests.Core;

/// <summary>
/// Locks the outbox drain's type-resolution path: a stored <c>MessageType</c> naming a type that is not an
/// <see cref="IDispatchMessage"/> must be refused BEFORE the payload is handed to the deserializer.
/// </summary>
/// <remarks>
/// <para>
/// <c>MessageType</c> is a string read back from the outbox table. Resolving it against every loaded assembly
/// lets that stored string select any type in the process; the deserializer then runs that type's constructors
/// and setters. Checking the result is an <see cref="IDispatchMessage"/> AFTER deserialization — which is what
/// the drain loop did — discards the object but only once it has already been built.
/// </para>
/// <para>
/// NON-VACUITY. <see cref="System.Text.StringBuilder"/> is chosen deliberately: it is loaded in every process,
/// so the assembly scan resolves it, and it is trivially deserializable from <c>{}</c>, so the old path
/// constructed it successfully and only then dropped it. The observable is therefore not "no message came
/// back" — that was true before the fix too — but that the drain reports the type as unresolvable, which is
/// only reachable when resolution itself refuses the type.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxMessageTypeResolutionShould : IDisposable
{
	private readonly IOutboxStore _outboxStore = A.Fake<IOutboxStore>();
	private readonly IOutboxProcessor _outboxProcessor = A.Fake<IOutboxProcessor>();
	private readonly DispatchJsonSerializer _serializer = new();
	/// <summary>
	/// Options declaring exactly one resolvable message type.
	/// </summary>
	/// <remarks>
	/// The declaration is what makes the two arms below distinguishable. <see cref="ResolvableTestMessage"/>
	/// and <see cref="UndeclaredTestMessage"/> are identical in every respect the old resolver could
	/// observe — same assembly, both loaded, both <see cref="IDispatchMessage"/>, both deserializable from
	/// <c>{}</c> — so the only thing separating them is that the host named one and not the other.
	/// </remarks>
	private readonly IOptions<DispatchOutboxOptions> _options = Options.Create(
		DispatchOutboxOptions.Balanced().WithMessageTypes(typeof(ResolvableTestMessage)));
	private readonly ILogger<MessageOutbox> _logger = A.Fake<ILogger<MessageOutbox>>();
	private MessageOutbox? _sut;

	public void Dispose() => _sut?.Dispose();

	private static OutboundMessage Staged(string messageType, byte[] payload) => new()
	{
		Id = "message-1",
		MessageType = messageType,
		Payload = payload,
		Destination = "test",
		CreatedAt = DateTimeOffset.UtcNow,
		Status = OutboxStatus.Staged,
	};

	[Fact]
	public async Task RefuseAStoredTypeThatIsNotADispatchMessage()
	{
		// Arrange -- a real, loaded, deserializable type that is NOT an IDispatchMessage.
		var staged = Staged(typeof(System.Text.StringBuilder).FullName!, "{}"u8.ToArray());
		_ = A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>([staged]));

		// A faked ILogger returns false from IsEnabled, and the source-generated log methods check it first,
		// so Log() is never reached and an assertion on it fails whatever the code did.
		_ = A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);

		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);

		// Act
		var pending = await _sut.GetPendingMessagesAsync(CancellationToken.None);

		// Assert -- nothing dispatchable came back, and the drain took the "cannot resolve" branch rather
		// than the "deserialized, then discarded" one.
		pending.ShouldBeEmpty();
		A.CallTo(_logger)
			.Where(call => call.Method.Name == nameof(ILogger.Log)
				&& call.GetArgument<LogLevel>(0) == LogLevel.Warning)
			.MustHaveHappened();
	}

	/// <summary>
	/// A stored type name that IS an <see cref="IDispatchMessage"/> but was never declared by the host
	/// must not resolve.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm the interface check above cannot reach. Constraining resolution to
	/// <see cref="IDispatchMessage"/> bounds the reachable set by what happens to be loaded, which is not
	/// a property the host chose — a referenced package, a test double, a plugin, or anything else that
	/// implements the interface enters the set without anyone deciding it should. Only an explicit
	/// declaration bounds it by intent.
	/// </para>
	/// <para>
	/// NON-VACUITY. <see cref="UndeclaredTestMessage"/> is deliberately built to be resolvable the old
	/// way: it lives in this assembly, so it is loaded; it implements <see cref="IDispatchMessage"/>, so
	/// it passed the interface filter; and it deserializes from <c>{}</c>, so it materialised
	/// successfully. Against the assembly-scanning resolver this arm therefore fails on its first
	/// assertion — the message comes back — rather than passing for an unrelated reason.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RefuseAStoredDispatchMessageTypeTheHostNeverDeclared()
	{
		var staged = Staged(
			typeof(UndeclaredTestMessage).FullName!,
			System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new UndeclaredTestMessage()));

		_ = A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>([staged]));
		_ = A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);

		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);

		var pending = await _sut.GetPendingMessagesAsync(CancellationToken.None);

		pending.ShouldBeEmpty();
		A.CallTo(_logger)
			.Where(call => call.Method.Name == nameof(ILogger.Log)
				&& call.GetArgument<LogLevel>(0) == LogLevel.Warning)
			.MustHaveHappened();
	}

	[Fact]
	public async Task StillResolveAStoredTypeTheHostDeclared()
	{
		// Liveness, and it carries more weight now than it did against the interface check alone: an
		// allow-list that resolved nothing would satisfy both arms above perfectly, and would turn the
		// security narrowing into a store that can never read anything back.
		var staged = Staged(
			typeof(ResolvableTestMessage).FullName!,
			System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new ResolvableTestMessage()));

		_ = A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>([staged]));

		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, _options, _logger);

		var pending = await _sut.GetPendingMessagesAsync(CancellationToken.None);

		_ = pending.ShouldHaveSingleItem().ShouldBeOfType<ResolvableTestMessage>();
	}

	/// <summary>A dispatch message the host declared, which the drain must continue to resolve.</summary>
	private sealed record ResolvableTestMessage : IDispatchMessage
	{
		public string MessageId { get; init; } = "message-1";
	}

	/// <summary>
	/// A dispatch message the host did NOT declare. Identical in shape to
	/// <see cref="ResolvableTestMessage"/> so that the only difference between the two arms is the
	/// declaration.
	/// </summary>
	private sealed record UndeclaredTestMessage : IDispatchMessage
	{
		public string MessageId { get; init; } = "message-1";
	}
}
