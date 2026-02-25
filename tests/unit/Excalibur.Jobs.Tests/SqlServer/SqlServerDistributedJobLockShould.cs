// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Jobs.Coordination;
using Excalibur.Jobs.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Jobs.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SqlServerDistributedJobLockShould
{
	private const string UnreachableConnectionString =
		"Server=tcp:127.0.0.1,1;Database=Jobs;User ID=sa;Password=BadPassword123!;Encrypt=False;TrustServerCertificate=True;Connect Timeout=1";

	[Fact]
	public void ExposeConstructorValuesAndValidityState()
	{
		var acquiredAt = DateTimeOffset.UtcNow;
		var expiresAt = acquiredAt.AddMinutes(1);
		var sut = CreateSut(acquiredAt, expiresAt);

		sut.JobKey.ShouldBe("job-1");
		sut.InstanceId.ShouldBe("instance-1");
		sut.AcquiredAt.ShouldBe(acquiredAt);
		sut.ExpiresAt.ShouldBe(expiresAt);
		sut.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void ReportInvalid_WhenLockHasExpired()
	{
		var now = DateTimeOffset.UtcNow;
		var sut = CreateSut(now.AddMinutes(-2), now.AddSeconds(-1));

		sut.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnFalseAndNoOp_WhenDisposed()
	{
		var sut = CreateSut(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
		SetDisposed(sut, value: true);

		var extended = await sut.ExtendAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
		extended.ShouldBeFalse();

		await Should.NotThrowAsync(() => sut.ReleaseAsync(CancellationToken.None));
		sut.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowWhenBackendIsUnavailable_ForActiveLockOperations()
	{
		var sut = CreateSut(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

		await Should.ThrowAsync<Exception>(async () =>
		{
			_ = await sut.ExtendAsync(TimeSpan.FromSeconds(15), cts.Token);
		});

		await Should.ThrowAsync<Exception>(() => sut.ReleaseAsync(cts.Token));
	}

	[Fact]
	public async Task DisposeAsync_NoThrow_WhenAlreadyDisposed()
	{
		var sut = CreateSut(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
		SetDisposed(sut, value: true);

		await Should.NotThrowAsync(async () => await sut.DisposeAsync());
	}

	private static IDistributedJobLock CreateSut(DateTimeOffset acquiredAt, DateTimeOffset expiresAt)
	{
		var lockType = typeof(SqlServerJobCoordinator).Assembly
			.GetType("Excalibur.Jobs.SqlServer.SqlServerDistributedJobLock", throwOnError: true)!;

		var constructor = lockType.GetConstructor(
			BindingFlags.Instance | BindingFlags.NonPublic,
			binder: null,
			[
				typeof(Func<SqlConnection>),
				typeof(string),
				typeof(string),
				typeof(string),
				typeof(DateTimeOffset),
				typeof(DateTimeOffset),
				typeof(ILogger)
			],
			modifiers: null);

		constructor.ShouldNotBeNull();

		Func<SqlConnection> connectionFactory = static () => new SqlConnection(UnreachableConnectionString);

		var instance = constructor!.Invoke(
			[
				connectionFactory,
				"Jobs",
				"job-1",
				"instance-1",
				acquiredAt,
				expiresAt,
				NullLogger.Instance
			]);

		return (IDistributedJobLock)instance;
	}

	private static void SetDisposed(IDistributedJobLock lockInstance, bool value)
	{
		var disposedField = lockInstance.GetType().GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic);
		disposedField.ShouldNotBeNull();
		disposedField!.SetValue(lockInstance, value);
	}
}
