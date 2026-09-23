// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// A handler the index cannot see must be refused at startup, never dropped in silence.
/// </summary>
/// <remarks>
/// <para>
/// The handler index is built from a descriptor's implementation type and implementation instance. A
/// factory registration supplies neither, so such a descriptor used to contribute no entry and the
/// handler was simply never invoked — for an action that surfaced much later as a dispatch-time "no
/// handler registered", and for an event as nothing at all, since publishing to zero handlers is legal.
/// </para>
/// <para>
/// The registration is now refused where it is made, with a message naming the supported forms. These
/// arms pair a refusal (safety) with an invocation (liveness): a scan that refused everything would
/// satisfy the first while making the framework unusable.
/// </para>
/// </remarks>
public sealed class FactoryRegisteredHandlerIsInvokedShould
{
	private sealed record ProbeAction(string Value) : IDispatchAction;

	private sealed record ProbeEvent(string Value) : IDispatchEvent;

	private sealed class RecordingHandler(List<string> log) : IActionHandler<ProbeAction>
	{
		public Task HandleAsync(ProbeAction action, CancellationToken cancellationToken)
		{
			log.Add(action.Value);
			return Task.CompletedTask;
		}
	}

	private sealed class RecordingEventHandler(List<string> log) : IEventHandler<ProbeEvent>
	{
		public Task HandleAsync(ProbeEvent @event, CancellationToken cancellationToken)
		{
			log.Add(@event.Value);
			return Task.CompletedTask;
		}
	}

	[Fact]
	public async Task InvokeAHandlerRegisteredByType()
	{
		// LIVENESS. The documented registration shape must still work. Without this arm, a scan that
		// refused every handler would satisfy both refusal arms below while breaking the framework.
		var log = new List<string>();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddScoped<IActionHandler<ProbeAction>, RecordingHandler>();
		_ = services.AddSingleton(log);
		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		_ = await dispatcher.DispatchAsync(new ProbeAction("by-type"), CancellationToken.None);

		log.ShouldContain("by-type",
			"a handler registered by type must be invoked; if this fails the control is broken, not the subject");
	}

	[Fact]
	public void RefuseAnActionHandlerRegisteredByFactory()
	{
		// SAFETY. The registration cannot be indexed, so it must be refused rather than accepted and
		// silently ignored.
		var log = new List<string>();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddScoped<IActionHandler<ProbeAction>>(_ => new RecordingHandler(log));
		_ = services.AddDispatch(_ => { });

		var ex = Should.Throw<InvalidOperationException>(() =>
		{
			using var provider = services.BuildServiceProvider();
			_ = provider.GetRequiredService<IDispatcher>();
		});

		ex.Message.ShouldContain("factory", customMessage:
			"the refusal must say WHY the registration was rejected, not merely that it was");
		ex.Message.ShouldContain("by type", customMessage:
			"the refusal must name the supported form, or the consumer cannot act on it");
	}

	[Fact]
	public void RefuseAnEventHandlerRegisteredByFactory()
	{
		// SAFETY, and this is the half that was previously undetectable. An unindexed ACTION handler at
		// least produced an exception at dispatch; an unindexed EVENT handler produced nothing, because
		// publishing to zero handlers is a legal no-op.
		var log = new List<string>();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddScoped<IEventHandler<ProbeEvent>>(_ => new RecordingEventHandler(log));
		_ = services.AddDispatch(_ => { });

		var ex = Should.Throw<InvalidOperationException>(() =>
		{
			using var provider = services.BuildServiceProvider();
			_ = provider.GetRequiredService<IDispatcher>();
		});

		ex.Message.ShouldContain(nameof(ProbeEvent), customMessage:
			"the refusal must name the offending registration; a consumer with many handlers cannot "
			+ "otherwise tell which one to change");
	}
}
