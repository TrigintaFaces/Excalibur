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
/// gate directly prove the gate works when the test registers it — they say nothing about whether a
/// consumer composing scheduled delivery normally ever receives it, which is the property that actually
/// protects anyone. Both kinds are present below and labelled, because the seam shipped once with only
/// the first kind and was wired to nothing.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ScheduleDurabilityGateShould
{
	// ---------- SAFETY: the gate itself ----------

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

	// ---------- SAFETY: PRODUCTION-PATH WIRING ----------

	/// <summary>
	/// The arm that fails when the gate is declared but installed by nothing. Starting the scheduler runtime
	/// is the act of accepting deliveries the host owes later; on the volatile default that promise is broken
	/// by any restart, so this composition must not be allowed to start.
	/// </summary>
	[Fact]
	public void Refuse_a_volatile_store_when_a_host_starts_the_scheduler_runtime()
	{
		var services = NewHost();
		_ = services.AddDispatchScheduling();
		_ = services.AddTimeAwareScheduling();

		using var provider = services.BuildServiceProvider();

		var error = Should.Throw<OptionsValidationException>(() => Resolve(provider));
		error.Message.ShouldContain(nameof(DurableScheduleStoreRegistration.AddDurableScheduleStore));
	}

	/// <summary>
	/// The gate reads an attestation, so the attestation must be unable to outrun the store it attests.
	/// Scheduling seats the volatile store into the same contract key first, and a TryAdd in the durable
	/// seam would silently lose that race while still emitting the marker — leaving a host that asked for
	/// durability, passed the gate, and runs on the volatile store.
	/// </summary>
	[Fact]
	public void Seat_the_durable_store_even_when_scheduling_was_composed_first()
	{
		var services = NewHost();
		_ = services.AddSingleton(new ConcurrentDictionary<Guid, IScheduledMessage>());
		_ = services.AddDispatchScheduling();
		_ = services.AddDurableScheduleStore<FakeDurableScheduleStore>();

		using var provider = services.BuildServiceProvider();

		_ = provider.GetRequiredService<IScheduleStore>().ShouldBeOfType<FakeDurableScheduleStore>();
	}

	// ---------- LIVENESS ----------

	[Fact]
	public void Start_when_a_durable_store_is_registered_through_the_attesting_seam()
	{
		var services = NewHost();
		_ = services.AddSingleton(new ConcurrentDictionary<Guid, IScheduledMessage>());
		_ = services.AddScheduleDurabilityGate();
		_ = services.AddDurableScheduleStore<FakeDurableScheduleStore>();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
	}

	[Fact]
	public void Start_when_the_host_accepts_a_volatile_store_deliberately()
	{
		var services = NewHost();
		_ = services.AddScheduleDurabilityGate();
		_ = services.Configure<ScheduleDurabilityOptions>(o => o.AllowVolatileScheduleStore = true);

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
	}

	/// <summary>
	/// The arm a refuse-everything gate cannot pass: the supported production composition must still start,
	/// and must start on the store the host actually asked for.
	/// </summary>
	[Fact]
	public void Start_a_scheduler_runtime_backed_by_a_durable_store()
	{
		var services = NewHost();
		_ = services.AddSingleton(new ConcurrentDictionary<Guid, IScheduledMessage>());
		_ = services.AddDispatchScheduling();
		_ = services.AddTimeAwareScheduling();
		_ = services.AddDurableScheduleStore<FakeDurableScheduleStore>();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
		_ = provider.GetRequiredService<IScheduleStore>().ShouldBeOfType<FakeDurableScheduleStore>();
	}

	/// <summary>
	/// Composing scheduling without starting the runtime stays gate-free on purpose, so a development host
	/// is not made to justify a store it never schedules against. Without this arm the gate could be widened
	/// to refuse everything and every safety arm above would still pass.
	/// </summary>
	[Fact]
	public void Leave_a_host_that_only_composes_scheduling_free_to_start()
	{
		var services = NewHost();
		_ = services.AddDispatchScheduling();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
		_ = provider.GetRequiredService<IScheduleStore>().ShouldNotBeNull();
	}

	[Fact]
	public void Not_attest_durability_for_the_in_memory_default()
	{
		var services = NewHost();
		_ = services.AddDispatchScheduling();

		using var provider = services.BuildServiceProvider();

		provider.GetService<IDurableScheduleStoreCapability>().ShouldBeNull();
	}

	// ---------- LIVENESS: the schedule itself survives ----------

	/// <summary>
	/// Exercises the seam rather than the attestation: a delivery scheduled by one process is still there,
	/// and still enabled, for the next one.
	/// </summary>
	[Fact]
	public async Task Keep_a_pending_schedule_across_a_restart_on_a_durable_store()
	{
		var backing = new ConcurrentDictionary<Guid, IScheduledMessage>();
		var scheduled = NewSchedule();

		using (var first = DurableHost(backing).BuildServiceProvider())
		{
			await first.GetRequiredService<IScheduleStore>()
				.StoreAsync(scheduled, TestContext.Current.CancellationToken);
		}

		using var restarted = DurableHost(backing).BuildServiceProvider();

		var surviving = await restarted.GetRequiredService<IScheduleStore>()
			.GetAllAsync(TestContext.Current.CancellationToken);

		var recovered = surviving.ShouldHaveSingleItem();
		recovered.Id.ShouldBe(scheduled.Id);
		recovered.Enabled.ShouldBeTrue("a pending delivery must still be due after a restart");
	}

	/// <summary>
	/// The loss the gate exists to refuse, stated as behaviour. This is what a host silently gets when it
	/// schedules against the in-memory default, and it is why the arm above is not trivially satisfiable.
	/// </summary>
	[Fact]
	public async Task Lose_a_pending_schedule_across_a_restart_on_the_volatile_default()
	{
		var scheduled = NewSchedule();

		using (var first = VolatileHost().BuildServiceProvider())
		{
			await first.GetRequiredService<IScheduleStore>()
				.StoreAsync(scheduled, TestContext.Current.CancellationToken);
		}

		using var restarted = VolatileHost().BuildServiceProvider();

		var surviving = await restarted.GetRequiredService<IScheduleStore>()
			.GetAllAsync(TestContext.Current.CancellationToken);

		surviving.ShouldBeEmpty("the in-memory store forgets, which is exactly what the gate refuses");
	}

	// ---------- helpers ----------

	private static ServiceCollection NewHost()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		return services;
	}

	private static IServiceCollection DurableHost(ConcurrentDictionary<Guid, IScheduledMessage> backing)
	{
		var services = NewHost();
		_ = services.AddSingleton(backing);
		_ = services.AddDispatchScheduling();
		_ = services.AddDurableScheduleStore<FakeDurableScheduleStore>();
		return services;
	}

	private static IServiceCollection VolatileHost()
	{
		var services = NewHost();
		_ = services.AddDispatchScheduling();
		return services;
	}

	private static ScheduledMessage NewSchedule() => new()
	{
		CronExpression = "0 0 * * *",
		MessageName = "PendingDelivery",
		MessageBody = "{}",
		NextExecutionUtc = DateTimeOffset.UtcNow.AddHours(1),
	};

	private static ScheduleDurabilityOptions Resolve(IServiceProvider provider) =>
		provider.GetRequiredService<IOptions<ScheduleDurabilityOptions>>().Value;

	/// <summary>
	/// Implements <see cref="IScheduleStore" /> directly, inheriting no first-party base, so the assertions
	/// bind the contract of the interface rather than a base class that would supply it. The backing
	/// dictionary is supplied from outside the container so it outlives any one provider, which is what
	/// makes the restart arm above a real one.
	/// </summary>
	private sealed class FakeDurableScheduleStore : IScheduleStore
	{
		private readonly ConcurrentDictionary<Guid, IScheduledMessage> _store;

		public FakeDurableScheduleStore(ConcurrentDictionary<Guid, IScheduledMessage> store) => _store = store;

		public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken) =>
			Task.FromResult<IEnumerable<IScheduledMessage>>([.. _store.Values]);

		public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(message);
			_store[message.Id] = message;
			return Task.CompletedTask;
		}

		public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken)
		{
			_ = _store.TryRemove(scheduleId, out _);
			return Task.CompletedTask;
		}
	}
}
