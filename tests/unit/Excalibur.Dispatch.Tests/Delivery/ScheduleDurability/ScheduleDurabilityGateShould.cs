// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Delivery.ScheduleDurability;

/// <summary>
/// Binds the fail-closed contract for schedule durability: a scheduled delivery accepted now must still
/// exist later, and a host that never states otherwise gets that requirement rather than a silent loss.
/// </summary>
/// <remarks>
/// The production-path arms are the load-bearing ones. Arms that build their own container and call the
/// gate directly prove the gate works when the test registers it — they say nothing about whether
/// <c>AddDispatchScheduling</c> installs it, which is the property that actually protects a consumer.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ScheduleDurabilityGateShould
{
	// ---------- SAFETY ----------

	[Fact]
	public void Refuse_the_volatile_default_when_the_host_says_nothing()
	{
		var services = new ServiceCollection();
		_ = services.AddScheduleDurabilityGate();

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(() => Resolve(provider));
	}

	[Fact]
	public void Say_what_is_lost_and_name_both_remedies()
	{
		var services = new ServiceCollection();
		_ = services.AddScheduleDurabilityGate();

		using var provider = services.BuildServiceProvider();

		var error = Should.Throw<OptionsValidationException>(() => Resolve(provider));

		error.Message.ShouldContain("lost");
		error.Message.ShouldContain(nameof(DurableScheduleStoreRegistration.AddDurableScheduleStore));
		error.Message.ShouldContain(nameof(ScheduleDurabilityOptions.AllowVolatileScheduleStore));
	}

	[Fact]
	public void Default_the_volatile_allowance_to_the_protective_value() =>
		new ScheduleDurabilityOptions().AllowVolatileScheduleStore.ShouldBeFalse();

	// ---------- LIVENESS ----------

	[Fact]
	public void Start_when_a_durable_store_is_registered_through_the_attesting_seam()
	{
		var services = new ServiceCollection();
		_ = services.AddScheduleDurabilityGate();
		_ = services.AddSingleton(new ConcurrentDictionary<string, string>());
		_ = services.AddDurableScheduleStore<FakeDurableScheduleStore>();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
	}

	[Fact]
	public void Start_when_the_host_accepts_a_volatile_store_deliberately()
	{
		var services = new ServiceCollection();
		_ = services.AddScheduleDurabilityGate();
		_ = services.Configure<ScheduleDurabilityOptions>(o => o.AllowVolatileScheduleStore = true);

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
	}

	// ---------- PRODUCTION-PATH WIRING ----------



	[Fact]
	public void Not_attest_durability_for_the_in_memory_default()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchScheduling();

		using var provider = services.BuildServiceProvider();

		provider.GetService<IDurableScheduleStoreCapability>().ShouldBeNull();
	}

	private static ScheduleDurabilityOptions Resolve(IServiceProvider provider) =>
		provider.GetRequiredService<IOptions<ScheduleDurabilityOptions>>().Value;

	/// <summary>
	/// Implements <see cref="IScheduleStore" /> directly, inheriting no first-party base, so the assertions
	/// bind the interface's contract rather than a base class that would supply it.
	/// </summary>
	private sealed class FakeDurableScheduleStore : IScheduleStore
	{
		private readonly ConcurrentDictionary<string, string> _store;

		public FakeDurableScheduleStore(ConcurrentDictionary<string, string> store) => _store = store;

		public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken) =>
			Task.FromResult(Enumerable.Empty<IScheduledMessage>());

		public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken)
		{
			_ = _store.TryAdd(message.Id.ToString(), "scheduled");
			return Task.CompletedTask;
		}

		public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken)
		{
			_ = _store.TryRemove(scheduleId.ToString(), out _);
			return Task.CompletedTask;
		}
	}
}
