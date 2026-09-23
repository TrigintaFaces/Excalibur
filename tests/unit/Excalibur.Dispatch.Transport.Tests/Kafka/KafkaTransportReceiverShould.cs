// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Unit tests for <see cref="KafkaTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="KafkaTransportSenderShould"/> for the GetService rationale.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class KafkaTransportReceiverShould : IAsyncDisposable
{
	private const string TestSource = "orders-topic";
	private readonly IConsumer<string, byte[]> _fakeConsumer;
	private readonly KafkaTransportReceiver _sut;

	public KafkaTransportReceiverShould()
	{
		_fakeConsumer = A.Fake<IConsumer<string, byte[]>>();
		_sut = new KafkaTransportReceiver(
			_fakeConsumer,
			TestSource,
			NullLogger<KafkaTransportReceiver>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		_fakeConsumer.Dispose();
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
			new KafkaTransportReceiver(null!, TestSource, NullLogger<KafkaTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KafkaTransportReceiver(A.Fake<IConsumer<string, byte[]>>(), null!, NullLogger<KafkaTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KafkaTransportReceiver(A.Fake<IConsumer<string, byte[]>>(), TestSource, null!));
	}

	[Fact]
	public void Return_consumer_via_GetService()
	{
		var result = _sut.GetService(typeof(IConsumer<string, byte[]>));
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
