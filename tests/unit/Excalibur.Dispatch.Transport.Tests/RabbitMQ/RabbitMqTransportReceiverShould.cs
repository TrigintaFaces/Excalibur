// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Unit tests for <see cref="RabbitMqTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="RabbitMqTransportSenderShould"/> for the GetService rationale.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class RabbitMqTransportReceiverShould : IAsyncDisposable
{
	private const string TestSource = "orders-queue";
	private const string TestQueueName = "orders-queue";
	private readonly IChannel _fakeChannel;
	private readonly RabbitMqTransportReceiver _sut;

	public RabbitMqTransportReceiverShould()
	{
		_fakeChannel = A.Fake<IChannel>();
		_sut = new RabbitMqTransportReceiver(
			_fakeChannel,
			TestSource,
			TestQueueName,
			NullLogger<RabbitMqTransportReceiver>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		_fakeChannel.Dispose();
	}

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_channel_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportReceiver(null!, TestSource, TestQueueName, NullLogger<RabbitMqTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportReceiver(A.Fake<IChannel>(), null!, TestQueueName, NullLogger<RabbitMqTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportReceiver(A.Fake<IChannel>(), TestSource, TestQueueName, null!));
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
