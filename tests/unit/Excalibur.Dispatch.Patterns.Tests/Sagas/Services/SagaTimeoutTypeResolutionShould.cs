// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Saga;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Services;
using Excalibur.Saga.Storage;

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


		// A registry that is PRESENT but does not know this type -- the realistic shape of an unregistered
		// type in a configured host, as opposed to a host with no registry at all.
		var registry = A.Fake<ISagaTypeRegistry>();
		_ = A.CallTo(() => registry.ResolveType(A<string>._)).Returns(null);

		var dispatcher = A.Fake<IDispatcher>();
		var services = new ServiceCollection();
		_ = services.AddSingleton(dispatcher);
		services.AddDefaultTenantContext();

		using var provider = services.BuildServiceProvider();
		var store = new InMemorySagaTimeoutStore(provider.GetRequiredService<ITenantContext>());
		await store.ScheduleTimeoutAsync(timeout, CancellationToken.None);
		var failures = ((IServiceProvider)store).GetService(typeof(ISagaTimeoutFailureStore)).ShouldBeAssignableTo<ISagaTimeoutFailureStore>().ShouldNotBeNull();

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

		// R08 preserves unresolved sources as Unknown holds instead of blindly acknowledging them.
		SagaTimeoutHoldPage page;
		try
		{
			var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
			do
			{
				page = await failures.GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, timeout.SagaId, 10, null, CancellationToken.None);
				if (page.Items.Count != 0)
				{
					break;
				}

				await Task.Delay(10);
			}
			while (DateTimeOffset.UtcNow < deadline);
		}
		finally
		{
			await service.StopAsync(CancellationToken.None);
		}

		var held = page.Items.ShouldHaveSingleItem();
		held.Kind.ShouldBe(MessageFailureKind.Unknown);
		held.State.ShouldBe(SagaTimeoutHoldState.Held);
		held.Timeout.TimeoutId.ShouldBe(timeout.TimeoutId);
		held.Timeout.SagaId.ShouldBe(timeout.SagaId);
		held.Timeout.SagaType.ShouldBe(timeout.SagaType);
		held.Timeout.TimeoutType.ShouldBe(timeout.TimeoutType);
		held.Timeout.TimeoutData.ShouldBe(timeout.TimeoutData);
		held.Timeout.DueAt.ShouldBe(timeout.DueAt);
		(await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, CancellationToken.None)).ShouldHaveSingleItem().TimeoutId.ShouldBe(timeout.TimeoutId);
		(await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow.AddMinutes(3), 10, CancellationToken.None)).ShouldBeEmpty();
		A.CallTo(() => registry.ResolveType(timeout.TimeoutType)).MustHaveHappened();
		A.CallTo(dispatcher).MustNotHaveHappened();
	}
}
