// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DotPulsar.Abstractions;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Pulsar;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Pulsar;

/// <summary>
/// Unit tests for <see cref="PulsarTransportSender"/>.
/// Validates constructor validation, destination exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr) -- one of the three
/// transports named directly for thin failure-path coverage. The GetService arm pins the "hand out the
/// native SDK handle" contract -- production already implements it correctly; without this lock a
/// regression to the base default would silently decline the native <see cref="IProducer{TMessage}"/>
/// capability.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class PulsarTransportSenderShould : IAsyncDisposable
{
	private const string TestDestination = "persistent://public/default/orders-topic";
	private readonly IProducer<byte[]> _fakeProducer;
	private readonly PulsarTransportSender _sut;

	public PulsarTransportSenderShould()
	{
		_fakeProducer = A.Fake<IProducer<byte[]>>();
		_sut = new PulsarTransportSender(
			_fakeProducer,
			TestDestination,
			NullLogger<PulsarTransportSender>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		await _fakeProducer.DisposeAsync();
	}

	[Fact]
	public void Expose_destination_from_constructor()
	{
		_sut.Destination.ShouldBe(TestDestination);
	}

	[Fact]
	public void Throw_when_producer_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PulsarTransportSender(null!, TestDestination, NullLogger<PulsarTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_destination_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PulsarTransportSender(A.Fake<IProducer<byte[]>>(), null!, NullLogger<PulsarTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PulsarTransportSender(A.Fake<IProducer<byte[]>>(), TestDestination, null!));
	}

	[Fact]
	public void Return_producer_via_GetService()
	{
		var result = _sut.GetService(typeof(IProducer<byte[]>));
		result.ShouldBe(_fakeProducer);
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
