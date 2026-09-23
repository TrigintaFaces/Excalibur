// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Excalibur.Dispatch;
using Excalibur.Inbox.Firestore;

using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;

using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// The Firestore inbox store's handling of optimistic-concurrency EXHAUSTION on mark-failed.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this proves, and what it does not.</b> It proves the STORE's loop: given N consecutive
/// precondition failures at a bound of N, the store reports <see cref="InboxMarkFailedOutcome.Undecided"/>,
/// does not throw, and attempted exactly N preconditioned commits. It does NOT prove that concurrent writers
/// produce those failures -- the staleness is forced by the interceptor below, not by a rival writer. That
/// is the real-infrastructure lost-race arms' job, and a green here must not be read as discharging it.
/// </para>
/// <para>
/// The failures themselves are genuine: every call reaches the real emulator, and the interceptor only
/// rewrites a commit's update-time precondition to a time the document was never at. The emulator refuses
/// it with a real <c>FAILED_PRECONDITION</c>, which the real SDK surfaces as its real exception.
/// </para>
/// </remarks>
[Collection(FirestoreInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
public sealed class FirestoreInboxOccExhaustionShould
{
	private const int Bound = 3;

	private readonly FirestoreInboxStoreContainerFixture _fixture;

	public FirestoreInboxOccExhaustionShould(FirestoreInboxStoreContainerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task ReportUndecided_AfterExactlyTheBoundOfPreconditionFailures_AndNotThrow()
	{
		var interceptor = new StalePreconditionInterceptor();
		var store = CreateStore(interceptor);
		var messageId = $"m-{Guid.NewGuid():N}";
		await SeedAsync(store, messageId).ConfigureAwait(false);

		interceptor.ResetCount();
		interceptor.ForceStale = true;

		var outcome = await store.MarkFailedAsync(messageId, "h-1", "transient failure", CancellationToken.None)
			.ConfigureAwait(false);

		outcome.ShouldBe(
			InboxMarkFailedOutcome.Undecided,
			"contention that outlasts the bound is reported as Undecided so the caller can re-drive it");
		interceptor.PreconditionedCommits.ShouldBe(
			Bound,
			"the store must attempt the preconditioned commit exactly once per permitted attempt -- fewer "
			+ "means it gave up early, more means the bound is not the bound");
	}

	/// <summary>
	/// CONTROL for the fixture: the same path with the precondition left alone yields Applied after ONE
	/// commit. Without it, the arm above could be green because the interceptor broke every call.
	/// </summary>
	[Fact]
	public async Task ReportApplied_AfterOneCommit_WhenThePreconditionIsCurrent()
	{
		var interceptor = new StalePreconditionInterceptor();
		var store = CreateStore(interceptor);
		var messageId = $"m-{Guid.NewGuid():N}";
		await SeedAsync(store, messageId).ConfigureAwait(false);

		interceptor.ResetCount();

		var outcome = await store.MarkFailedAsync(messageId, "h-1", "transient failure", CancellationToken.None)
			.ConfigureAwait(false);

		outcome.ShouldBe(InboxMarkFailedOutcome.Applied);
		interceptor.PreconditionedCommits.ShouldBe(1, "an accepted commit must not be retried");
	}

	private static Task SeedAsync(FirestoreInboxStore store, string messageId) =>
		store.CreateEntryAsync(
			messageId,
			"h-1",
			"TestMessage",
			Encoding.UTF8.GetBytes("{}"),
			new Dictionary<string, object>(),
			CancellationToken.None).AsTask();

	private FirestoreInboxStore CreateStore(StalePreconditionInterceptor interceptor)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Firestore emulator must be available - real-infra arms are never skipped.");

		// The fixture's own client is built with EmulatorOnly, which is mutually exclusive with an explicit
		// channel, so an interceptor cannot be added to it. Build a second client over a channel to the SAME
		// emulator the fixture started.
		var emulator = Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST")
			?? throw new InvalidOperationException("The Firestore fixture did not publish its emulator address.");
		// Testcontainers publishes the endpoint with its scheme; FIRESTORE_EMULATOR_HOST is conventionally
		// bare host:port. Accept either.
		var address = emulator.Contains("://", StringComparison.Ordinal) ? emulator : $"http://{emulator}";
		var channel = GrpcChannel.ForAddress(address);
		var client = new FirestoreClientBuilder { CallInvoker = channel.CreateCallInvoker().Intercept(interceptor) }.Build();
		var db = FirestoreDb.Create(_fixture.ProjectId, client);

		return new FirestoreInboxStore(
			db,
			Options.Create(new FirestoreInboxOptions
			{
				ProjectId = _fixture.ProjectId,
				CollectionName = _fixture.CollectionName,
				MaxConcurrencyRetries = Bound,
			}),
			NullLogger<FirestoreInboxStore>.Instance,
			SingleTenantTestContext.Instance);
	}

	/// <summary>
	/// Passes every call through to the real emulator. Counts commits that carry an update-time precondition
	/// and, when told to, moves that precondition to a time the document was never at.
	/// </summary>
	private sealed class StalePreconditionInterceptor : Interceptor
	{
		private bool _forceStale;
		private int _preconditionedCommits;

		public bool ForceStale
		{
			get => Volatile.Read(ref _forceStale);
			set => Volatile.Write(ref _forceStale, value);
		}

		public int PreconditionedCommits => Volatile.Read(ref _preconditionedCommits);

		public void ResetCount() => Volatile.Write(ref _preconditionedCommits, 0);

		public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
			TRequest request,
			ClientInterceptorContext<TRequest, TResponse> context,
			AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
		{
			if (request is CommitRequest commit)
			{
				var preconditioned = commit.Writes.Where(static w => w.CurrentDocument?.UpdateTime is not null).ToList();
				if (preconditioned.Count > 0)
				{
					_ = Interlocked.Increment(ref _preconditionedCommits);

					if (ForceStale)
					{
						foreach (var write in preconditioned)
						{
							write.CurrentDocument.UpdateTime = new Google.Protobuf.WellKnownTypes.Timestamp { Seconds = 1 };
						}
					}
				}
			}

			return continuation(request, WithEmulatorOwner(context));
		}

		public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
			TRequest request,
			ClientInterceptorContext<TRequest, TResponse> context,
			AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation) =>
			continuation(request, WithEmulatorOwner(context));

		// The emulator grants full access to the "owner" bearer token -- what EmulatorOnly would have sent.
		private static ClientInterceptorContext<TRequest, TResponse> WithEmulatorOwner<TRequest, TResponse>(
			ClientInterceptorContext<TRequest, TResponse> context)
			where TRequest : class
			where TResponse : class
		{
			var headers = context.Options.Headers ?? [];
			if (!headers.Any(static h => string.Equals(h.Key, "authorization", StringComparison.OrdinalIgnoreCase)))
			{
				headers.Add("authorization", "Bearer owner");
			}

			return new ClientInterceptorContext<TRequest, TResponse>(
				context.Method, context.Host, context.Options.WithHeaders(headers));
		}
	}
}
