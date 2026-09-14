// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
///     Tests for the <see cref="OutboxStagingMiddleware" /> class.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class OutboxStagingMiddlewareShould
{
	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() =>
			new OutboxStagingMiddleware(null!, null, new DispatchJsonSerializer(), NullLogger<OutboxStagingMiddleware>.Instance));

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() =>
			new OutboxStagingMiddleware(
				Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions()),
				null, new DispatchJsonSerializer(), null!));

	[Fact]
	public void ConstructWithoutAStore_SoTheDefaultPipelineCanSeatItInert()
	{
		// This arm ASSERTED THE OPPOSITE until the middleware moved onto the default pipeline, and the
		// inversion is deliberate rather than a relaxation. The constructor used to throw when enabled with
		// no store, which made the middleware unseatable by default: a zero-configuration host that never
		// asked for an outbox would have failed to dispatch. The constructor cannot tell a host that
		// deliberately called UseOutbox() from one that merely took the defaults, so it is the wrong place
		// to decide -- it punished the second population for the first one's mistake, and it fired per scope
		// on first dispatch rather than at startup.
		//
		// The check now lives in OutboxStagingWiringValidator, registered by UseOutbox() alone and refusing
		// to START. Nothing is unguarded: a host that asked for the outbox and registered no store fails at
		// startup, and a handler that writes with no store fails at the write naming IOutboxStore.
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = true });

		var middleware = new OutboxStagingMiddleware(
			options, null, new DispatchJsonSerializer(), NullLogger<OutboxStagingMiddleware>.Instance);

		middleware.ShouldNotBeNull(
			"the default profile seats this middleware for every host, so construction without a store must "
			+ "succeed and stay inert rather than throw");
	}

	[Fact]
	public void CreateSuccessfullyWhenDisabled()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = false });

		var middleware = new OutboxStagingMiddleware(options, null, new DispatchJsonSerializer(), NullLogger<OutboxStagingMiddleware>.Instance);
		middleware.ShouldNotBeNull();
	}

	[Fact]
	public void CreateSuccessfullyWithOutboxStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = true });
		var store = A.Fake<IOutboxStore>();

		var middleware = new OutboxStagingMiddleware(options, store, new DispatchJsonSerializer(), NullLogger<OutboxStagingMiddleware>.Instance);
		middleware.ShouldNotBeNull();
	}

	// Sprint 683 T.17: IOutboxService deleted -- CreateSuccessfullyWithOutboxService test removed

	/// <summary>
	/// FR-B5 (r4nd4w) regression lock: when the producer context carries W3C baggage
	/// (captured as "baggage.{name}" items), the staged outbound message's headers MUST
	/// carry a single "baggage" header with the reconstructed, key-ordered value.
	/// </summary>
	/// <remarks>
	/// Structural RED argument: pre-fix, <c>CreateMessageHeaders</c> had no baggage parameter
	/// and never wrote a "baggage" key (only MessageType/SourceMessageType/CreatedAt/Correlation/
	/// Causation/Tenant/traceparent were emitted). The staged headers therefore would NOT contain
	/// "baggage", so <c>ShouldContainKey("baggage")</c> fails on the pre-fix code.
	/// </remarks>
	[Fact]
	public async Task StageBaggageHeaderWhenBaggageIsPresent()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = true });
		OutboundMessage? captured = null;
		var store = A.Fake<IOutboxStore>();
		A.CallTo(() => store.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Invokes((OutboundMessage m, CancellationToken _) => captured = m);

		var middleware = new OutboxStagingMiddleware(options, store, new DispatchJsonSerializer(), NullLogger<OutboxStagingMiddleware>.Instance);

		var context = new Excalibur.Dispatch.Messaging.MessageContext();
		// Baggage items as captured from the ambient Activity (keyed "baggage.{name}"); ordered by key.
		context.Items["baggage.tenant"] = "acme";
		context.Items["baggage.user"] = "alice";

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			var outboxContext = ctx.GetItem<OutboxContext>("OutboxContext")!;
			outboxContext.AddOutboundMessage(new StagedTestMessage());
			return new ValueTask<IMessageResult>(Excalibur.Dispatch.MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(new StagedTestMessage(), context, next, CancellationToken.None);

		// Assert
		captured.ShouldNotBeNull();
		captured!.Headers.ShouldContainKey("baggage");
		captured.Headers["baggage"].ShouldBe("tenant=acme,user=alice");
	}

	/// <summary>
	/// FR-B5 (r4nd4w) additive guard: when NO baggage is present in the producer context,
	/// the staged outbound message MUST NOT carry a "baggage" header (additive-only behavior).
	/// </summary>
	/// <remarks>
	/// Structural RED argument: this pins the additive half of the contract — the header is only
	/// written when baggage is non-empty. It fails any impl that unconditionally emits a (possibly
	/// empty) "baggage" header.
	/// </remarks>
	[Fact]
	public async Task NotStageBaggageHeaderWhenNoBaggageIsPresent()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = true });
		OutboundMessage? captured = null;
		var store = A.Fake<IOutboxStore>();
		A.CallTo(() => store.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Invokes((OutboundMessage m, CancellationToken _) => captured = m);

		var middleware = new OutboxStagingMiddleware(options, store, new DispatchJsonSerializer(), NullLogger<OutboxStagingMiddleware>.Instance);

		var context = new Excalibur.Dispatch.Messaging.MessageContext(); // no baggage.* items

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			var outboxContext = ctx.GetItem<OutboxContext>("OutboxContext")!;
			outboxContext.AddOutboundMessage(new StagedTestMessage());
			return new ValueTask<IMessageResult>(Excalibur.Dispatch.MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(new StagedTestMessage(), context, next, CancellationToken.None);

		// Assert
		captured.ShouldNotBeNull();
		captured!.Headers.ShouldNotContainKey("baggage");
	}

	/// <summary>
	/// 15sf7a engage-test (author≠impl): the outbox WRITE path MUST route through the injected
	/// <see cref="DispatchJsonSerializer"/> (camelCase property naming + <c>JsonStringEnumConverter</c>),
	/// symmetric with the READ path (OutboxProcessor) — NOT a raw default <c>JsonSerializer</c>.
	/// </summary>
	/// <remarks>
	/// Structural RED argument: pre-fix, <c>SerializeMessageToBytes</c> called
	/// <c>JsonSerializer.SerializeToUtf8Bytes(message, message.GetType())</c> with DEFAULT options,
	/// emitting PascalCase keys and NUMERIC enums. This pins the configured wire format on the persisted
	/// payload, so any raw-default write (PascalCase <c>"Status":1</c>) fails the assertions.
	/// </remarks>
	[Fact]
	public async Task StagePayloadUsingConfiguredSerializer_CamelCaseAndStringEnums()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = true });
		OutboundMessage? captured = null;
		var store = A.Fake<IOutboxStore>();
		A.CallTo(() => store.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Invokes((OutboundMessage m, CancellationToken _) => captured = m);

		// The DEFAULT DispatchJsonSerializer already applies camelCase naming + JsonStringEnumConverter
		// (DispatchJsonSerializer.cs:55-112) — the configured wire contract shared with the read path.
		var middleware = new OutboxStagingMiddleware(options, store, new DispatchJsonSerializer(), NullLogger<OutboxStagingMiddleware>.Instance);

		var context = new Excalibur.Dispatch.Messaging.MessageContext();

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			var outboxContext = ctx.GetItem<OutboxContext>("OutboxContext")!;
			outboxContext.AddOutboundMessage(new SerializerEngageMessage());
			return new ValueTask<IMessageResult>(Excalibur.Dispatch.MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(new SerializerEngageMessage(), context, next, CancellationToken.None);

		// Assert
		captured.ShouldNotBeNull();
		var json = System.Text.Encoding.UTF8.GetString(captured!.Payload);

		// String enum (configured JsonStringEnumConverter) — RED on the raw-default write which emits the
		// numeric enum value.
		json.ShouldContain("Active");
		json.ShouldNotContain("\"Status\":1");

		// camelCase property naming (configured) — RED on the raw-default PascalCase write.
		json.ShouldContain("\"status\"");
	}

	private enum EngageStatus
	{
		Pending = 0,
		Active = 1,
	}

	private sealed class SerializerEngageMessage : IDispatchMessage
	{
		public EngageStatus Status { get; init; } = EngageStatus.Active;
	}

	private sealed class StagedTestMessage : IDispatchMessage
	{
		public string Name { get; init; } = "staged";
	}
}
