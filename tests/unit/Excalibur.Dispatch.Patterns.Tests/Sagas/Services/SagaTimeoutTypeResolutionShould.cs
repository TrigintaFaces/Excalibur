// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Saga;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Services;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Patterns.Tests.Sagas.Services;

/// <summary>
/// Locks the timeout-delivery type-resolution path to the registry: a stored <c>TimeoutType</c> that is not
/// a registered saga timeout type must be REFUSED, never resolved by scanning loaded assemblies.
/// </summary>
/// <remarks>
/// <para>
/// <c>TimeoutType</c> is a string read back from the timeout store. Resolving it with
/// <c>AppDomain.CurrentDomain.GetAssemblies()</c> lets that stored string select any type in the process and
/// hand it to <c>JsonSerializer.Deserialize</c> or a constructor invoke — the gadget-chain shape.
/// </para>
/// <para>
/// NON-VACUITY. <see cref="UnregisteredTimeoutMessage"/> is deliberately a real, loaded type that DOES
/// implement <see cref="IDispatchMessage"/>. That is what makes this arm discriminating: the scan would have
/// found it (it is in a loaded assembly) and it would have passed the downstream
/// <c>is not IDispatchMessage</c> guard, so delivery would have proceeded. A type that failed either of those
/// would be refused with or without the fix and would prove nothing. The registry returns null for it, so the
/// only way it can reach the dispatcher is a resolution path other than the registry.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Saga)]
public sealed class SagaTimeoutTypeResolutionShould
{
	/// <summary>A real, loaded dispatch message that is never registered with the saga type registry.</summary>
	private sealed record UnregisteredTimeoutMessage : IDispatchMessage
	{
		public string MessageId { get; init; } = Guid.NewGuid().ToString();
	}

	[Fact]
	public async Task RefuseAnUnregisteredTimeoutType_RatherThanResolveItByScanningAssemblies()
	{
		// Arrange
		var timeout = new SagaTimeout(
			TimeoutId: "timeout-1",
			SagaId: "saga-1",
			SagaType: "TestSaga",
			TimeoutType: typeof(UnregisteredTimeoutMessage).FullName!,
			TimeoutData: null,
			DueAt: DateTimeOffset.UtcNow.AddMinutes(-1),
			ScheduledAt: DateTimeOffset.UtcNow.AddMinutes(-2));

		var store = A.Fake<ISagaTimeoutStore>();

		// Every claim returns the same due timeout. A one-shot setup followed by an empty one does NOT work
		// here: FakeItEasy matches the most recently configured rule first, so the empty result shadows the
		// one-shot and the service never sees a timeout at all -- the arm then passes or fails for reasons
		// unrelated to type resolution.
		_ = A.CallTo(() => store.ClaimDueTimeoutsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<SagaTimeout>>([timeout]));

		// A registry that is PRESENT but does not know this type -- the realistic shape of an unregistered
		// type in a configured host, as opposed to a host with no registry at all.
		var registry = A.Fake<ISagaTypeRegistry>();
		_ = A.CallTo(() => registry.ResolveType(A<string>._)).Returns(null);

		var dispatcher = A.Fake<IDispatcher>();
		var services = new ServiceCollection();
		_ = services.AddSingleton(dispatcher);

		using var provider = services.BuildServiceProvider();

		var options = MsOptions.Create(new SagaTimeoutOptions
		{
			PollInterval = TimeSpan.FromMilliseconds(10),
			EnableVerboseLogging = false,
		});

		using var service = new SagaTimeoutDeliveryService(
			store,
			provider,
			NullLogger<SagaTimeoutDeliveryService>.Instance,
			options,
			registry);

		// Act
		await service.StartAsync(CancellationToken.None);

		// The refusal path marks the timeout delivered so it does not retry forever; poll for that observable
		// rather than sleeping a fixed interval, so the arm is not timing-dependent.
		var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
		while (DateTimeOffset.UtcNow < deadline
			&& !Fake.GetCalls(store).Any(call => call.Method.Name == nameof(ISagaTimeoutStore.MarkDeliveredAsync)))
		{
			await Task.Delay(10);
		}

		await service.StopAsync(CancellationToken.None);

		// Assert -- the timeout was retired without ever reaching the dispatcher.
		A.CallTo(() => store.MarkDeliveredAsync("timeout-1", A<CancellationToken>._)).MustHaveHappened();
		A.CallTo(dispatcher).MustNotHaveHappened();
	}
}
