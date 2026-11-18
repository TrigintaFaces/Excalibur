// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Transactions;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;

using FakeItEasy;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

namespace Excalibur.Outbox.Oracle.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class OracleWorkerOwnershipShould
{
	private const string Unopened = "User Id=unit_only;Password=unit_only;Data Source=unit.invalid:1521/unit";

	[Fact]
	public async Task RejectDuplicateHandoutWithoutDisposingFirstLeaseAndRetainFirstScope()
	{
		await using var services = new ServiceCollection().AddScoped<ScopeStamp>().BuildServiceProvider(validateScopes: true);
		var connection = new RecordingOracleConnection(Unopened);
		var stamps = new List<ScopeStamp>();
		var source = OracleWorkerConnectionSource.FromFactory(services.GetRequiredService<IServiceScopeFactory>(), (scope, _) =>
		{
			stamps.Add(scope.GetRequiredService<ScopeStamp>());
			return ValueTask.FromResult<OracleConnection>(connection);
		});
		await using (var first = await source.AcquireAsync(NullLogger.Instance, CancellationToken.None))
		{
			_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
				await source.AcquireAsync(NullLogger.Instance, CancellationToken.None));
			connection.DisposeAsyncCalls.ShouldBe(0, "the duplicate rejection must not dispose the first operation's connection");
			stamps.Count.ShouldBe(2);
			stamps[0].Disposed.ShouldBeFalse();
			stamps[1].Disposed.ShouldBeTrue();
			using var command = first.Connection.CreateCommand();
			command.Connection.ShouldBeSameAs(connection);
		}
		connection.DisposeAsyncCalls.ShouldBe(1);
		stamps[0].Disposed.ShouldBeTrue();
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await source.AcquireAsync(NullLogger.Instance, CancellationToken.None));
		connection.DisposeAsyncCalls.ShouldBe(1, "a later reuse does not acquire disposal ownership either");
	}

	[Fact]
	public async Task RejectAmbientScopeBeforeFactoryInvocation()
	{
		await using var services = new ServiceCollection().BuildServiceProvider();
		var calls = 0;
		var source = OracleWorkerConnectionSource.FromFactory(services.GetRequiredService<IServiceScopeFactory>(), (_, _) =>
		{
			calls++;
			return ValueTask.FromResult(new OracleConnection(Unopened));
		});
		using var ambient = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await source.AcquireAsync(NullLogger.Instance, CancellationToken.None));
		calls.ShouldBe(0);
	}

	[Fact]
	public async Task DisposeCallbackScopeWhenFactoryThrowsWithoutReplacingItsError()
	{
		await using var services = new ServiceCollection().AddScoped<ScopeStamp>().BuildServiceProvider(validateScopes: true);
		ScopeStamp? stamp = null;
		var expected = new IOException("factory failure");
		var source = OracleWorkerConnectionSource.FromFactory(services.GetRequiredService<IServiceScopeFactory>(), (scope, _) =>
		{
			stamp = scope.GetRequiredService<ScopeStamp>();
			throw expected;
		});
		var actual = await Should.ThrowAsync<IOException>(async () =>
			await source.AcquireAsync(NullLogger.Instance, CancellationToken.None));
		actual.ShouldBeSameAs(expected);
		stamp.ShouldNotBeNull().Disposed.ShouldBeTrue();
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task BorrowedStagingOnlyHostStartsButAnEnabledWorkerFailsBeforeFactoryTraffic(bool worker, bool elected)
	{
		var calls = 0;
		var db = A.Fake<IDb>();
		var builder = Host.CreateApplicationBuilder();
		builder.Services.AddSingleton(A.Fake<IOutboxPublisher>());
		if (elected)
		{
			builder.Services.AddSingleton(A.Fake<ILeaderElection>());
			builder.Services.AddSingleton(A.Fake<ILeaderProcessingGate>());
		}
		builder.Services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
		{
			outbox.UseOracle(oracle => oracle.ConnectionFactory(_ => { calls++; return db; }));
			if (worker) { outbox.EnableBackgroundProcessing(); }
		}));
		using var host = builder.Build();
		if (worker)
		{
			var error = await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync());
			error.ToString().ShouldContain("owned worker connection");
		}
		else
		{
			await host.StartAsync();
			await host.StopAsync();
		}
		calls.ShouldBe(1, "the borrowed staging lifecycle is resolved normally, never mined for worker credentials");
		A.CallTo(() => db.Connection).MustNotHaveHappened();
	}

	[Theory]
	[InlineData("direct")]
	[InlineData("named")]
	[InlineData("bound")]
	public async Task ResolveOwnedWorkerModesWithoutTouchingUnrelatedGlobalDb(string mode)
	{
		var unrelated = A.Fake<IDb>();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(unrelated);
		services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["ConnectionStrings:outbox"] = Unopened,
			["Oracle:ConnectionString"] = Unopened,
			["Oracle:OutboxTableName"] = "CUSTOM_OUTBOX",
		}).Build());
		services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseOracle(oracle =>
		{
			if (mode == "direct") { oracle.ConnectionString(Unopened); }
			else if (mode == "named") { oracle.ConnectionStringName("outbox"); }
			else { oracle.BindConfiguration("Oracle"); }
		})));
		await using var provider = services.BuildServiceProvider(validateScopes: true);
		var store = provider.GetRequiredService<IOutboxStore>();
		store.ShouldBeSameAs(provider.GetRequiredKeyedService<IOutboxStore>("oracle"));
		store.ShouldBeSameAs(provider.GetRequiredKeyedService<IOutboxStore>("default"));
		store.GetService(typeof(IClaimScopedOutboxStore)).ShouldBeSameAs(store);
		A.CallTo(() => unrelated.Connection).MustNotHaveHappened();
	}

	[Fact]
	public async Task RejectStrongCompletionWithoutWorkerFactoryBeforeTouchingBorrowedConnection()
	{
		var borrowed = A.Fake<IDb>();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
			outbox.UseOracle(oracle => oracle.ConnectionFactory(_ => borrowed))));
		await using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredService<OracleOutboxStore>();
		((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeNull();
		((IOutboxStore)store).GetService(typeof(IFencedClaimScopedOutboxStore)).ShouldBeNull();
		var failure = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await store.MarkSentAsync("message", "claim", CancellationToken.None));
		failure.Message.ShouldContain("WorkerConnectionFactory");
		A.CallTo(() => borrowed.Connection).MustNotHaveHappened();
	}

	[Fact]
	public async Task PreservePrimaryFactoryFailureWhenCallbackScopeDisposalAlsoFails()
	{
		var scopeDisposed = false;
		await using var services = new ServiceCollection().AddScoped(_ => new ScopeStamp(() => scopeDisposed = true, true)).BuildServiceProvider();
		var expected = new IOException("primary factory failure");
		var source = OracleWorkerConnectionSource.FromFactory(services.GetRequiredService<IServiceScopeFactory>(), (scope, cancellationToken) =>
		{
			_ = scope.GetRequiredService<ScopeStamp>();
			throw expected;
		});
		var actual = await Should.ThrowAsync<IOException>(async () =>
			await source.AcquireAsync(NullLogger.Instance, CancellationToken.None));
		actual.ShouldBeSameAs(expected);
		scopeDisposed.ShouldBeTrue();
	}

	[Fact]
	public async Task DisposeConnectionBeforeScopeAndPreserveAnExistingPrimaryErrorAcrossBothCleanupFailures()
	{
		var connectionDisposed = false;
		var scopeDisposed = false;
		await using var services = new ServiceCollection().AddScoped(_ => new ScopeStamp(() =>
		{
			connectionDisposed.ShouldBeTrue("factory dependencies stay alive through connection disposal");
			scopeDisposed = true;
		}, true)).BuildServiceProvider();
		var connection = new RecordingOracleConnection(Unopened,
			afterDispose: () => connectionDisposed = true, cleanupFailure: new IOException("connection cleanup"));
		var source = OracleWorkerConnectionSource.FromFactory(services.GetRequiredService<IServiceScopeFactory>(), (scope, cancellationToken) =>
		{
			_ = scope.GetRequiredService<ScopeStamp>();
			return ValueTask.FromResult<OracleConnection>(connection);
		});
		var lease = await source.AcquireAsync(NullLogger.Instance, CancellationToken.None);
		var primary = new InvalidOperationException("original operation");
		var actual = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			try { throw primary; }
			finally { await lease.DisposePreservingAsync(primary); }
		});
		actual.ShouldBeSameAs(primary);
		connectionDisposed.ShouldBeTrue("the cleanup-failure control must actually await base SDK disposal");
		connection.DisposeAsyncCalls.ShouldBe(1);
		connection.BaseDisposeCompleted.ShouldBeTrue();
		scopeDisposed.ShouldBeTrue();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ExplicitWorkerFactorySurvivesEitherPrimarySelectionOrder(bool workerFirst)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		var expected = new IOException("arranged callback before opening");
		services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseOracle(oracle =>
		{
			if (workerFirst) { oracle.WorkerConnectionFactory((_, _) => throw expected); }
			oracle.ConnectionString(Unopened);
			if (!workerFirst) { oracle.WorkerConnectionFactory((_, _) => throw expected); }
		})));
		await using var provider = services.BuildServiceProvider(validateScopes: true);
		var store = provider.GetRequiredService<OracleOutboxStore>();
		var actual = await Should.ThrowAsync<IOException>(async () =>
			await store.MarkSentAsync("message", "claim", CancellationToken.None));
		actual.ShouldBeSameAs(expected);
	}

	// Observe the actual typed SDK disposal boundary, not Component.Disposed (which pinned ODP.NET
	// does not raise). Production factory/lease/helper types and implementation remain unchanged.
	private sealed class RecordingOracleConnection(string connectionString, Action? afterDispose = null,
		Exception? cleanupFailure = null) : OracleConnection(connectionString)
	{
		private int _disposeAsyncCalls;
		internal int DisposeAsyncCalls => Volatile.Read(ref _disposeAsyncCalls);
		internal bool BaseDisposeCompleted { get; private set; }
		public override async ValueTask DisposeAsync()
		{
			Interlocked.Increment(ref _disposeAsyncCalls);
			await base.DisposeAsync().ConfigureAwait(false);
			BaseDisposeCompleted = true;
			afterDispose?.Invoke();
			if (cleanupFailure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupFailure).Throw(); }
		}
	}

	private sealed class ScopeStamp : IDisposable
	{
		private readonly Action? _onDispose;
		private readonly bool _fail;
		public ScopeStamp() { }
		internal ScopeStamp(Action onDispose, bool fail) { _onDispose = onDispose; _fail = fail; }
		internal bool Disposed { get; private set; }
		public void Dispose()
		{
			Disposed = true;
			_onDispose?.Invoke();
			if (_fail) { throw new IOException("scope cleanup"); }
		}
	}
}
