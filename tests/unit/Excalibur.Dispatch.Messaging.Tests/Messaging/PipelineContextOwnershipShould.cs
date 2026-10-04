// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.ZeroAlloc;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Pipeline")]
[Trait("Pattern", "Regression")]
public sealed class PipelineContextOwnershipShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void ReturnRentedContextWhenDispatchThrowsSynchronously(bool typed)
	{
		var previous = MessageContextHolder.Current;
		MessageContextHolder.Current = null;
		try
		{
			var factory = A.Fake<IMessageContextFactory>();
			var context = new MessageContext();
			A.CallTo(() => factory.CreateContext()).Returns(context);
			using var provider = new ServiceCollection().AddSingleton(factory).BuildServiceProvider();
			var dispatcher = A.Fake<IDispatcher>();
			A.CallTo(() => dispatcher.ServiceProvider).Returns(provider);
			var message = new Command();
			A.CallTo(() => dispatcher.DispatchAsync(message, context, CancellationToken.None)).Throws<InvalidOperationException>();
			A.CallTo(() => dispatcher.DispatchAsync<Command, string>(message, context, CancellationToken.None)).Throws<InvalidOperationException>();
			if (typed)
			{
				Should.Throw<InvalidOperationException>(() => DispatcherContextExtensions.DispatchAsync<Command, string>(dispatcher, message, CancellationToken.None));
			}
			else
			{
				Should.Throw<InvalidOperationException>(() => DispatcherContextExtensions.DispatchAsync<Command>(dispatcher, message, CancellationToken.None));
			}
			A.CallTo(() => factory.Return(context)).MustHaveHappenedOnceExactly();
		}
		finally
		{
			MessageContextHolder.Current = previous;
		}
	}

	[Fact]
	public async Task RebindProviderOnSharedPoolRent()
	{
		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var thread = new Thread(() =>
		{
			try
			{
				using var root = new ServiceCollection().BuildServiceProvider();
				using var request = root.CreateScope();
				var pool = new MessageContextPool(root);
				var contexts = Enumerable.Range(0, 5).Select(_ => pool.Rent()).ToArray();
				contexts[4].RequestServices = request.ServiceProvider;
				foreach (var context in contexts)
				{
					pool.ReturnToPool(context);
				}
				var rented = Enumerable.Range(0, 5).Select(_ => pool.Rent()).ToArray();
				rented[4].ShouldBeSameAs(contexts[4]);
				rented.ShouldAllBe(context => ReferenceEquals(context.RequestServices, root));
				foreach (var context in rented)
				{
					pool.ReturnToPool(context);
				}
				completion.SetResult();
			}
			catch (Exception exception)
			{
				completion.SetException(exception);
			}
		}) { IsBackground = true };
		thread.Start();
		await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
	}

	private sealed class Command : IDispatchAction<string>;
}
