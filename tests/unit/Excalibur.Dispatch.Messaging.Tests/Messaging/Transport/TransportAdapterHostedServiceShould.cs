// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
///     Tests for the <see cref="TransportAdapterHostedService" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransportAdapterHostedServiceShould
{
	[Fact]
	public void ThrowForNullTransportRegistry() =>
		Should.Throw<ArgumentNullException>(() =>
			new TransportAdapterHostedService(
				null!,
				Microsoft.Extensions.Options.Options.Create(new TransportAdapterHostedServiceOptions()),
				A.Fake<IServiceProvider>(),
				NullLogger<TransportAdapterHostedService>.Instance));

	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() =>
			new TransportAdapterHostedService(
				new TransportRegistry(),
				null!,
				A.Fake<IServiceProvider>(),
				NullLogger<TransportAdapterHostedService>.Instance));

	[Fact]
	public void ThrowForNullServiceProvider() =>
		Should.Throw<ArgumentNullException>(() =>
			new TransportAdapterHostedService(
				new TransportRegistry(),
				Microsoft.Extensions.Options.Options.Create(new TransportAdapterHostedServiceOptions()),
				null!,
				NullLogger<TransportAdapterHostedService>.Instance));

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() =>
			new TransportAdapterHostedService(
				new TransportRegistry(),
				Microsoft.Extensions.Options.Options.Create(new TransportAdapterHostedServiceOptions()),
				A.Fake<IServiceProvider>(),
				null!));

	[Fact]
	public void CreateSuccessfully()
	{
		var sut = new TransportAdapterHostedService(
			new TransportRegistry(),
			Microsoft.Extensions.Options.Options.Create(new TransportAdapterHostedServiceOptions()),
			A.Fake<IServiceProvider>(),
			NullLogger<TransportAdapterHostedService>.Instance);

		sut.ShouldNotBeNull();
	}

	[Fact]
	public async Task StartWithNoRegisteredAdapters()
	{
		var sut = new TransportAdapterHostedService(
			new TransportRegistry(),
			Microsoft.Extensions.Options.Options.Create(new TransportAdapterHostedServiceOptions()),
			A.Fake<IServiceProvider>(),
			NullLogger<TransportAdapterHostedService>.Instance);

		await Should.NotThrowAsync(
			() => sut.StartAsync(CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task StopWithNoRegisteredAdapters()
	{
		var sut = new TransportAdapterHostedService(
			new TransportRegistry(),
			Microsoft.Extensions.Options.Options.Create(new TransportAdapterHostedServiceOptions()),
			A.Fake<IServiceProvider>(),
			NullLogger<TransportAdapterHostedService>.Instance);

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await Should.NotThrowAsync(
			() => sut.StopAsync(CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void ImplementIHostedService()
	{
		var sut = new TransportAdapterHostedService(
			new TransportRegistry(),
			Microsoft.Extensions.Options.Options.Create(new TransportAdapterHostedServiceOptions()),
			A.Fake<IServiceProvider>(),
			NullLogger<TransportAdapterHostedService>.Instance);

		sut.ShouldBeAssignableTo<Microsoft.Extensions.Hosting.IHostedService>();
	}

	[Fact]
	public void ImplementITransportLifecycleManager()
	{
		var sut = new TransportAdapterHostedService(
			new TransportRegistry(),
			Microsoft.Extensions.Options.Options.Create(new TransportAdapterHostedServiceOptions()),
			A.Fake<IServiceProvider>(),
			NullLogger<TransportAdapterHostedService>.Instance);

		sut.ShouldBeAssignableTo<ITransportLifecycleManager>();
	}
}
