// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Unit tests for <see cref="RabbitMqTransportSender"/>.
/// Validates constructor validation, destination exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). The GetService arms
/// here pin the "hand out the native SDK handle" contract -- production already implements it correctly;
/// without this lock a regression to the base default would silently decline the native
/// <see cref="IChannel"/> capability.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class RabbitMqTransportSenderShould : IAsyncDisposable
{
	private const string TestDestination = "orders-queue";
	private const string TestExchange = "orders-exchange";
	private const string TestRoutingKey = "orders-queue";
	private readonly IChannel _fakeChannel;
	private readonly RabbitMqTransportSender _sut;

	public RabbitMqTransportSenderShould()
	{
		_fakeChannel = A.Fake<IChannel>();
		_sut = new RabbitMqTransportSender(
			_fakeChannel,
			TestDestination,
			TestExchange,
			TestRoutingKey,
			NullLogger<RabbitMqTransportSender>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		_fakeChannel.Dispose();
	}

	[Fact]
	public void Expose_destination_from_constructor()
	{
		_sut.Destination.ShouldBe(TestDestination);
	}

	[Fact]
	public void Throw_when_channel_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportSender(null!, TestDestination, TestExchange, TestRoutingKey, NullLogger<RabbitMqTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_destination_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportSender(A.Fake<IChannel>(), null!, TestExchange, TestRoutingKey, NullLogger<RabbitMqTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportSender(A.Fake<IChannel>(), TestDestination, TestExchange, TestRoutingKey, null!));
	}

	[Fact]
	public void Return_channel_via_GetService()
	{
		var result = _sut.GetService(typeof(IChannel));
		result.ShouldBe(_fakeChannel);
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
