// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Concurrency")]
public sealed class PipelineProfileConcurrencyShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task KeepMembershipAndOrderingConsistentWhenAnAddWaitsForMutation(bool clear)
	{
		var profile = new PipelineProfile("concurrent", MessageKinds.All);
		// Hold the actual mutation monitor to put the writer at its lock boundary.
		var monitor = typeof(PipelineProfile).GetField("_orderedMiddleware", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(profile)!;
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var writer = new Thread(() =>
		{
			try
			{
				profile.AddMiddleware<Middleware>(0);
				completed.SetResult();
			}
			catch (Exception exception)
			{
				completed.SetException(exception);
			}
		}) { IsBackground = true };

		lock (monitor)
		{
			writer.Start();
			SpinWait.SpinUntil(() => (writer.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0,
				TimeSpan.FromSeconds(10)).ShouldBeTrue();
			if (clear)
			{
				profile.ClearMiddleware();
			}
			else
			{
				profile.RemoveMiddleware<Middleware>();
			}
		}

		await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
		profile.RemoveMiddleware<Middleware>();
		profile.MiddlewareEntries.ShouldBeEmpty();
		profile.AddMiddleware<Middleware>(0);
		profile.MiddlewareEntries.Count.ShouldBe(1);
	}

	private sealed class Middleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken) =>
			nextDelegate(message, context, cancellationToken);
	}
}
