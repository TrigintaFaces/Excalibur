// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Unit tests for <see cref="KafkaTransportSender"/>.
/// Validates constructor validation, destination exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). The GetService arms
/// here specifically pin the "hand out the native SDK handle" contract documented on
/// <see cref="ITransportSender.GetService(Type)"/> -- production already implements it correctly; without
/// this lock a regression to the base default (which answers only for types the instance itself
/// implements) would silently decline the native <see cref="IProducer{TKey,TValue}"/> capability.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class KafkaTransportSenderShould : IAsyncDisposable
{
	private const string TestDestination = "orders-topic";
	private readonly IProducer<string, byte[]> _fakeProducer;
	private readonly KafkaTransportSender _sut;

	public KafkaTransportSenderShould()
	{
		_fakeProducer = A.Fake<IProducer<string, byte[]>>();
		_sut = new KafkaTransportSender(
			_fakeProducer,
			TestDestination,
			NullLogger<KafkaTransportSender>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		_fakeProducer.Dispose();
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
			new KafkaTransportSender(null!, TestDestination, NullLogger<KafkaTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_destination_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KafkaTransportSender(A.Fake<IProducer<string, byte[]>>(), null!, NullLogger<KafkaTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KafkaTransportSender(A.Fake<IProducer<string, byte[]>>(), TestDestination, null!));
	}

	[Fact]
	public void Return_producer_via_GetService()
	{
		var result = _sut.GetService(typeof(IProducer<string, byte[]>));
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
