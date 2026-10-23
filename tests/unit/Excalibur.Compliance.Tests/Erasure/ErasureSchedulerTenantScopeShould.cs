// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Infrastructure;

namespace Excalibur.Compliance.Tests.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class ErasureSchedulerTenantScopeShould
{
	private static readonly string[] ExpectedTenantOwners = ["tenant-t", "tenant-u"];
	private static readonly string[] ExpectedTenantT = ["tenant-t"];
	private static readonly string[] ExpectedTenantU = ["tenant-u"];

	[Fact]
	public async Task CompleteNamedTenantRequestThroughSchedulerWithoutDestroyingOtherTenantsKey()
	{
		using var keys = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
		var harness = new ErasureLifecycleHarness(keys, keys,
			tenantContext: TenantContextHolder.AmbientContext, requireTenant: true,
			erasureOptions: new ErasureOptions
			{
				KeyShredOnlyErasure = true, MinimumGracePeriod = TimeSpan.Zero, DefaultGracePeriod = TimeSpan.Zero,
				Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
			});
		var keyT = SubjectKeyHandle.ForSubject(new TenantId("tenant-t"), "shared-subject", TestDataSubjectHasher.Instance).Value;
		var keyU = SubjectKeyHandle.ForSubject(new TenantId("tenant-u"), "shared-subject", TestDataSubjectHasher.Instance).Value;
		_ = await keys.RotateKeyAsync(keyT, EncryptionAlgorithm.Aes256Gcm, "crypto-shred", null, CancellationToken.None);
		_ = await keys.RotateKeyAsync(keyU, EncryptionAlgorithm.Aes256Gcm, "crypto-shred", null, CancellationToken.None);
		(await keys.GetKeyAsync(keyT, CancellationToken.None)).ShouldNotBeNull();
		var originalU = await keys.GetKeyAsync(keyU, CancellationToken.None);
		originalU.ShouldNotBeNull();
		var request = Request("shared-subject");
		using (TenantContextHolder.BeginScope("tenant-t"))
		{
			_ = await harness.Service.RequestErasureAsync(request, CancellationToken.None);
		}
		using var caller = TenantContextHolder.BeginScope("tenant-u");
		using var container = SchedulerContainer(harness.Store, harness.Service);
		using var scheduler = Scheduler(container);
		await scheduler.StartAsync(CancellationToken.None);
		try
		{
			(await WaitHelpers.WaitUntilAsync(async () =>
			{
				using var owner = TenantContextHolder.BeginScope("tenant-t");
				return (await harness.StatusOfAsync(request.RequestId)) == ErasureRequestStatus.Completed;
			}, TestTimeouts.Scale(TimeSpan.FromSeconds(5)))).ShouldBeTrue("the scheduler must reach the row its estate-wide scan discovered");
		}
		finally
		{
			await scheduler.StopAsync(CancellationToken.None).WaitAsync(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		}
		TenantContextHolder.Current.ShouldBe("tenant-u");
		(await keys.GetKeyAsync(keyT, CancellationToken.None)).ShouldBeNull();
		(await keys.GetKeyAsync(keyU, CancellationToken.None)).ShouldNotBeNull();
		(await keys.GetKeyAsync(keyU, CancellationToken.None))!.Generation.ShouldBe(originalU!.Generation);
		using var verification = TenantContextHolder.BeginScope("tenant-t");
		(await harness.Store.GetCertificateAsync(request.RequestId, CancellationToken.None)).ShouldNotBeNull();
	}

	[Fact]
	public async Task DiscoverAndCompleteNamedOwnersOnlyAfterProviderConfirmsDestruction()
	{
		var admin = KeyDestructionFakes.AdminThatReportsAGenerationForEveryKey();
		var provider = KeyDestructionFakes.ProviderThatReportsDestruction();
		A.CallTo(() => admin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.ScheduledAt(DateTimeOffset.UtcNow.AddDays(7))));
		var harness = new ErasureLifecycleHarness(admin, provider,
			tenantContext: TenantContextHolder.AmbientContext, requireTenant: true);
		var requests = new List<(string Tenant, Guid Id)>();
		foreach (var tenant in new[] { "tenant-t", "tenant-u" })
		{
			using var owner = TenantContextHolder.BeginScope(tenant);
			var (id, _, _) = await harness.SubmitAndExecuteAsync("shared-subject");
			(await harness.StatusOfAsync(id)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
			(await harness.Store.GetCertificateAsync(id, CancellationToken.None)).ShouldBeNull();
			requests.Add((tenant, id));
		}
		using var caller = TenantContextHolder.BeginScope("unrelated-caller");
		(await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None)).ShouldBe(0);
		provider.ReportsEveryKeyDestroyed();
		(await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None)).ShouldBe(2);
		TenantContextHolder.Current.ShouldBe("unrelated-caller");
		foreach (var (tenant, id) in requests)
		{
			using var owner = TenantContextHolder.BeginScope(tenant);
			(await harness.StatusOfAsync(id)).ShouldBe(ErasureRequestStatus.Completed);
			(await harness.Store.GetCertificateAsync(id, CancellationToken.None)).ShouldNotBeNull();
		}
	}

	[Fact]
	public async Task BackgroundDiscoveryPagesOwnersAndPreservesConsumerTenantFloor()
	{
		var store = new InMemoryErasureStore(TestDataSubjectHasher.Instance, TenantContextHolder.AmbientContext,
			Options.Create(new TenantContextOptions { RequireTenant = true }));
		var expected = new List<Guid>();
		var when = DateTimeOffset.UtcNow.AddDays(-1);
		foreach (var tenant in new[] { "tenant-t", "tenant-u", "tenant-t" })
		{
			using var owner = TenantContextHolder.BeginScope(tenant);
			var request = Request(tenant) with { RequestedAt = when };
			await store.SaveRequestAsync(request, when, CancellationToken.None);
			_ = await store.UpdateStatusAsync(request.RequestId, ErasureRequestStatus.AwaitingKeyDestruction, null, CancellationToken.None);
			expected.Add(request.RequestId);
		}
		using var caller = TenantContextHolder.BeginScope("tenant-t");
		await store.SaveRequestAsync(Request("scheduled"), when, CancellationToken.None);
		var consumer = await store.ListRequestsAsync(ErasureRequestStatus.AwaitingKeyDestruction, null, null, null, 1, 100, CancellationToken.None);
		consumer.Count.ShouldBe(2);
		consumer.ShouldAllBe(row => row.TenantId == "tenant-t");
		(await store.ListRequestsAsync(null, "tenant-u", null, null, 1, 100, CancellationToken.None)).ShouldBeEmpty();
		var background = store.GetService(typeof(IErasureBackgroundQueryStore)).ShouldBeAssignableTo<IErasureBackgroundQueryStore>();
		var actual = new List<ErasureStatus>();
		for (var page = 1; page <= 3; page++)
		{
			actual.AddRange(await background.GetAwaitingKeyDestructionRequestsAsync(page, 1, CancellationToken.None));
		}
		actual.Select(row => row.RequestId).Order().ShouldBe(expected.Order());
		actual.Select(row => row.TenantId).ShouldContain("tenant-u");
		(await background.GetAwaitingKeyDestructionRequestsAsync(int.MaxValue, 1000, CancellationToken.None)).ShouldBeEmpty();
		TenantContextHolder.Current.ShouldBe("tenant-t");
	}

	[Fact]
	public async Task CompleteEveryOwnerAcrossPageBoundaryBeforeMutatingDiscoverySet()
	{
		var admin = KeyDestructionFakes.AdminThatReportsAGenerationForEveryKey();
		var provider = KeyDestructionFakes.ProviderThatReportsDestruction();
		A.CallTo(() => admin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.ScheduledAt(DateTimeOffset.UtcNow.AddDays(7))));
		var harness = new ErasureLifecycleHarness(admin, provider,
			tenantContext: TenantContextHolder.AmbientContext, requireTenant: true);
		var requests = new List<(string Tenant, Guid Id)>();
		for (var index = 0; index < 501; index++)
		{
			var tenant = index % 2 == 0 ? "tenant-t" : "tenant-u";
			using var owner = TenantContextHolder.BeginScope(tenant);
			var (id, _, _) = await harness.SubmitAndExecuteAsync($"subject-{index}");
			(await harness.StatusOfAsync(id)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
			requests.Add((tenant, id));
		}
		provider.ReportsEveryKeyDestroyed();
		using var caller = TenantContextHolder.BeginScope("unrelated-caller");
		(await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None)).ShouldBe(501);
		TenantContextHolder.Current.ShouldBe("unrelated-caller");
		foreach (var (tenant, id) in requests)
		{
			using var owner = TenantContextHolder.BeginScope(tenant);
			(await harness.StatusOfAsync(id)).ShouldBe(ErasureRequestStatus.Completed);
			(await harness.Store.GetCertificateAsync(id, CancellationToken.None)).ShouldNotBeNull();
		}
		(await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None)).ShouldBe(0);
	}

	[Fact]
	public async Task RefuseMissingBackgroundFacetWithoutFallingBackToConsumerListing()
	{
		var harness = new ErasureLifecycleHarness(
			KeyDestructionFakes.AdminThatReportsAGenerationForEveryKey(),
			KeyDestructionFakes.ProviderThatReportsDestruction());
		var store = A.Fake<IErasureStore>();
		A.CallTo(() => store.GetService(typeof(IErasureBackgroundQueryStore))).Returns(null);
		var processor = new ErasureCompletionProcessor(harness.Service, store, harness.Verifier,
			NullLogger<ErasureCompletionProcessor>.Instance);
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => processor.CompletePendingErasuresAsync(CancellationToken.None));
		exception.Message.ShouldContain(nameof(IErasureBackgroundQueryStore));
		A.CallTo(() => store.GetService(typeof(IErasureQueryStore))).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PersistFailureUnderRecordedOwnerAndContinueOtherTenants(bool throws)
	{
		var harness = new ErasureLifecycleHarness(
			KeyDestructionFakes.AdminThatReportsAGenerationForEveryKey(),
			KeyDestructionFakes.ProviderThatReportsDestruction(),
			tenantContext: TenantContextHolder.AmbientContext, requireTenant: true);
		var failed = Request("failed-subject");
		var healthy = Request("healthy-subject");
		using (TenantContextHolder.BeginScope("tenant-t"))
		{
			await harness.Store.SaveRequestAsync(failed, DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None);
		}
		using (TenantContextHolder.BeginScope("tenant-u"))
		{
			await harness.Store.SaveRequestAsync(healthy, DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None);
		}
		var executor = A.Fake<IErasureExecutor>();
		var observed = new List<string?>();
		A.CallTo(() => executor.ExecuteAsync(A<Guid>._, A<CancellationToken>._))
			.ReturnsLazily((Guid id, CancellationToken _) =>
			{
				observed.Add(TenantContextHolder.Current);
				if (id == failed.RequestId && throws)
				{
					throw new InvalidOperationException("controlled execution failure");
				}
				return Task.FromResult(ErasureExecutionResult.Failed("controlled execution failure"));
			});
		using var caller = TenantContextHolder.BeginScope("unrelated-caller");
		using var container = SchedulerContainer(harness.Store, executor);
		using var scheduler = new ErasureSchedulerBackgroundService(
			container.GetRequiredService<IServiceScopeFactory>(),
			Options.Create(new ErasureSchedulerOptions
			{
				PollingInterval = TimeSpan.FromHours(1), MaxRetryAttempts = 1,
			}), NullLogger<ErasureSchedulerBackgroundService>.Instance);
		await scheduler.StartAsync(CancellationToken.None);
		try
		{
			(await WaitHelpers.WaitUntilAsync(async () =>
			{
				using var owner = TenantContextHolder.BeginScope("tenant-u");
				return await harness.StatusOfAsync(healthy.RequestId) == ErasureRequestStatus.Failed;
			}, TestTimeouts.Scale(TimeSpan.FromSeconds(5)))).ShouldBeTrue();
		}
		finally
		{
			await scheduler.StopAsync(CancellationToken.None).WaitAsync(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		}
		TenantContextHolder.Current.ShouldBe("unrelated-caller");
		observed.Order().ShouldBe(ExpectedTenantOwners);
		using var verification = TenantContextHolder.BeginScope("tenant-t");
		(await harness.StatusOfAsync(failed.RequestId)).ShouldBe(ErasureRequestStatus.Failed);
		(await harness.Store.GetStatusAsync(failed.RequestId, CancellationToken.None))!.ErrorMessage
			.ShouldContain("controlled execution failure");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SkipMalformedRecordedOwnerWithoutExecutingOrWritingAndContinueBatch(bool overlength)
	{
		var malformed = ScheduledStatus(overlength ? new string('x', TenantId.MaxLength + 1) : "   ");
		var valid = ScheduledStatus("tenant-u");
		var store = A.Fake<IErasureStore>();
		var query = A.Fake<IErasureQueryStore>();
		A.CallTo(() => store.GetService(typeof(IErasureQueryStore))).Returns(query);
		A.CallTo(() => query.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<ErasureStatus>>([malformed, valid]));
		var executor = A.Fake<IErasureExecutor>();
		var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => executor.ExecuteAsync(valid.RequestId, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				TenantContextHolder.Current.ShouldBe("tenant-u");
				reached.TrySetResult();
				return Task.FromResult(ErasureExecutionResult.Succeeded(0, 0));
			});
		using var caller = TenantContextHolder.BeginScope("unrelated-caller");
		using var container = SchedulerContainer(store, executor);
		using var scheduler = Scheduler(container);
		await scheduler.StartAsync(CancellationToken.None);
		try
		{
			await reached.Task.WaitAsync(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		}
		finally
		{
			await scheduler.StopAsync(CancellationToken.None).WaitAsync(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		}
		A.CallTo(() => executor.ExecuteAsync(malformed.RequestId, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => store.GetStatusAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => store.UpdateStatusAsync(A<Guid>._, A<ErasureRequestStatus>._, A<string?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		TenantContextHolder.Current.ShouldBe("unrelated-caller");
	}

	[Fact]
	public async Task StopCancellationWithoutRetryingOrWritingUnderAnyOwner()
	{
		var first = ScheduledStatus("tenant-t");
		var next = ScheduledStatus("tenant-u");
		var store = A.Fake<IErasureStore>();
		var query = A.Fake<IErasureQueryStore>();
		A.CallTo(() => store.GetService(typeof(IErasureQueryStore))).Returns(query);
		A.CallTo(() => query.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<ErasureStatus>>([first, next]));
		var executor = A.Fake<IErasureExecutor>();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => executor.ExecuteAsync(first.RequestId, A<CancellationToken>._))
			.ReturnsLazily(async (Guid _, CancellationToken token) =>
			{
				TenantContextHolder.Current.ShouldBe("tenant-t");
				entered.TrySetResult();
				await Task.Delay(Timeout.InfiniteTimeSpan, token);
				return ErasureExecutionResult.Succeeded(0, 0);
			});
		using var caller = TenantContextHolder.BeginScope("unrelated-caller");
		using var container = SchedulerContainer(store, executor);
		using var scheduler = Scheduler(container);
		await scheduler.StartAsync(CancellationToken.None);
		try
		{
			await entered.Task.WaitAsync(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		}
		finally
		{
			await scheduler.StopAsync(CancellationToken.None).WaitAsync(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		}
		A.CallTo(() => executor.ExecuteAsync(next.RequestId, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => store.GetStatusAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => store.UpdateStatusAsync(A<Guid>._, A<ErasureRequestStatus>._, A<string?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		TenantContextHolder.Current.ShouldBe("unrelated-caller");
	}

	[Fact]
	public async Task SkipMalformedCompletionOwnerAndConfirmNextRecordedOwner()
	{
		var (harness, waiting) = await PendingNamedRequestsAsync(TenantScope.UntenantedSentinel);
		// A real legacy row makes a missing ownership refusal observable: blank scope would resolve to
		// the sentinel partition and reach verification, rather than disappear as a nonexistent id.
		var malformed = waiting[0] with { TenantId = "   " };
		var verifier = A.Fake<IErasureVerificationService>();
		var observed = new List<string?>();
		A.CallTo(() => verifier.VerifyKeyDeletionAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				observed.Add(TenantContextHolder.Current);
				return Task.FromResult(false);
			});
		var processor = CompletionWithDiscovery(harness, verifier, [malformed, waiting[1]]);
		using var caller = TenantContextHolder.BeginScope("unrelated-caller");
		(await processor.CompletePendingErasuresAsync(CancellationToken.None)).ShouldBe(0);
		observed.ShouldBe(ExpectedTenantU);
		TenantContextHolder.Current.ShouldBe("unrelated-caller");
		foreach (var request in waiting)
		{
			using var owner = TenantContextHolder.BeginScope(request.TenantId);
			(await harness.StatusOfAsync(request.RequestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
			(await harness.Store.GetCertificateAsync(request.RequestId, CancellationToken.None)).ShouldBeNull();
		}
	}

	[Fact]
	public async Task PropagateCancellationDuringConfirmationAndRestoreCallerWithoutConfirmingNextOwner()
	{
		var (harness, waiting) = await PendingNamedRequestsAsync();
		using var cancellation = new CancellationTokenSource();
		var verifier = A.Fake<IErasureVerificationService>();
		var observed = new List<string?>();
		A.CallTo(() => verifier.VerifyKeyDeletionAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string _, CancellationToken token) =>
			{
				observed.Add(TenantContextHolder.Current);
				cancellation.Cancel();
				return Task.FromCanceled<bool>(token);
			});
		var processor = CompletionWithDiscovery(harness, verifier, waiting);
		using var caller = TenantContextHolder.BeginScope("unrelated-caller");
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => processor.CompletePendingErasuresAsync(cancellation.Token));
		observed.ShouldBe(ExpectedTenantT);
		TenantContextHolder.Current.ShouldBe("unrelated-caller");
		foreach (var request in waiting)
		{
			using var owner = TenantContextHolder.BeginScope(request.TenantId);
			(await harness.StatusOfAsync(request.RequestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
			(await harness.Store.GetCertificateAsync(request.RequestId, CancellationToken.None)).ShouldBeNull();
		}
	}

	private static ErasureCompletionProcessor CompletionWithDiscovery(
		ErasureLifecycleHarness harness, IErasureVerificationService verifier, IReadOnlyList<ErasureStatus> waiting)
	{
		var store = A.Fake<IErasureStore>();
		var background = A.Fake<IErasureBackgroundQueryStore>();
		A.CallTo(() => store.GetService(typeof(IErasureBackgroundQueryStore))).Returns(background);
		A.CallTo(() => background.GetAwaitingKeyDestructionRequestsAsync(1, 500, A<CancellationToken>._))
			.Returns(Task.FromResult(waiting));
		return new ErasureCompletionProcessor(harness.Service, store, verifier,
			NullLogger<ErasureCompletionProcessor>.Instance);
	}

	private static async Task<(ErasureLifecycleHarness Harness, IReadOnlyList<ErasureStatus> Waiting)> PendingNamedRequestsAsync(
		string firstTenant = "tenant-t")
	{
		var admin = KeyDestructionFakes.AdminThatReportsAGenerationForEveryKey();
		var provider = KeyDestructionFakes.ProviderThatReportsDestruction();
		A.CallTo(() => admin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.ScheduledAt(DateTimeOffset.UtcNow.AddDays(7))));
		var harness = new ErasureLifecycleHarness(admin, provider,
			tenantContext: TenantContextHolder.AmbientContext, requireTenant: true);
		var waiting = new List<ErasureStatus>();
		foreach (var tenant in new[] { firstTenant, "tenant-u" })
		{
			using var owner = TenantContextHolder.BeginScope(tenant);
			var (id, _, _) = await harness.SubmitAndExecuteAsync("shared-subject");
			var status = await harness.Store.GetStatusAsync(id, CancellationToken.None);
			status.ShouldNotBeNull();
			status.Status.ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
			waiting.Add(status);
		}
		return (harness, waiting);
	}

	private static ErasureStatus ScheduledStatus(string tenant) => new()
	{
		TenantPartition = KeyedTenantPartition.Untenanted,
		RequestId = Guid.NewGuid(), DataSubjectIdHash = "test-subject", IdType = DataSubjectIdType.UserId,
		Scope = ErasureScope.User, LegalBasis = ErasureLegalBasis.DataSubjectRequest,
		Status = ErasureRequestStatus.Scheduled, RequestedBy = "test", TenantId = tenant,
		RequestedAt = DateTimeOffset.UtcNow.AddDays(-1), UpdatedAt = DateTimeOffset.UtcNow,
		ScheduledExecutionAt = DateTimeOffset.UtcNow.AddMinutes(-1),
	};

	private static ErasureRequest Request(string subject) => new()
	{
		DataSubjectId = subject, IdType = DataSubjectIdType.UserId,
		LegalBasis = ErasureLegalBasis.DataSubjectRequest, RequestedBy = "test",
	};

	private static ServiceProvider SchedulerContainer(IErasureStore store, IErasureExecutor executor)
	{
		var services = new ServiceCollection();
		services.AddSingleton(store);
		services.AddSingleton(executor);
		return services.BuildServiceProvider();
	}

	private static ErasureSchedulerBackgroundService Scheduler(ServiceProvider container) => new(
		container.GetRequiredService<IServiceScopeFactory>(),
		Options.Create(new ErasureSchedulerOptions
		{
			Enabled = true, PollingInterval = TimeSpan.FromHours(1), BatchSize = 10,
		}), NullLogger<ErasureSchedulerBackgroundService>.Instance);
}
