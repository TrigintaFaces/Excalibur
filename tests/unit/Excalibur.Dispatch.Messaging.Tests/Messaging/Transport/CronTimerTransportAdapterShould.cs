// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
///     Tests for the <see cref="CronTimerTransportAdapter" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CronTimerTransportAdapterShould : IAsyncDisposable
{
	private readonly CronTimerTransportAdapter _sut;

	public CronTimerTransportAdapterShould()
	{
		_sut = new CronTimerTransportAdapter(
			NullLogger<CronTimerTransportAdapter>.Instance,
			A.Fake<ICronScheduler>(),
			A.Fake<IServiceProvider>(),
			new CronTimerTransportAdapterOptions { CronExpression = "*/5 * * * *" });
	}

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() =>
			new CronTimerTransportAdapter(
				null!,
				A.Fake<ICronScheduler>(),
				A.Fake<IServiceProvider>(),
				new CronTimerTransportAdapterOptions()));

	[Fact]
	public void ThrowForNullCronScheduler() =>
		Should.Throw<ArgumentNullException>(() =>
			new CronTimerTransportAdapter(
				NullLogger<CronTimerTransportAdapter>.Instance,
				null!,
				A.Fake<IServiceProvider>(),
				new CronTimerTransportAdapterOptions()));

	[Fact]
	public void ThrowForNullServiceProvider() =>
		Should.Throw<ArgumentNullException>(() =>
			new CronTimerTransportAdapter(
				NullLogger<CronTimerTransportAdapter>.Instance,
				A.Fake<ICronScheduler>(),
				null!,
				new CronTimerTransportAdapterOptions()));

	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() =>
			new CronTimerTransportAdapter(
				NullLogger<CronTimerTransportAdapter>.Instance,
				A.Fake<ICronScheduler>(),
				A.Fake<IServiceProvider>(),
				null!));

	[Fact]
	public void CreateSuccessfully()
	{
		_sut.ShouldNotBeNull();
	}

	[Fact]
	public void HaveCorrectDefaultName()
	{
		CronTimerTransportAdapter.DefaultName.ShouldBe("CronTimer");
	}

	[Fact]
	public void HaveCorrectTransportTypeName()
	{
		CronTimerTransportAdapter.TransportTypeName.ShouldBe("crontimer");
	}

	[Fact]
	public void HaveCorrectTransportType()
	{
		_sut.TransportType.ShouldBe(CronTimerTransportAdapter.TransportTypeName);
	}

	[Fact]
	public void NotBeRunningInitially()
	{
		_sut.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public void ImplementITransportAdapter()
	{
		_sut.ShouldBeAssignableTo<ITransportAdapter>();
	}

	[Fact]
	public void ImplementITransportHealthChecker()
	{
		_sut.ShouldBeAssignableTo<ITransportHealthChecker>();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		_sut.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	public async ValueTask DisposeAsync() => await _sut.DisposeAsync().ConfigureAwait(false);
}
