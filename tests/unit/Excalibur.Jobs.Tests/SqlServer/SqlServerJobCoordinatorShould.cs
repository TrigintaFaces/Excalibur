// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Coordination;
using Excalibur.Jobs.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Jobs.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SqlServerJobCoordinatorShould : UnitTestBase
{
	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerJobCoordinator(null!, NullLogger<SqlServerJobCoordinator>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = Options.Create(new SqlServerJobCoordinatorOptions { ConnectionString = "Server=.;Database=Jobs;Trusted_Connection=True" });

		Should.Throw<ArgumentNullException>(() =>
			new SqlServerJobCoordinator(options, null!));
	}

	[Fact]
	public async Task ThrowWhenTryAcquireLockJobKeyIsNullOrWhitespace()
	{
		var sut = CreateSut();

		await Should.ThrowAsync<ArgumentException>(() => sut.TryAcquireLockAsync(null!, TimeSpan.FromSeconds(30), CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(() => sut.TryAcquireLockAsync(" ", TimeSpan.FromSeconds(30), CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenRegisterInstanceArgumentsAreInvalid()
	{
		var sut = CreateSut();
		var info = CreateInstanceInfo();

		await Should.ThrowAsync<ArgumentException>(() => sut.RegisterInstanceAsync(null!, info, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(() => sut.RegisterInstanceAsync(" ", info, CancellationToken.None));
		await Should.ThrowAsync<ArgumentNullException>(() => sut.RegisterInstanceAsync("instance-1", null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenUnregisterInstanceIdIsInvalid()
	{
		var sut = CreateSut();

		await Should.ThrowAsync<ArgumentException>(() => sut.UnregisterInstanceAsync(null!, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(() => sut.UnregisterInstanceAsync(" ", CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenDistributeJobJobKeyIsInvalid()
	{
		var sut = CreateSut();

		await Should.ThrowAsync<ArgumentException>(() => sut.DistributeJobAsync(null!, new { Job = "X" }, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(() => sut.DistributeJobAsync(" ", new { Job = "X" }, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenReportJobCompletionArgumentsAreInvalid()
	{
		var sut = CreateSut();

		await Should.ThrowAsync<ArgumentException>(() =>
			sut.ReportJobCompletionAsync(null!, "instance-1", true, new { }, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(() =>
			sut.ReportJobCompletionAsync("job-1", null!, true, new { }, CancellationToken.None));
	}

	[Fact]
	public async Task EnterTryAcquireLockFlow_WhenArgumentsAreValid_EvenIfSqlEndpointIsUnavailable()
	{
		var sut = CreateSutWithUnreachableSql();
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

		await Should.ThrowAsync<Exception>(() =>
			sut.TryAcquireLockAsync("job-1", TimeSpan.FromSeconds(30), cts.Token));
	}

	[Fact]
	public async Task EnterRegisterAndUnregisterFlows_WhenArgumentsAreValid_EvenIfSqlEndpointIsUnavailable()
	{
		var sut = CreateSutWithUnreachableSql();
		var info = CreateInstanceInfo();
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

		await Should.ThrowAsync<Exception>(() =>
			sut.RegisterInstanceAsync("instance-1", info, cts.Token));
		await Should.ThrowAsync<Exception>(() =>
			sut.UnregisterInstanceAsync("instance-1", cts.Token));
	}

	[Fact]
	public async Task EnterGetActiveInstancesAndDistributeFlows_WhenSqlEndpointIsUnavailable()
	{
		var sut = CreateSutWithUnreachableSql();
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

		await Should.ThrowAsync<Exception>(() =>
			sut.GetActiveInstancesAsync(cts.Token));
		await Should.ThrowAsync<Exception>(() =>
			sut.DistributeJobAsync("job-1", new { Work = "sync" }, cts.Token));
	}

	[Fact]
	public async Task EnterReportCompletionFlow_ForNullAndNonNullResults_WhenSqlEndpointIsUnavailable()
	{
		var sut = CreateSutWithUnreachableSql();
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

		await Should.ThrowAsync<Exception>(() =>
			sut.ReportJobCompletionAsync("job-1", "instance-1", success: true, result: new { Message = "ok" }, cts.Token));
		await Should.ThrowAsync<Exception>(() =>
			sut.ReportJobCompletionAsync("job-2", "instance-2", success: false, result: null, cts.Token));
	}

	private static SqlServerJobCoordinator CreateSut()
	{
		var options = Options.Create(new SqlServerJobCoordinatorOptions
		{
			ConnectionString = "Server=.;Database=Jobs;Trusted_Connection=True",
		});

		return new SqlServerJobCoordinator(options, NullLogger<SqlServerJobCoordinator>.Instance);
	}

	private static SqlServerJobCoordinator CreateSutWithUnreachableSql()
	{
		var options = Options.Create(new SqlServerJobCoordinatorOptions
		{
			ConnectionString = "Server=tcp:127.0.0.1,1;Database=Jobs;User ID=sa;Password=BadPassword123!;Encrypt=False;TrustServerCertificate=True;Connect Timeout=1",
		});

		return new SqlServerJobCoordinator(options, NullLogger<SqlServerJobCoordinator>.Instance);
	}

	private static JobInstanceInfo CreateInstanceInfo() =>
		new(
			"instance-1",
			"host-1",
			new JobInstanceCapabilities(8, ["*"]));
}
