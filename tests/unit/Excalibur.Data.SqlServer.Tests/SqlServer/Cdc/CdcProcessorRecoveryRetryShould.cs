// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

using Polly;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Binds the stale-position recovery path to the shared data-access policy.
/// </summary>
/// <remarks>
/// <para>
/// <c>CdcProcessor.RecoverFromStalePositionAsync</c> reads the CDC position bounds directly rather than
/// through the change detector or applier. Both of those siblings run their repository calls through the
/// policy obtained from <see cref="IDataAccessPolicyFactory"/>; recovery must too. Recovery is invoked
/// from inside the producer loop's stale-position <c>catch</c> block, so an exception raised there is not
/// caught by any sibling handler — a single transient fault during recovery stops the producer for the
/// remainder of the run rather than being retried.
/// </para>
/// <para>
/// Each arm supplies a policy that retries once and a repository whose position read fails transiently on
/// its first call. The liveness assertion is that recovery still produces a resume position; the safety
/// assertion is that the retry happened <em>through the factory's policy</em> (the factory was consulted
/// and the read was re-issued), not by some incidental swallow. Both arms fail if the position reads are
/// executed unwrapped.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcProcessorRecoveryRetryShould : UnitTestBase
{
	private static readonly MethodInfo RecoverMethod = typeof(CdcProcessor)
		.GetMethod("RecoverFromStalePositionAsync", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private RecoverFromStalePositionAsync method on CdcProcessor.");

	private static readonly FieldInfo CdcRepositoryField = typeof(CdcProcessor)
		.GetField("_cdcRepository", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected _cdcRepository field on CdcProcessor.");

	private static readonly byte[] MinLsn = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05];
	private static readonly byte[] MaxLsn = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0A];

	[Fact]
	public async Task RetryTheEarliestPositionRead_WhenItFailsTransiently()
	{
		var (processor, repo, policyFactory) = CreateProcessor();
		using (processor)
		{
			var attempts = 0;
			A.CallTo(() => repo.GetMinPositionAsync("dbo_orders", A<CancellationToken>._))
				.ReturnsLazily(() => ++attempts == 1
					? throw new TimeoutException("transient")
					: Task.FromResult(MinLsn));

			var options = new CdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest };

			// Liveness: the transient fault is absorbed and recovery still yields a resume position.
			var newPosition = await InvokeRecoverAsync(processor, options).ConfigureAwait(false);
			_ = newPosition.ShouldNotBeNull();

			// Safety: the read was re-issued, and it was the factory's policy that re-issued it.
			attempts.ShouldBe(2);
			A.CallTo(() => policyFactory.GetComprehensivePolicy()).MustHaveHappened();
		}
	}

	[Fact]
	public async Task RetryTheLatestPositionRead_WhenItFailsTransiently()
	{
		var (processor, repo, policyFactory) = CreateProcessor();
		using (processor)
		{
			var attempts = 0;
			A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._))
				.ReturnsLazily(() => ++attempts == 1
					? throw new TimeoutException("transient")
					: Task.FromResult(MaxLsn));

			var options = new CdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest };

			var newPosition = await InvokeRecoverAsync(processor, options).ConfigureAwait(false);
			newPosition.ShouldBe(MaxLsn);

			attempts.ShouldBe(2);
			A.CallTo(() => policyFactory.GetComprehensivePolicy()).MustHaveHappened();
		}
	}

	private static async Task<byte[]?> InvokeRecoverAsync(CdcProcessor processor, CdcRecoveryOptions options)
	{
		var task = (Task<byte[]?>)RecoverMethod.Invoke(processor, [null, options, CancellationToken.None])!;
		return await task.ConfigureAwait(false);
	}

	private static (CdcProcessor Processor, ICdcRepository Repo, IDataAccessPolicyFactory PolicyFactory) CreateProcessor()
	{
		var appLifetime = A.Fake<IHostApplicationLifetime>();
		var dbConfig = A.Fake<IDatabaseOptions>();
		var policyFactory = A.Fake<IDataAccessPolicyFactory>();
		var logger = A.Fake<ILogger<CdcProcessor>>();

		A.CallTo(() => dbConfig.QueueSize).Returns(32);
		A.CallTo(() => dbConfig.ProducerBatchSize).Returns(16);
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(8);
		A.CallTo(() => dbConfig.DatabaseConnectionIdentifier).Returns("test-connection");
		A.CallTo(() => dbConfig.DatabaseName).Returns("test-db");
		A.CallTo(() => dbConfig.CaptureInstances).Returns(["dbo_orders"]);

		// A policy that retries exactly once, so a single transient fault is survivable and a second is not.
		var retryOnce = Policy.Handle<TimeoutException>().RetryAsync(1);
		A.CallTo(() => policyFactory.GetComprehensivePolicy()).Returns(retryOnce);
		A.CallTo(() => policyFactory.GetRetryPolicy()).Returns(retryOnce);
		A.CallTo(() => policyFactory.CreateCircuitBreakerPolicy()).Returns(Policy.NoOpAsync());

		var processor = new CdcProcessor(
			appLifetime,
			dbConfig,
			new CdcRepository(new SqlConnection("Server=localhost;Database=master;Encrypt=false;TrustServerCertificate=true")),
			() => new SqlConnection("Server=localhost;Database=master;Encrypt=false;TrustServerCertificate=true"),
			stateStoreOptions: null,
			policyFactory,
			TimeProvider.System,
			logger);

		var repo = A.Fake<ICdcRepository>();
		A.CallTo(() => repo.GetMinPositionAsync(A<string>._, A<CancellationToken>._)).Returns(MinLsn);
		A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._)).Returns(MaxLsn);
		CdcRepositoryField.SetValue(processor, repo);

		return (processor, repo, policyFactory);
	}
}
