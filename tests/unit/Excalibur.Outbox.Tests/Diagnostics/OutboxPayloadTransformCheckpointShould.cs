// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.Diagnostics;
using Excalibur.MultiTenancy;
using Excalibur.Outbox.InMemory;
using Excalibur.Outbox.MultiTransport;

namespace Excalibur.Outbox.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class OutboxPayloadTransformCheckpointShould(ITestOutputHelper output)
{
	private static readonly string[] ExpectedTenants = ["tenant-one", "tenant-two"];

	[Fact]
	public async Task PreserveTheActualProviderWireAndPreparedContextAndOwnTheCallbackResult()
	{
		await using var harness = new Harness(encrypt: false);
		var facet = Facet(harness.Raw);
		var message = new InputMessage("before", 17);
		var context = Context(message, harness.Services);
		var returned = new byte[] { 9, 8, 7, 0, 255 };
		var calls = 0;
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var operation = facet.EnqueueWithPayloadTransformAsync(message, context, async (bytes, tenant, token) =>
		{
			calls++;
			bytes.ToArray().ShouldBe(Encoding.UTF8.GetBytes("{\"Secret\":\"before\",\"Sequence\":17}"));
			tenant.ShouldBe("before-tenant");
			entered.TrySetResult();
			await release.Task.WaitAsync(token);
			return returned;
		}, CancellationToken.None).AsTask();
		var failure = await ReleaseAndJoinAsync(harness, operation, entered, release, async () =>
		{
			await AssertEmptyAsync(harness.Raw);
			context.CorrelationId = "after-correlation";
			context.CausationId = "after-cause";
			context.GetOrCreateIdentityFeature().TenantId = "after-tenant";
			context.SetPropagatedItem("custom", "after");
		});
		Rethrow(failure);
		calls.ShouldBe(1);
		returned[0] = 77;
		var persisted = (await harness.Raw.GetUnsentMessagesAsync(10, CancellationToken.None)).ShouldHaveSingleItem();
		persisted.Payload.ShouldBe(new byte[] { 9, 8, 7, 0, 255 });
		persisted.MessageType.ShouldBe(typeof(InputMessage).FullName);
		persisted.Destination.ShouldBe(typeof(InputMessage).FullName);
		persisted.Id.ShouldNotBeNullOrWhiteSpace();
		persisted.Id.ShouldNotBe("caller-context-id");
		persisted.CorrelationId.ShouldBe("before-correlation");
		persisted.CausationId.ShouldBe("before-cause");
		persisted.TenantId.ShouldBe("before-tenant");
		persisted.Headers["custom"].ShouldBe("before");
		persisted.Headers.ContainsKey("request-only").ShouldBeFalse();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task RefuseCallbackFaultCancellationOrCanceledSuccessBeforeAcceptanceOrCapacityEviction(int kind)
	{
		await using var harness = new Harness(encrypt: false, maxMessages: 1);
		var seed = new OutboundMessage("seed", [33], "seed")
		{
			Id = "retained-terminal", Status = OutboxStatus.Sent, SentAt = DateTimeOffset.UtcNow.AddDays(-1),
		};
		await harness.Raw.StageMessageAsync(seed, CancellationToken.None);
		using var cancellation = new CancellationTokenSource();
		var expected = new IOException("exact callback fault");
		var message = new InputMessage("before", 17);
		var calls = 0;
		var operation = Facet(harness.Raw).EnqueueWithPayloadTransformAsync(message, Context(message, harness.Services), (_, _, token) =>
		{
			calls++;
			token.ShouldBe(cancellation.Token);
			if (kind == 0) { return ValueTask.FromException<byte[]>(expected); }
			cancellation.Cancel();
			return kind == 1 ? ValueTask.FromCanceled<byte[]>(token) : ValueTask.FromResult(new byte[] { 1, 2 });
		}, cancellation.Token);
		if (kind == 0)
		{
			(await Should.ThrowAsync<IOException>(async () => await operation)).ShouldBeSameAs(expected);
		}
		else
		{
			var error = await Should.ThrowAsync<OperationCanceledException>(async () => await operation);
			error.CancellationToken.ShouldBe(cancellation.Token);
		}
		calls.ShouldBe(1);
		var stats = await harness.Raw.GetAllTenantsStatisticsAsync(CancellationToken.None);
		stats.SentMessageCount.ShouldBe(1, "callback refusal must not evict an existing terminal row to make capacity");
		stats.StagedMessageCount.ShouldBe(0);
		(await harness.Raw.GetUnsentMessagesAsync(10, CancellationToken.None)).ShouldBeEmpty();
		// Matching positive: the actual original capacity policy now accepts and replaces that terminal row.
		await Facet(harness.Raw).EnqueueWithPayloadTransformAsync(message, Context(message, harness.Services),
			(_, _, _) => ValueTask.FromResult(new byte[] { 4, 5 }), CancellationToken.None);
		var accepted = (await harness.Raw.GetUnsentMessagesAsync(10, CancellationToken.None)).ShouldHaveSingleItem();
		accepted.Payload.ShouldBe(new byte[] { 4, 5 });
		(await harness.Raw.GetAllTenantsStatisticsAsync(CancellationToken.None)).SentMessageCount.ShouldBe(0);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task RejectNullCallbackNullResultAndPreCanceledEntryWithoutAcceptingRows(int kind)
	{
		await using var harness = new Harness(encrypt: false);
		var message = new GetterMessage();
		var calls = 0;
		using var cancellation = new CancellationTokenSource();
		if (kind == 2) { await cancellation.CancelAsync(); }
		Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transform = (_, _, _) =>
		{
			calls++;
			return ValueTask.FromResult<byte[]>(null!);
		};
		var context = Context(message, harness.Services);
		if (kind == 0)
		{
			var error = await Should.ThrowAsync<ArgumentNullException>(async () => await Facet(harness.Raw)
				.EnqueueWithPayloadTransformAsync(message, context, null!, cancellation.Token));
			error.ParamName.ShouldBe("transformPayload");
		}
		else if (kind == 1)
		{
			(await Should.ThrowAsync<InvalidOperationException>(async () => await Facet(harness.Raw)
				.EnqueueWithPayloadTransformAsync(message, context, transform, cancellation.Token))).Message.ShouldContain("null");
		}
		else
		{
			var error = await Should.ThrowAsync<OperationCanceledException>(async () => await Facet(harness.Raw)
				.EnqueueWithPayloadTransformAsync(message, context, transform, cancellation.Token));
			error.CancellationToken.ShouldBe(cancellation.Token);
		}
		calls.ShouldBe(kind == 1 ? 1 : 0);
		if (kind != 1) { message.Reads.ShouldBe(0, "refusal must happen before provider serialization"); }
		else { message.Reads.ShouldBeGreaterThan(0); }
		await AssertEmptyAsync(harness.Raw);
	}

	[Fact]
	public async Task KeepOwningAliasesAndHideTheRawFacetAcrossEncryption()
	{
		await using var harness = new Harness();
		var store = harness.Services.GetRequiredKeyedService<IOutboxStore>("default");
		store.ShouldBeSameAs(harness.Services.GetRequiredService<IOutboxStore>());
		store.ShouldBeSameAs(harness.Services.GetRequiredKeyedService<IOutboxStore>("inmemory"));
		Facet(harness.Raw).ShouldBeSameAs(harness.Raw);
		store.GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeNull("an identity transform cannot bypass encryption");
		typeof(IOutboxStore).IsAssignableFrom(typeof(IOutboxPayloadTransformStore)).ShouldBeFalse();
		var standalone = new ServiceCollection();
		standalone.AddSingleton(Facet(harness.Raw));
		await using var facetOnly = standalone.BuildServiceProvider();
		facetOnly.GetService<IOutboxStore>().ShouldBeNull("a standalone facet does not supply the primary store/worker registration");
	}

	[Fact]
	public async Task RefuseMissingFacetWithoutLosingPreencodedStageSupport()
	{
		await using var harness = new Harness(stageOnly: true);
		var message = new InputMessage("before", 17);
		var error = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await harness.Store.EnqueueAsync(message, Context(message, harness.Services), CancellationToken.None));
		error.Message.ShouldContain(nameof(IOutboxPayloadTransformStore));
		error.Message.ShouldContain(nameof(IOutboxStore.StageMessageAsync));
		await AssertEmptyAsync(harness.Raw);
		await harness.Store.StageMessageAsync(new OutboundMessage("encoded", [1, 2, 3], "destination") { TenantId = "before-tenant" }, CancellationToken.None);
		var stored = (await harness.Raw.GetUnsentMessagesAsync(1, CancellationToken.None)).ShouldHaveSingleItem();
		EncryptedData.IsFieldEncrypted(stored.Payload).ShouldBeTrue();
	}

	[Fact]
	public async Task PreserveLegacyForwardingWithoutActivatingStageOnlyTransportBindings()
	{
		await using var harness = new Harness(strictTransport: true);
		var message = new InputMessage("before", 17);
		await harness.Store.EnqueueAsync(message, Context(message, harness.Services), CancellationToken.None);
		var row = (await harness.Raw.GetUnsentMessagesAsync(1, CancellationToken.None)).ShouldHaveSingleItem();
		EncryptedData.IsFieldEncrypted(row.Payload).ShouldBeTrue();
		row.Destination.ShouldBe(typeof(InputMessage).FullName);
		row.TargetTransports.ShouldBeNull("the old inherited Enqueue never ran MultiTransport.Stage binding");
	}

	[Fact]
	public async Task SecureTheInheritedFencedEnqueueView()
	{
		await using var harness = new Harness();
		var fenced = harness.Store.GetService(typeof(IFencedOutboxStore)).ShouldBeAssignableTo<IFencedOutboxStore>();
		var message = new InputMessage("before", 17);
		await fenced.EnqueueAsync(message, Context(message, harness.Services), CancellationToken.None);
		EncryptedData.IsFieldEncrypted((await harness.Raw.GetUnsentMessagesAsync(1, CancellationToken.None)).ShouldHaveSingleItem().Payload).ShouldBeTrue();
	}

	[Theory]
	[InlineData(false, 0)]
	[InlineData(true, 0)]
	[InlineData(false, 1)]
	[InlineData(true, 1)]
	[InlineData(false, 2)]
	[InlineData(true, 2)]
	public async Task PreserveTelemetryEnqueueAccountingAndCryptoInputOwnershipInBothDecoratorOrders(bool outside, int outcome)
	{
		await using var harness = new Harness(telemetry: true, telemetryOutside: outside, observe: true);
		using var activity = new Activity("owned-enqueue-transform-test").Start();
		using var measurements = new Measurements(activity);
		using var cancellation = new CancellationTokenSource();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var expected = new IOException("actual crypto callback fault");
		harness.Encrypt = async (bytes, context, token) =>
		{
			entered.TrySetResult();
			await release.Task.WaitAsync(token);
			if (outcome == 1) { throw expected; }
			if (outcome == 2)
			{
				cancellation.Cancel();
				return await Task.FromCanceled<EncryptedData>(token);
			}
			return await harness.RealProvider.EncryptAsync(bytes, context, token);
		};
		var message = new InputMessage("before", 17);
		var operation = harness.Store.EnqueueAsync(message, Context(message, harness.Services), cancellation.Token).AsTask();
		var failure = await ReleaseAndJoinAsync(harness, operation, entered, release, async () =>
		{
			measurements.Samples.ShouldBeEmpty("no successful enqueue is measured while crypto is pending");
			await AssertEmptyAsync(harness.Raw);
			var borrowed = harness.ObservedInputs.ShouldHaveSingleItem();
			MemoryMarshal.TryGetArray(borrowed, out var segment).ShouldBeTrue();
			harness.CryptoInput.ShouldNotBeSameAs(segment.Array, "crypto must own a copy before awaiting");
			harness.CryptoInput.ShouldBe(borrowed.ToArray());
		});
		if (outcome == 0)
		{
			Rethrow(failure);
			var own = measurements.Samples.Where(sample => sample.Operation == "enqueue").ToArray();
			own.Length.ShouldBe(3);
			own.Single(sample => sample.Name == "excalibur.outbox.store.operations").Value.ShouldBe(1);
			own.Single(sample => sample.Name == "excalibur.outbox.store.messages").Value.ShouldBe(1);
			own.Single(sample => sample.Name == "excalibur.outbox.store.operation_duration").Value.ShouldBeGreaterThanOrEqualTo(0);
			measurements.Samples.ShouldNotContain(sample => sample.Operation == "stage");
			EncryptedData.IsFieldEncrypted((await harness.Raw.GetUnsentMessagesAsync(1, CancellationToken.None)).ShouldHaveSingleItem().Payload).ShouldBeTrue();
		}
		else
		{
			if (outcome == 1) { failure.ShouldBeSameAs(expected); }
			else { failure.ShouldBeAssignableTo<OperationCanceledException>().CancellationToken.ShouldBe(cancellation.Token); }
			measurements.Samples.ShouldBeEmpty("current telemetry counts success only, never a callback failure as success");
			await AssertEmptyAsync(harness.Raw);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PreserveActualRowDiscriminatorAndEncryptionRegistrationOrdersWithoutAnAmbientDrainTenant(bool tenantFirst)
	{
		await using var harness = new Harness(rowDiscriminator: true, tenantFirst: tenantFirst);
		harness.Store.GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeNull();
		harness.Store.ShouldBeSameAs(harness.Services.GetRequiredService<IOutboxStore>());
		harness.Store.ShouldBeSameAs(harness.Services.GetRequiredKeyedService<IOutboxStore>("inmemory"));
		foreach (var tenant in new[] { "tenant-one", "tenant-two" })
		{
			var message = new InputMessage(tenant, 17);
			var context = Context(message, harness.Services);
			context.GetOrCreateIdentityFeature().TenantId = tenant;
			await harness.Store.EnqueueAsync(message, context, CancellationToken.None);
		}
		var claimed = (await harness.Raw.GetUnsentMessagesAsync(10, CancellationToken.None)).ToArray();
		claimed.Length.ShouldBe(2, "the actual outbox tenant gate attests row provenance; it must not add an ambient-tenant drain filter");
		claimed.Select(row => row.TenantId).OrderBy(tenant => tenant, StringComparer.Ordinal).ShouldBe(ExpectedTenants);
		var completion = ((IOutboxStore)harness.Raw).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>();
		foreach (var row in claimed)
		{
			EncryptedData.IsFieldEncrypted(row.Payload).ShouldBeTrue();
			(await completion.MarkFailedAsync(row.Id, "read-control", 1, row.DispatcherId!, CancellationToken.None))
				.ShouldBe(OutboxCompletionOutcome.Applied);
		}
		var admin = harness.Store.GetService(typeof(IOutboxStoreAdmin)).ShouldBeAssignableTo<IOutboxStoreAdmin>();
		var read = (await admin.GetAllTenantsFailedMessagesAsync(0, null, 10, CancellationToken.None)).ToArray();
		read.Length.ShouldBe(2);
		foreach (var row in read)
		{
			using var json = System.Text.Json.JsonDocument.Parse(row.Payload);
			json.RootElement.GetProperty("Secret").GetString().ShouldBe(row.TenantId);
		}
	}

	[Fact]
	public async Task RefuseAnUnattestedPrimaryEvenWhenAnActualTransformFacetIsRegisteredSeparately()
	{
		await using var harness = new Harness(encrypt: false);
		var services = new ServiceCollection();
		services.AddSingleton(Facet(harness.Raw));
		services.AddSingleton<IOutboxStore>(new StageOnlyStore(harness.Raw));
		var error = Should.Throw<InvalidOperationException>(() => services.AddMultiTenancy(options => options.Strategy = TenantIsolationStrategy.RowDiscriminator));
		error.Message.ShouldContain(nameof(IOutboxStore));
		await AssertEmptyAsync(harness.Raw);
	}

	private async Task<Exception?> ReleaseAndJoinAsync(Harness harness, Task operation, TaskCompletionSource entered,
		TaskCompletionSource release, Func<Task> assertions)
	{
		Exception? primary = null;
		Exception? operationFailure = null;
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		try { await entered.Task.WaitAsync(deadline.Token); await assertions(); }
		catch (Exception error) { primary = error; }
		finally
		{
			release.TrySetResult();
			using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			try { await operation.WaitAsync(cleanup.Token); }
			catch (Exception error)
			{
				operationFailure = error;
				if (!operation.IsCompleted) { harness.DeferDisposalUntil(operation); }
			}
		}
		if (primary is not null)
		{
			if (operationFailure is not null) { output.WriteLine("SECONDARY_OPERATION_FAILURE: " + operationFailure.GetType().FullName); }
			ExceptionDispatchInfo.Capture(primary).Throw();
		}
		return operationFailure;
	}
	private static void Rethrow(Exception? failure) { if (failure is not null) { ExceptionDispatchInfo.Capture(failure).Throw(); } }
	private static IOutboxPayloadTransformStore Facet(IOutboxStore store)
	{
		var capability = store.GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeAssignableTo<IOutboxPayloadTransformStore>();
		capability.ShouldNotBeNull();
		return capability;
	}
	private static async Task AssertEmptyAsync(InMemoryOutboxStore store)
	{
		var stats = await store.GetAllTenantsStatisticsAsync(CancellationToken.None);
		(stats.StagedMessageCount + stats.SendingMessageCount + stats.SentMessageCount + stats.FailedMessageCount + stats.ScheduledMessageCount).ShouldBe(0);
	}
	private static MessageContext Context(IDispatchMessage message, IServiceProvider services)
	{
		var context = new MessageContext(message, services)
		{
			MessageId = "caller-context-id", CorrelationId = "before-correlation", CausationId = "before-cause",
		};
		context.GetOrCreateIdentityFeature().TenantId = "before-tenant";
		context.SetPropagatedItem("custom", "before");
		context.Items["request-only"] = "never-serialize";
		context.SetDestination("context-destination-not-used-by-legacy-inmemory");
		return context;
	}
	private sealed record InputMessage(string Secret, int Sequence) : IDispatchMessage;
	private sealed class GetterMessage : IDispatchMessage
	{
		internal int Reads { get; private set; }
		public string Secret { get { Reads++; return "probe"; } }
	}
	private sealed class StageOnlyStore(IOutboxStore inner) : IsolatingOutboxStoreDecorator(inner);
	private sealed class ObservingTransformStore : OutboxStoreDecorator, IOutboxPayloadTransformStore
	{
		private readonly IOutboxPayloadTransformStore _facet;
		private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _observed;
		internal ObservingTransformStore(IOutboxStore inner, ConcurrentQueue<ReadOnlyMemory<byte>> observed) : base(inner)
		{
			_facet = Facet(inner);
			_observed = observed;
		}
		[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public ValueTask EnqueueWithPayloadTransformAsync(IDispatchMessage message, IMessageContext context,
			Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transformPayload, CancellationToken cancellationToken) =>
			_facet.EnqueueWithPayloadTransformAsync(message, context, (bytes, tenant, token) =>
			{
				_observed.Enqueue(bytes);
				return transformPayload(bytes, tenant, token);
			}, cancellationToken);
	}
	private sealed record Measurement(string Name, double Value, string? Operation);
	private sealed class Measurements : IDisposable
	{
		private readonly MeterListener _listener = new();
		internal ConcurrentQueue<Measurement> Samples { get; } = new();
		internal Measurements(Activity activity)
		{
			_listener.InstrumentPublished = (instrument, listener) =>
			{
				if (instrument.Meter.Name == "Excalibur.Outbox.Store") { listener.EnableMeasurementEvents(instrument); }
			};
			_listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(activity, instrument, value, tags));
			_listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(activity, instrument, value, tags));
			_listener.Start();
		}
		private void Record(Activity activity, Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
		{
			if (!ReferenceEquals(Activity.Current, activity)) { return; }
			string? operation = null;
			foreach (var tag in tags) { if (tag.Key == "operation") { operation = tag.Value as string; } }
			Samples.Enqueue(new Measurement(instrument.Name, value, operation));
		}
		public void Dispose() => _listener.Dispose();
	}
	private sealed class Harness : IAsyncDisposable
	{
		private readonly InMemoryKeyManagementProvider _keys = new(NullLogger<InMemoryKeyManagementProvider>.Instance,
			new InMemoryKeyManagementOptions { AutoGenerateDefaultKey = true });
		internal AesGcmEncryptionProvider RealProvider { get; }
		internal Func<byte[], EncryptionContext, CancellationToken, Task<EncryptedData>> Encrypt { get; set; }
		internal byte[]? CryptoInput { get; private set; }
		internal ConcurrentQueue<ReadOnlyMemory<byte>> ObservedInputs { get; } = new();
		internal ServiceProvider Services { get; }
		internal InMemoryOutboxStore Raw { get; }
		internal IOutboxStore Store { get; }
		private Task? _deferred;
		internal Harness(bool encrypt = true, int maxMessages = 10000, bool telemetry = false,
			bool telemetryOutside = false, bool observe = false, bool stageOnly = false, bool strictTransport = false,
			bool rowDiscriminator = false, bool tenantFirst = false)
		{
			RealProvider = new AesGcmEncryptionProvider(_keys, NullLogger<AesGcmEncryptionProvider>.Instance);
			Encrypt = RealProvider.EncryptAsync;
			var encryption = A.Fake<IEncryptionProvider>();
			A.CallTo(() => encryption.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
				.ReturnsLazily((byte[] bytes, EncryptionContext context, CancellationToken token) =>
				{
					CryptoInput = bytes;
					return Encrypt(bytes, context, token);
				});
			var registry = A.Fake<IEncryptionProviderRegistry>();
			A.CallTo(() => registry.GetPrimary()).Returns(encryption);
			A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(RealProvider);
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddInMemoryOutboxStore(options => options.MaxMessages = maxMessages);
			services.AddSingleton(registry);
			services.Configure<EncryptionOptions>(options => options.Mode = EncryptionMode.EncryptAndDecrypt);
			if (stageOnly) { services.DecorateKeyedStores<IOutboxStore>((inner, _) => new StageOnlyStore(inner)).ShouldBeTrue(); }
			if (strictTransport)
			{
				services.DecorateKeyedStores<IOutboxStore>((inner, _) => new MultiTransportOutboxStore(inner,
					Microsoft.Extensions.Options.Options.Create(new MultiTransportOutboxOptions { RequireExplicitBindings = true }),
					NullLogger<MultiTransportOutboxStore>.Instance)).ShouldBeTrue();
			}
			if (observe) { services.DecorateKeyedStores<IOutboxStore>((inner, _) => new ObservingTransformStore(inner, ObservedInputs)).ShouldBeTrue(); }
			if (telemetry && !telemetryOutside) { services.DecorateKeyedStores<IOutboxStore>((inner, _) => new TelemetryOutboxStoreDecorator(inner)).ShouldBeTrue(); }
			if (rowDiscriminator && tenantFirst) { services.AddMultiTenancy(options => options.Strategy = TenantIsolationStrategy.RowDiscriminator); }
			if (encrypt) { services.AddOutboxEncryption(); }
			if (rowDiscriminator && !tenantFirst) { services.AddMultiTenancy(options => options.Strategy = TenantIsolationStrategy.RowDiscriminator); }
			if (telemetry && telemetryOutside) { services.DecorateKeyedStores<IOutboxStore>((inner, _) => new TelemetryOutboxStoreDecorator(inner)).ShouldBeTrue(); }
			Services = services.BuildServiceProvider(validateScopes: true);
			Raw = Services.GetRequiredService<InMemoryOutboxStore>();
			Store = Services.GetRequiredKeyedService<IOutboxStore>("default");
		}
		internal void DeferDisposalUntil(Task operation) => _deferred = operation;
		public async ValueTask DisposeAsync()
		{
			if (_deferred is { IsCompleted: false } pending) { _ = DisposeAfterOperationAsync(pending); return; }
			await DisposeCoreAsync();
		}
		private async Task DisposeAfterOperationAsync(Task operation)
		{
			try { await operation.ConfigureAwait(false); }
			catch (Exception error) { Trace.WriteLine("Late operation fault: " + error.GetType().FullName); }
			finally { await DisposeCoreAsync(); }
		}
		private async ValueTask DisposeCoreAsync() { await Services.DisposeAsync(); RealProvider.Dispose(); _keys.Dispose(); }
	}
}
