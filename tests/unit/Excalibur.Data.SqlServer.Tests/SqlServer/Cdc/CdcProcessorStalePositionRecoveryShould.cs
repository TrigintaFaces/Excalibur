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
/// Characterization tests pinning the per-<see cref="StalePositionRecoveryStrategy"/> behavior of
/// <c>CdcProcessor.RecoverFromStalePositionAsync</c>. The recovery method is exercised through the
/// <see cref="ICdcRepository"/> seam: a fake repository is injected into the composed
/// <see cref="CdcProcessor"/> so each strategy's min/max plumbing and callback notification are
/// deterministic without live SQL Server (per the injected-seam ruling — determinism over real infra).
/// </summary>
/// <remarks>
/// <para>
/// The <c>Throw</c> strategy is intentionally not covered here: the producer loop rethrows on
/// <c>Throw</c> <em>before</em> calling recovery, so <c>RecoverFromStalePositionAsync</c> is only ever
/// reached for <see cref="StalePositionRecoveryStrategy.FallbackToEarliest"/>,
/// <see cref="StalePositionRecoveryStrategy.FallbackToLatest"/>, and
/// <see cref="StalePositionRecoveryStrategy.InvokeCallback"/>.
/// </para>
/// <para>
/// Each strategy is asserted with BOTH a liveness arm (the permitted position query happens and a
/// resume position is produced) AND a safety arm (the other strategy's position query does NOT happen),
/// so a recovery method that silently did nothing — or queried the wrong bound — fails the test.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcProcessorStalePositionRecoveryShould : UnitTestBase
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
	public async Task FallbackToEarliest_QueriesMinPerCaptureInstance_AndNeverQueriesMax()
	{
		var (processor, repo) = CreateProcessorWithFakeRepo("dbo_orders", "dbo_customers");
		using (processor)
		{
			var options = new CdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest };

			var newPosition = await InvokeRecoverAsync(processor, sqlException: null, options).ConfigureAwait(false);

			// Liveness: the earliest position was queried for EVERY capture instance and a resume position produced.
			A.CallTo(() => repo.GetMinPositionAsync("dbo_orders", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			A.CallTo(() => repo.GetMinPositionAsync("dbo_customers", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			_ = newPosition.ShouldNotBeNull();

			// Safety: FallbackToEarliest must never consult the LATEST position (that is the data-loss strategy).
			A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._)).MustNotHaveHappened();
		}
	}

	[Fact]
	public async Task FallbackToLatest_ResetsToMaxPosition_AndNeverQueriesMin()
	{
		var (processor, repo) = CreateProcessorWithFakeRepo("dbo_orders");
		using (processor)
		{
			var options = new CdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest };

			var newPosition = await InvokeRecoverAsync(processor, sqlException: null, options).ConfigureAwait(false);

			// Liveness: the latest position was queried once and returned verbatim as the resume position.
			A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			newPosition.ShouldBe(MaxLsn);

			// Safety: FallbackToLatest must never consult the EARLIEST position.
			A.CallTo(() => repo.GetMinPositionAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
	}

	[Fact]
	public async Task InvokeCallback_WithoutHandler_ThrowsInvalidOperationException()
	{
		var (processor, repo) = CreateProcessorWithFakeRepo("dbo_orders");
		using (processor)
		{
			var options = new CdcRecoveryOptions
			{
				RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
				OnPositionReset = null,
			};

			// Safety: a callback strategy with no callback must FAIL LOUD, not silently proceed with no recovery.
			_ = await Should.ThrowAsync<InvalidOperationException>(
				() => InvokeRecoverAsync(processor, sqlException: null, options)).ConfigureAwait(false);

			// And it must fail BEFORE touching the repository (no position was reset).
			A.CallTo(() => repo.GetMinPositionAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
			A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._)).MustNotHaveHappened();
		}
	}

	[Fact]
	public async Task InvokeCallback_WithHandler_NotifiesCallback_AndResetsViaMinPath()
	{
		var (processor, repo) = CreateProcessorWithFakeRepo("dbo_orders");
		using (processor)
		{
			var callbackCount = 0;
			CdcPositionResetEventArgs? observed = null;
			var options = new CdcRecoveryOptions
			{
				RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
				OnPositionReset = (args, _) =>
				{
					callbackCount++;
					observed = args;
					return Task.CompletedTask;
				},
			};

			var ex = CreateSqlException(errorNumber: 22037, message: "Stale CDC position");
			var newPosition = await InvokeRecoverAsync(processor, ex, options).ConfigureAwait(false);

			// Liveness: the callback fired exactly once and recovery took the earliest (min) path.
			callbackCount.ShouldBe(1);
			A.CallTo(() => repo.GetMinPositionAsync("dbo_orders", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			_ = observed.ShouldNotBeNull();
			observed.NewPosition.ShouldBe(newPosition);
		}
	}

	[Fact]
	public async Task FallbackToLatest_WithHandler_AlsoNotifiesCallback()
	{
		var (processor, _) = CreateProcessorWithFakeRepo("dbo_orders");
		using (processor)
		{
			var callbackCount = 0;
			var options = new CdcRecoveryOptions
			{
				RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest,
				OnPositionReset = (_, _) =>
				{
					callbackCount++;
					return Task.CompletedTask;
				},
			};

			var ex = CreateSqlException(errorNumber: 22037, message: "Stale CDC position");
			_ = await InvokeRecoverAsync(processor, ex, options).ConfigureAwait(false);

			// The position-reset callback fires for observability on EVERY strategy, not just InvokeCallback.
			callbackCount.ShouldBe(1);
		}
	}

	private static async Task<byte[]?> InvokeRecoverAsync(CdcProcessor processor, SqlException? sqlException, CdcRecoveryOptions options)
	{
		// RecoverFromStalePositionAsync is an async method: an exception thrown in its synchronous prologue
		// (e.g. the missing-callback guard) is captured onto the returned Task, so awaiting surfaces it.
		var task = (Task<byte[]?>)RecoverMethod.Invoke(processor, [sqlException, options, CancellationToken.None])!;
		return await task.ConfigureAwait(false);
	}

	private static (CdcProcessor Processor, ICdcRepository Repo) CreateProcessorWithFakeRepo(params string[] captureInstances)
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
		A.CallTo(() => dbConfig.CaptureInstances).Returns(captureInstances.Length == 0 ? ["dbo_orders"] : captureInstances);

		var noOpPolicy = Policy.NoOpAsync();
		A.CallTo(() => policyFactory.GetComprehensivePolicy()).Returns(noOpPolicy);
		A.CallTo(() => policyFactory.GetRetryPolicy()).Returns(noOpPolicy);
		A.CallTo(() => policyFactory.CreateCircuitBreakerPolicy()).Returns(noOpPolicy);

		var processor = new CdcProcessor(
			appLifetime,
			dbConfig,
			new CdcRepository(new SqlConnection("Server=localhost;Database=master;Encrypt=false;TrustServerCertificate=true")),
			() => new SqlConnection("Server=localhost;Database=master;Encrypt=false;TrustServerCertificate=true"),
			stateStoreOptions: null,
			policyFactory,
			TimeProvider.System,
			logger);

		// Inject the deterministic repository seam. RecoverFromStalePositionAsync reads GetMin/GetMax through
		// the _cdcRepository field; the in-memory checkpoint manager (built from the real repo in the ctor) is
		// never touched for its DB path during recovery, so no live SQL is required.
		var repo = A.Fake<ICdcRepository>();
		A.CallTo(() => repo.GetMinPositionAsync(A<string>._, A<CancellationToken>._)).Returns(MinLsn);
		A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._)).Returns(MaxLsn);
		CdcRepositoryField.SetValue(processor, repo);

		return (processor, repo);
	}

	// ── SqlException factory (lifted from CdcStalePositionDetectorShould) ─────────────────────────────
	// SqlException has no public constructor; the recovery method's signature requires one for the callback
	// paths (CreateEventArgs rejects a null exception). Reflection over the internal factory is the project's
	// established way to synthesize a SqlException in a unit test.
	private static SqlException CreateSqlException(int errorNumber, string message)
	{
		var createExceptionMethod = typeof(SqlException).GetMethod(
			"CreateException",
			BindingFlags.Static | BindingFlags.NonPublic,
			null,
			[typeof(SqlErrorCollection), typeof(string)],
			null);

		var sqlError = CreateSqlError(errorNumber, message);

		var errorCollection = (SqlErrorCollection)Activator.CreateInstance(
			typeof(SqlErrorCollection),
			BindingFlags.Instance | BindingFlags.NonPublic,
			null,
			null,
			null)!;

		var addMethod = typeof(SqlErrorCollection).GetMethod(
			"Add",
			BindingFlags.Instance | BindingFlags.NonPublic);
		_ = addMethod!.Invoke(errorCollection, [sqlError]);

		if (createExceptionMethod != null)
		{
			return (SqlException)createExceptionMethod.Invoke(null, [errorCollection, "1.0.0"])!;
		}

		throw new InvalidOperationException("Could not synthesize SqlException via reflection.");
	}

	private static SqlError CreateSqlError(int errorNumber, string message)
	{
		var ctors = typeof(SqlError).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);

		foreach (var ctor in ctors)
		{
			var parameters = ctor.GetParameters();
			try
			{
				var args = new object?[parameters.Length];
				for (int i = 0; i < parameters.Length; i++)
				{
					var param = parameters[i];
					if (param.ParameterType == typeof(int))
					{
						args[i] = Array.FindIndex(parameters, 0, i, p => p.ParameterType == typeof(int)) >= 0
							? 1          // subsequent int is the line number
							: errorNumber;
					}
					else if (param.ParameterType == typeof(byte))
					{
						args[i] = (byte)0;
					}
					else if (param.ParameterType == typeof(string))
					{
						args[i] = param.Name switch
						{
							"server" => "server",
							"message" or "errorMessage" => message,
							"procedure" or "procName" or "source" => "procedure",
							_ => message,
						};
					}
					else if (param.ParameterType == typeof(uint))
					{
						args[i] = (uint)0;
					}
					else if (param.ParameterType == typeof(Exception))
					{
						args[i] = null;
					}
					else if (param.HasDefaultValue)
					{
						args[i] = param.DefaultValue;
					}
					else if (Nullable.GetUnderlyingType(param.ParameterType) != null)
					{
						args[i] = null;
					}
					else
					{
						args[i] = Activator.CreateInstance(param.ParameterType);
					}
				}

				if (ctor.Invoke(args) is SqlError error)
				{
					return error;
				}
			}
			catch (TargetInvocationException)
			{
				// Try the next constructor overload.
			}
		}

		throw new InvalidOperationException("Could not synthesize SqlError via reflection.");
	}
}
