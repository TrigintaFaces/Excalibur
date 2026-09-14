// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// Unit tests for <see cref="PubSubTransportSender"/>.
/// Validates constructor validation, destination exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). The GetService arm
/// pins the "hand out the native SDK seam" contract -- production already implements it correctly;
/// without this lock a regression to the base default would silently decline the
/// <see cref="IPublisherClientSeam"/> capability.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class PubSubTransportSenderShould : IAsyncDisposable
{
	private const string TestDestination = "projects/test-project/topics/orders-topic";
	private readonly IPublisherClientSeam _fakeClient;
	private readonly PubSubTransportSender _sut;

	public PubSubTransportSenderShould()
	{
		_fakeClient = A.Fake<IPublisherClientSeam>();
		_sut = new PubSubTransportSender(
			_fakeClient,
			TestDestination,
			NullLogger<PubSubTransportSender>.Instance);
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();

	[Fact]
	public void Expose_destination_from_constructor()
	{
		_sut.Destination.ShouldBe(TestDestination);
	}

	[Fact]
	public void Throw_when_client_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportSender((IPublisherClientSeam)null!, TestDestination, NullLogger<PubSubTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_destination_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportSender(A.Fake<IPublisherClientSeam>(), null!, NullLogger<PubSubTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportSender(A.Fake<IPublisherClientSeam>(), TestDestination, null!));
	}

	[Fact]
	public void Return_client_seam_via_GetService()
	{
		var result = _sut.GetService(typeof(IPublisherClientSeam));
		result.ShouldBe(_fakeClient);
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
