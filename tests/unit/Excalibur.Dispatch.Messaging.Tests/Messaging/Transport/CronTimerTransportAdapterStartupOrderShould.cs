// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using MR = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Binds the ordering of the two things startup does when the adapter is configured to fire on start:
/// the startup fire runs to completion first, and only then is the scheduled loop launched.
/// </summary>
/// <remarks>
/// <para>
/// Launching the loop first puts the startup fire and the loop's first scheduled fire in flight
/// together. With overlap prevention on, the second of them is silently dropped as an overlap; with it
/// off -- which is a supported setting -- they run concurrently. Either way the adapter's behaviour on
/// its very first tick is decided by a race rather than by the overlap setting the consumer chose.
/// </para>
/// <para>
/// Both arms are needed. The ordering arm alone -- "the loop is not running during the startup fire" --
/// is satisfied by an adapter that never starts the loop at all, which is a silent scheduler outage. The
/// liveness arm says the loop is running once startup has returned, and that the startup fire happened
/// exactly once rather than being skipped to obtain the ordering.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class CronTimerTransportAdapterStartupOrderShould
{
	private static readonly FieldInfo TimerTaskField =
		typeof(CronTimerTransportAdapter).GetField("_timerTask", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException(
			"the adapter no longer has a _timerTask field; this lock reads it to observe whether the "
			+ "scheduled loop has been launched");

	[Fact]
	public async Task CompleteTheStartupFireBeforeLaunchingTheScheduledLoop()
	{
		var dispatcher = A.Fake<IDispatcher>();

		// A real provider, not a fake: the adapter builds a MessageContext from it before dispatching, and
		// a fake that answers every resolution with a proxy makes that throw into the adapter's own catch,
		// which would show up here as "the startup fire never happened" rather than as a wiring mistake.
		var serviceProvider = new ServiceCollection()
			.AddSingleton(dispatcher)
			.AddSingleton(TimeProvider.System)
			.BuildServiceProvider();

		await using var sut = new CronTimerTransportAdapter(
			NullLogger<CronTimerTransportAdapter>.Instance,
			A.Fake<ICronScheduler>(),
			serviceProvider,
			new CronTimerTransportAdapterOptions
			{
				CronExpression = "*/5 * * * *",
				RunOnStartup = true,

				// Overlap prevention off is the setting under which the two fires would actually run
				// together rather than one being dropped, so it is the setting that exposes the ordering.
				PreventOverlap = false,
			});

		var startupFires = 0;
		var loopLaunchedDuringStartupFire = false;

		// The adapter calls the generic overload with the concrete trigger type, so the configuration has
		// to name that instantiation -- a configuration on IDispatchMessage is a different method and
		// would silently not match.
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<CronTimerTriggerMessage>._,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes(() =>
			{
				startupFires++;
				if (TimerTaskField.GetValue(sut) is not null)
				{
					loopLaunchedDuringStartupFire = true;
				}
			})
			.Returns(Task.FromResult<IMessageResult>(MR.Success()));

		await sut.StartAsync(TestContext.Current.CancellationToken);

		loopLaunchedDuringStartupFire.ShouldBeFalse(
			"the scheduled loop was already running while the startup fire was still in progress, so the "
			+ "startup fire and the loop's first scheduled fire are free to overlap regardless of the "
			+ "configured overlap behaviour");

		startupFires.ShouldBe(
			1,
			"liveness: the startup fire must actually happen -- ordering obtained by skipping it is not "
			+ "the fix");

		TimerTaskField.GetValue(sut).ShouldNotBeNull(
			"liveness: the scheduled loop must be running once startup has returned, or the adapter has "
			+ "no schedule at all");

		await sut.StopAsync(TestContext.Current.CancellationToken);
		await serviceProvider.DisposeAsync();
	}
}
