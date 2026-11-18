// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery.BatchProcessing;

namespace Excalibur.Dispatch.Tests.Messaging.BatchProcessing;

[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Pattern", "Regression")]
public sealed class BatchingOwnershipShould
{
	[Fact]
	public async Task JoinAnAdmittedConsumerBeforeReportingTheOriginalProducerFailure()
	{
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var producerFailed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var consumerPublication = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
		var failure = new InvalidOperationException("enumeration failed after consumer admission");
		IEnumerable<int> FailingProducer()
		{
			yield return 1;
#pragma warning disable RS0030 // Hold the real synchronous enumerator until a real batch consumer owns its item.
			entered.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
#pragma warning restore RS0030
			producerFailed.TrySetResult();
			throw failure;
		}
		async Task ConsumeAsync()
		{
			entered.TrySetResult();
			try { await release.Task; }
			finally { returned.TrySetResult(); }
		}
		var batch = Batching.ProcessBatchAsync(FailingProducer(), (_, _) =>
		{
			var consumer = ConsumeAsync();
			consumerPublication.TrySetResult(consumer);
			return consumer;
		}, 1, TimeSpan.FromSeconds(30), CancellationToken.None);
		try
		{
			await producerFailed.Task.WaitAsync(TimeSpan.FromSeconds(10));
			var prematureResult = await Record.ExceptionAsync(() => batch.WaitAsync(TimeSpan.FromSeconds(1)));
			prematureResult.ShouldBeOfType<TimeoutException>("the owned consumer is still held, so the batch must remain pending rather than orphan it");
			returned.Task.IsCompleted.ShouldBeFalse();
		}
		finally
		{
			release.TrySetResult();
			_ = await Record.ExceptionAsync(() => batch.WaitAsync(TimeSpan.FromSeconds(10)));
			if (entered.Task.IsCompleted)
			{
				var actualConsumer = await consumerPublication.Task.WaitAsync(TimeSpan.FromSeconds(10));
				await actualConsumer.WaitAsync(TimeSpan.FromSeconds(10));
			}
		}
		var observed = await Record.ExceptionAsync(() => batch.WaitAsync(TimeSpan.FromSeconds(10)));
		observed.ShouldBeSameAs(failure);
		returned.Task.IsCompletedSuccessfully.ShouldBeTrue("the actual consumer must have settled before producer failure is returned");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task RefuseANonpositiveConsumerCountBeforeEnumeratingOrSchedulingWork(int degree)
	{
		var enumerated = false;
		IEnumerable<int> Items()
		{
			enumerated = true;
			yield return 1;
		}
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() => Batching.ProcessBatchAsync(Items(),
			(_, _) => Task.CompletedTask, degree, TimeSpan.FromSeconds(10), CancellationToken.None));
		enumerated.ShouldBeFalse();
	}

	[Fact]
	public async Task PreserveCallerCancellationWhenAllScheduledWorkersAreCanceledBeforeAdmission()
	{
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		var calls = 0;
		await Should.ThrowAsync<OperationCanceledException>(() => Batching.ProcessBatchAsync([1, 2], (_, _) =>
		{
			Interlocked.Increment(ref calls);
			return Task.CompletedTask;
		}, 2, TimeSpan.FromSeconds(10), cancellation.Token).WaitAsync(TimeSpan.FromSeconds(10)));
		Volatile.Read(ref calls).ShouldBe(0);
	}
}
