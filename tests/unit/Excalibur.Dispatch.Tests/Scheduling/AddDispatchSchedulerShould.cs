// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Scheduling;

/// <summary>
/// P.6 verification: confirms AddDispatchScheduler bundles the expected scheduling services.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class AddDispatchSchedulerShould
{
	[Fact]
	public void RegisterIScheduleStore()
	{
		var services = new ServiceCollection();

		services.AddDispatchScheduling();

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IScheduleStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterICronScheduler()
	{
		var services = new ServiceCollection();

		services.AddDispatchScheduling();

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICronScheduler) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterIDispatchScheduler()
	{
		var services = new ServiceCollection();

		services.AddDispatchScheduling();

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDispatchScheduler) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterSchedulerOptions()
	{
		var services = new ServiceCollection();

		services.AddDispatchScheduling();

		// Sprint 750: explicit IValidateOptions validators registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<SchedulerOptions>) ||
			sd.ServiceType == typeof(IOptionsChangeTokenSource<SchedulerOptions>) ||
			sd.ServiceType == typeof(IValidateOptions<SchedulerOptions>));
	}

	[Fact]
	public void RegisterCronScheduleOptions()
	{
		var services = new ServiceCollection();

		services.AddDispatchScheduling();

		// Sprint 750: explicit IValidateOptions validators registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<CronScheduleOptions>) ||
			sd.ServiceType == typeof(IOptionsChangeTokenSource<CronScheduleOptions>) ||
			sd.ServiceType == typeof(IValidateOptions<CronScheduleOptions>));
	}

	[Fact]
	public void AddDispatchSchedulerGeneric_ReplacesDefaultScheduler()
	{
		var services = new ServiceCollection();

		services.AddDispatchScheduler<CustomTestScheduler>();

		// Should have replaced default with custom
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDispatchScheduler) &&
			sd.ImplementationType == typeof(CustomTestScheduler) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddDispatchSchedulerGeneric_StillBundlesBaseServices()
	{
		var services = new ServiceCollection();

		services.AddDispatchScheduler<CustomTestScheduler>();

		// Base services still registered
		services.ShouldContain(sd => sd.ServiceType == typeof(IScheduleStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICronScheduler));
	}

	[Fact]
	public void ThrowOnNullServices()
	{
		Should.Throw<ArgumentNullException>(() =>
			DeliveryServiceCollectionExtensions.AddDispatchScheduling(null!));
	}

	// --- Test type ---
	private sealed class CustomTestScheduler : IDispatchScheduler
	{
		public Task ScheduleOnceAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(
			DateTimeOffset executeAtUtc, TMessage message, CancellationToken cancellationToken)
			where TMessage : class => Task.CompletedTask;

		public Task ScheduleRecurringAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(
			string cronExpression, TMessage message, CancellationToken cancellationToken)
			where TMessage : class => Task.CompletedTask;

		public Task ScheduleRecurringAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(
			TimeSpan interval, TMessage message, CancellationToken cancellationToken)
			where TMessage : class => Task.CompletedTask;
	}
}
