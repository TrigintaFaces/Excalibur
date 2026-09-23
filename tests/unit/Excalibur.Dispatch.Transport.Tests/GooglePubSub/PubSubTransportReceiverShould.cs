// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// Unit tests for <see cref="PubSubTransportReceiver"/>.
/// Validates constructor validation, source exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). See
/// <see cref="PubSubTransportSenderShould"/> for the GetService rationale.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class PubSubTransportReceiverShould : IAsyncDisposable
{
	private const string TestSource = "projects/test-project/subscriptions/orders-subscription";
	private readonly ISubscriberApiClientSeam _fakeClient;
	private readonly PubSubTransportReceiver _sut;

	public PubSubTransportReceiverShould()
	{
		_fakeClient = A.Fake<ISubscriberApiClientSeam>();
		_sut = new PubSubTransportReceiver(
			_fakeClient,
			TestSource,
			NullLogger<PubSubTransportReceiver>.Instance);
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_client_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportReceiver((ISubscriberApiClientSeam)null!, TestSource, NullLogger<PubSubTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportReceiver(A.Fake<ISubscriberApiClientSeam>(), null!, NullLogger<PubSubTransportReceiver>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportReceiver(A.Fake<ISubscriberApiClientSeam>(), TestSource, null!));
	}

	[Fact]
	public void Return_client_seam_via_GetService()
	{
		var result = _sut.GetService(typeof(ISubscriberApiClientSeam));
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
