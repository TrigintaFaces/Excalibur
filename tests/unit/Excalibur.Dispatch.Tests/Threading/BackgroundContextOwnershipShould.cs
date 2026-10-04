// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Threading;
using Excalibur.Dispatch.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Threading;

[Trait("Category", "Unit")]
[Trait("Component", "Pipeline")]
[Trait("Pattern", "Regression")]
public sealed class BackgroundContextOwnershipShould
{
	[Fact]
	public async Task OwnContextAndScopeAfterCallerReturns()
	{
		await using var root = new ServiceCollection().AddScoped<Dependency>().BuildServiceProvider();
		var request = root.CreateScope();
		var message = new BackgroundMessage();
		var original = new MessageContext(message, request.ServiceProvider) { MessageId = "original", CorrelationId = "correlation" };
		original.Items["value"] = "before";
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var ambient = new AsyncLocal<object?> { Value = new object() };
		var middleware = new BackgroundExecutionMiddleware(
			Microsoft.Extensions.Options.Options.Create(new BackgroundExecutionOptions()), NullLogger<BackgroundExecutionMiddleware>.Instance);
		var result = await middleware.InvokeAsync(message, original, async (_, context, _) =>
		{
			await release.Task.ConfigureAwait(false);
			try
			{
				context.ShouldNotBeSameAs(original);
				context.MessageId.ShouldBe("original");
				context.Items["value"].ShouldBe("before");
				ambient.Value.ShouldBeNull();
				MessageContextHolder.Current.ShouldBeSameAs(context);
				context.RequestServices.GetRequiredService<Dependency>().ShouldNotBeNull();
				observed.SetResult();
			}
			catch (Exception exception)
			{
				observed.SetException(exception);
			}
			return MessageResult.Success();
		}, CancellationToken.None);
		result.Disposition.ShouldBe(MessageDisposition.AcceptedForBackgroundExecution);
		original.Reset();
		request.Dispose();
		release.SetResult();
		await observed.Task.WaitAsync(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public async Task RefuseTransactionOwnershipBeforeAccepting()
	{
		var context = new MessageContext();
		context.SetFeature<IMessageTransactionFeature>(new MessageTransactionFeature { Transaction = new object() });
		var middleware = new BackgroundExecutionMiddleware(
			Microsoft.Extensions.Options.Options.Create(new BackgroundExecutionOptions()), NullLogger<BackgroundExecutionMiddleware>.Instance);
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await middleware.InvokeAsync(new BackgroundMessage(), context,
				static (_, _, _) => ValueTask.FromResult<IMessageResult>(MessageResult.Success()), CancellationToken.None));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DisposeOwnedAsyncScopeOnSuccessAndFailure(bool fail)
	{
		var dependency = new AsyncDependency();
		await using var root = new ServiceCollection().AddScoped(_ => dependency).BuildServiceProvider();
		var lifetime = A.Fake<Microsoft.Extensions.Hosting.IHostApplicationLifetime>();
		var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => lifetime.StopApplication()).Invokes(() => stopped.TrySetResult());
		var middleware = new BackgroundExecutionMiddleware(
			Microsoft.Extensions.Options.Options.Create(new BackgroundExecutionOptions
			{
				ExceptionBehavior = BackgroundExecutionExceptionBehavior.StopHost,
			}), NullLogger<BackgroundExecutionMiddleware>.Instance, lifetime);
		var message = new BackgroundMessage();
		var result = await middleware.InvokeAsync(message, new MessageContext(message, root), (_, context, _) =>
		{
			context.RequestServices.GetRequiredService<AsyncDependency>().ShouldBeSameAs(dependency);
			return ValueTask.FromResult<IMessageResult>(fail ? MessageResult.Failed("rejected") : MessageResult.Success());
		}, CancellationToken.None);
		result.Disposition.ShouldBe(MessageDisposition.AcceptedForBackgroundExecution);
		await dependency.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
		dependency.DisposeCount.ShouldBe(1);
		if (fail)
		{
			await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10));
			A.CallTo(() => lifetime.StopApplication()).MustHaveHappenedOnceExactly();
		}
	}

	[Fact]
	public async Task RefuseInboxBackgroundWorkBeforeAnyClaimOrTerminalCall()
	{
		var store = A.Fake<Excalibur.Dispatch.IInboxStore>();
		var middleware = new Excalibur.Dispatch.Middleware.Inbox.InboxMiddleware(
			Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.Options.Configuration.InboxConfigurationOptions { Enabled = true }),
			store, null, new Excalibur.Dispatch.Serialization.DispatchJsonSerializer(),
			NullLogger<Excalibur.Dispatch.Middleware.Inbox.InboxMiddleware>.Instance);
		var calls = 0;
		var result = await middleware.InvokeAsync(new BackgroundMessage(), new MessageContext(), (_, _, _) =>
		{
			calls++;
			return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
		}, CancellationToken.None);
		result.Succeeded.ShouldBeFalse();
		calls.ShouldBe(0);
		Fake.GetCalls(store).ShouldBeEmpty();
	}

	[Fact]
	public void CopyNamelessPrincipalAndDelegationWithoutSharingMutableClaims()
	{
		var actor = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("actor", "original")], "delegation");
		actor.Claims.Single().Properties["scope"] = "original";
		var identity = new System.Security.Claims.ClaimsIdentity([], "authenticated") { Actor = actor, Label = "delegated" };
		var principal = new System.Security.Claims.ClaimsPrincipal(identity);
		var context = new MessageContext();
		context.Items["UserId"] = principal.Identity!.Name!;
		context.Items["User"] = principal;
		var snapshot = BackgroundContextSnapshot.Capture(new BackgroundMessage(), context);
		context.Items["UserId"].ShouldBeNull();
		snapshot.Items["UserId"].ShouldBeNull();
		var copied = ((System.Security.Claims.ClaimsPrincipal)snapshot.Items["User"]).Identities.Single();
		copied.ShouldNotBeSameAs(identity);
		copied.IsAuthenticated.ShouldBeTrue();
		copied.Label.ShouldBe("delegated");
		copied.Actor.ShouldNotBeSameAs(actor);
		actor.Claims.Single().Properties["scope"] = "changed";
		actor.AddClaim(new System.Security.Claims.Claim("changed", "after"));
		copied.Actor!.Claims.ShouldHaveSingleItem().Value.ShouldBe("original");
		copied.Actor.Claims.Single().Properties["scope"].ShouldBe("original");
	}

	private sealed class AsyncDependency : IAsyncDisposable
	{
		public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public int DisposeCount { get; private set; }
		public ValueTask DisposeAsync()
		{
			DisposeCount++;
			Disposed.TrySetResult();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class Dependency;
	private sealed class BackgroundMessage : IDispatchAction, IExecuteInBackground;
}
