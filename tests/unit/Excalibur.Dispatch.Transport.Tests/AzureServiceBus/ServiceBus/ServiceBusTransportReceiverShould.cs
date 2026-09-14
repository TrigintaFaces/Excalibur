// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.ServiceBus;

/// <summary>
/// Unit tests for <see cref="ServiceBusTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="ServiceBusTransportSenderShould"/> for the GetService rationale.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class ServiceBusTransportReceiverShould : IAsyncDisposable
{
	private const string TestSource = "orders-queue";
	private readonly IServiceBusReceiverSeam _fakeReceiver;
	private readonly ServiceBusTransportReceiver _sut;

	public ServiceBusTransportReceiverShould()
	{
		_fakeReceiver = A.Fake<IServiceBusReceiverSeam>();
		_sut = new ServiceBusTransportReceiver(
			_fakeReceiver,
			TestSource,
			NullLogger<ServiceBusTransportReceiver>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		await _fakeReceiver.DisposeAsync();
	}

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_receiver_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportReceiver((IServiceBusReceiverSeam)null!, TestSource, NullLogger<ServiceBusTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportReceiver(A.Fake<IServiceBusReceiverSeam>(), null!, NullLogger<ServiceBusTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportReceiver(A.Fake<IServiceBusReceiverSeam>(), TestSource, null!));
	}

	[Fact]
	public void Return_receiver_seam_via_GetService()
	{
		var result = _sut.GetService(typeof(IServiceBusReceiverSeam));
		result.ShouldBe(_fakeReceiver);
	}

	[Fact]
	public void Return_null_for_unknown_service_type()
	{
		var result = _sut.GetService(typeof(string));
		result.ShouldBeNull();
	}

	[Fact]
	public void Throw_when_GetService_type_is_null()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}
}
