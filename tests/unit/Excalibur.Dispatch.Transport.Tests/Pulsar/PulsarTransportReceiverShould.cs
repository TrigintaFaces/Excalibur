// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DotPulsar.Abstractions;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Pulsar;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Pulsar;

/// <summary>
/// Unit tests for <see cref="PulsarTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="PulsarTransportSenderShould"/> for the GetService rationale.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class PulsarTransportReceiverShould : IAsyncDisposable
{
	private const string TestSource = "persistent://public/default/orders-topic";
	private readonly IConsumer<byte[]> _fakeConsumer;
	private readonly PulsarTransportReceiver _sut;

	public PulsarTransportReceiverShould()
	{
		_fakeConsumer = A.Fake<IConsumer<byte[]>>();
		_sut = new PulsarTransportReceiver(
			_fakeConsumer,
			TestSource,
			NullLogger<PulsarTransportReceiver>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		await _fakeConsumer.DisposeAsync();
	}

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_consumer_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PulsarTransportReceiver(null!, TestSource, NullLogger<PulsarTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PulsarTransportReceiver(A.Fake<IConsumer<byte[]>>(), null!, NullLogger<PulsarTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PulsarTransportReceiver(A.Fake<IConsumer<byte[]>>(), TestSource, null!));
	}

	[Fact]
	public void Return_consumer_via_GetService()
	{
		var result = _sut.GetService(typeof(IConsumer<byte[]>));
		result.ShouldBe(_fakeConsumer);
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
