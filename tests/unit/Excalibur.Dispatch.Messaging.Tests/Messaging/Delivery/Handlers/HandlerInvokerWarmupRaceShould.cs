// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery.Handlers;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Author≠impl regression lock for S850 Lane D · <c>pj1mck</c> (S-D/TOCTOU-NRE on
/// <see cref="HandlerInvoker"/>'s warmup cache).
/// </summary>
/// <remarks>
/// <para>
/// Authored by FrontendDeveloper (did NOT implement the fix — independence per
/// <c>issue-remediation-protocol</c>) against the frozen GUIDE seam (msg 15508): the hot path must read
/// the <c>volatile</c> <c>_warmupCache</c> field exactly once into a local and null-guard it, so a
/// concurrent <see cref="HandlerInvoker.FreezeCache"/> that nulls the field — after the <c>_isFrozen</c>
/// check already observed <see langword="false"/> — can never be dereferenced.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix surface):</b> the pre-fix code is
/// <c>_warmupCache!.GetOrAdd(...)</c>. This lock reflectively reconstructs the exact interleaving the race
/// produces — control is in the warmup section (<c>_isFrozen == false</c>) while <c>_warmupCache</c> has
/// been nulled — and invokes the public entry point. On the pre-fix surface the <c>!</c> dereferences
/// <see langword="null"/> → <see cref="NullReferenceException"/> (RED). On the fixed surface the read-once
/// local is observed null and the code falls through to the frozen cache / uncached build → no throw,
/// correct invocation (GREEN). Deterministic: no real thread race is needed — the state is constructed
/// directly, so the lock can never flap.
/// </para>
/// <para>
/// Joins the <c>HandlerInvokerRegistry</c> collection and resets the static cache on dispose so the
/// reflective field mutation cannot leak into any parallel test class.
/// </para>
/// </remarks>
[Collection("HandlerInvokerRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HandlerInvokerWarmupRaceShould : IDisposable
{
	private static readonly Type InvokerType = typeof(HandlerInvoker);

	public HandlerInvokerWarmupRaceShould() => HandlerInvoker.ClearCache();

	public void Dispose() => HandlerInvoker.ClearCache();

	[Fact]
	public async Task NotThrowNullReference_WhenWarmupCacheIsNulledByAConcurrentFreeze()
	{
		// Arrange — reconstruct the FreezeCache race state exactly:
		//   * _isFrozen == false  -> InvokeValueTaskAsync does NOT take the frozen branch; control reaches
		//                            the warmup section (this is the window the race lives in).
		//   * _warmupCache == null -> a concurrent FreezeCache nulled the field after the _isFrozen check.
		//   * _frozenCache == null -> force the rare cold fallback-build path so GREEN proves the full
		//                             null-safe fallthrough (not merely a frozen-cache hit).
		HandlerInvoker.ClearCache();      // empties _knownInvokerCache + ThreadStatic fast cache first
		SetStaticField("_isFrozen", value: false);
		SetStaticField("_frozenCache", value: null);
		SetStaticField("_warmupCache", value: null);

		var invoker = new HandlerInvoker();
		var handler = new RaceHandler();
		var message = new RaceMessage();

		// Act + Assert — pre-fix: `_warmupCache!.GetOrAdd` dereferences null → NRE (RED).
		// Fixed: read-once local is null → fallback BuildInvoker → invokes the handler, returns null (GREEN).
		object? result = null;
		await Should.NotThrowAsync(async () =>
			result = await invoker.InvokeAsync(handler, message, CancellationToken.None));

		result.ShouldBeNull();
	}

	private static void SetStaticField(string name, object? value)
	{
		var field = InvokerType.GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
		field.ShouldNotBeNull($"HandlerInvoker.{name} static field not found — seam changed");
		field.SetValue(obj: null, value);
	}

	private sealed class RaceMessage : IDispatchMessage
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? nameof(RaceMessage);
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class RaceHandler
	{
		public Task HandleAsync(RaceMessage message, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}
}
