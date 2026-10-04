// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Pipeline")]
[Trait("Pattern", "Regression")]
public sealed class PipelineFactoryLifetimeShould
{
	[Fact]
	public async Task ResolveLiveFactoryMiddlewareForEachDispatch()
	{
		await using var provider = new ServiceCollection().AddScoped<Probe>().BuildServiceProvider(
			new ServiceProviderOptions { ValidateScopes = true });
		var builder = new PipelineBuilder("Factory", provider);
		builder.Use(static services => services.GetRequiredService<Probe>());
		var pipeline = builder.Build();
		var observed = new List<Probe>();
		for (var i = 0; i < 2; i++)
		{
			await pipeline.ExecuteAsync(new Command(), new MessageContext(), (_, context, _) =>
			{
				var probe = context.RequestServices.GetRequiredService<Probe>();
				probe.Disposed.ShouldBeFalse();
				observed.Add(probe);
				return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
			}, CancellationToken.None);
		}
		observed.Count.ShouldBe(2);
		observed[0].ShouldNotBeSameAs(observed[1]);
		observed.ShouldAllBe(static probe => probe.Disposed);
	}

	[Fact]
	public async Task RejectARequiredFactoryThatStopsResolving()
	{
		await using var provider = new ServiceCollection().BuildServiceProvider();
		var calls = 0;
		var terminalCalls = 0;
		var builder = new PipelineBuilder("RequiredFactory", provider);
		builder.Use(_ => ++calls == 1 ? new Probe() : null!);
		var pipeline = builder.Build();
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await pipeline.ExecuteAsync(new Command(), new MessageContext(), (_, _, _) =>
			{
				terminalCalls++;
				return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
			}, CancellationToken.None));
		terminalCalls.ShouldBe(0);
	}

	[Fact]
	public async Task ActivateAsyncOnlyMiddlewareOnlyDuringDispatch()
	{
		var probes = new List<AsyncProbe>();
		await using var provider = new ServiceCollection().AddScoped(_ =>
		{
			var probe = new AsyncProbe();
			probes.Add(probe);
			return probe;
		}).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
		var builder = new PipelineBuilder("AsyncFactory", provider);
		builder.UseDeferred(static services => services.GetRequiredService<AsyncProbe>(),
			DispatchMiddlewareStage.PreProcessing, MessageKinds.Action);
		var pipeline = builder.Build();
		probes.ShouldBeEmpty();
		await pipeline.ExecuteAsync(new Command(), new MessageContext(), (_, context, _) =>
		{
			context.RequestServices.GetRequiredService<AsyncProbe>().Disposed.ShouldBeFalse();
			return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
		}, CancellationToken.None);
		probes.Count.ShouldBe(1);
		probes.ShouldAllBe(static probe => probe.Disposed);
	}

	[Fact]
	public async Task LeaveBorrowedScopeAliveAndSkipFilteredFactories()
	{
		await using var provider = new ServiceCollection().AddScoped<AsyncProbe>().BuildServiceProvider();
		var calls = 0;
		var builder = new PipelineBuilder("Borrowed", provider);
		builder.UseDeferred(services => { calls++; return services.GetRequiredService<AsyncProbe>(); },
			DispatchMiddlewareStage.PreProcessing, MessageKinds.Action);
		var pipeline = builder.Build();
		await using var scope = provider.CreateAsyncScope();
		var context = new MessageContext(new Command(), scope.ServiceProvider);
		DispatchRequestDelegate terminal = static (_, _, _) =>
			ValueTask.FromResult<IMessageResult>(MessageResult.Success());
		await pipeline.ExecuteAsync(new Event(), context, terminal, CancellationToken.None);
		calls.ShouldBe(0);
		await pipeline.ExecuteAsync(new Command(), context, terminal, CancellationToken.None);
		calls.ShouldBe(1);
		scope.ServiceProvider.GetRequiredService<AsyncProbe>().Disposed.ShouldBeFalse();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefuseMissingOrDifferentDeferredIdentityBeforeTerminal(bool derived)
	{
		await using var provider = new ServiceCollection().BuildServiceProvider();
		var builder = new PipelineBuilder("Identity", provider);
		builder.UseDeferred<IdentityProbe>(_ => derived ? new TenantReader() : null!,
			DispatchMiddlewareStage.Start, MessageKinds.All);
		var pipeline = builder.Build();
		var calls = 0;
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await pipeline.ExecuteAsync(new Command(), new MessageContext(), (_, _, _) =>
			{
				calls++;
				return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
			}, CancellationToken.None));
		calls.ShouldBe(0);
	}

	[Fact]
	public async Task RejectDeferredTenantReaderBeforeEstablisherWithoutActivatingEither()
	{
		await using var provider = new ServiceCollection().BuildServiceProvider();
		var builder = new PipelineBuilder("TenantOrder", provider);
		builder.UseDeferred<TenantReader>(_ => throw new InvalidOperationException("factory ran"),
			DispatchMiddlewareStage.Start, MessageKinds.All);
		builder.UseDeferred<TenantEstablisher>(_ => throw new InvalidOperationException("factory ran"),
			DispatchMiddlewareStage.Authorization, MessageKinds.All);
		Should.Throw<InvalidOperationException>(() => builder.Build()).Message.ShouldContain("IRequiresTenantContext");
	}

	private sealed class Event : IDispatchEvent;
	private class IdentityProbe : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;
		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
			=> nextDelegate(message, context, cancellationToken);
	}
	private sealed class TenantReader : IdentityProbe, IRequiresTenantContext;
	private sealed class TenantEstablisher : IdentityProbe, IEstablishesTenantContext;

	private sealed class AsyncProbe : IDispatchMiddleware, IAsyncDisposable
	{
		public bool Disposed { get; private set; }
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
			=> nextDelegate(message, context, cancellationToken);
		public async ValueTask DisposeAsync()
		{
			await Task.Yield();
			Disposed = true;
		}
	}

	private sealed class Command : IDispatchAction;

	private sealed class Probe : IDispatchMiddleware, IDisposable
	{
		public bool Disposed { get; private set; }
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			ObjectDisposedException.ThrowIf(Disposed, this);
			return nextDelegate(message, context, cancellationToken);
		}
		public void Dispose() => Disposed = true;
	}
}
