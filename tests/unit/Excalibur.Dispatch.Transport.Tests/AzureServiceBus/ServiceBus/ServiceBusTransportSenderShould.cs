// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.ServiceBus;

/// <summary>
/// Unit tests for <see cref="ServiceBusTransportSender"/>.
/// Validates constructor validation, destination exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). The GetService arms
/// here pin the "hand out the native SDK seam" contract -- production already implements it correctly;
/// without this lock a regression to the base default would silently decline the
/// <see cref="IServiceBusSenderSeam"/> capability.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class ServiceBusTransportSenderShould : IAsyncDisposable
{
	private const string TestDestination = "orders-queue";
	private readonly IServiceBusSenderSeam _fakeSender;
	private readonly ServiceBusTransportSender _sut;

	public ServiceBusTransportSenderShould()
	{
		_fakeSender = A.Fake<IServiceBusSenderSeam>();
		_sut = new ServiceBusTransportSender(
			_fakeSender,
			TestDestination,
			NullLogger<ServiceBusTransportSender>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		await _fakeSender.DisposeAsync();
	}

	[Fact]
	public void Expose_destination_from_constructor()
	{
		_sut.Destination.ShouldBe(TestDestination);
	}

	[Fact]
	public void Throw_when_sender_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportSender((IServiceBusSenderSeam)null!, TestDestination, NullLogger<ServiceBusTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_destination_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportSender(A.Fake<IServiceBusSenderSeam>(), null!, NullLogger<ServiceBusTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportSender(A.Fake<IServiceBusSenderSeam>(), TestDestination, null!));
	}

	[Fact]
	public void Return_sender_seam_via_GetService()
	{
		var result = _sut.GetService(typeof(IServiceBusSenderSeam));
		result.ShouldBe(_fakeSender);
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
