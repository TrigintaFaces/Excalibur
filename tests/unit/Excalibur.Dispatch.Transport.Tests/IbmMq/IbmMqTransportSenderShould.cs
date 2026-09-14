// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.IbmMq;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.IbmMq;

/// <summary>
/// Unit tests for <see cref="IbmMqTransportSender"/>.
/// Validates constructor validation, destination exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr) -- one of the three
/// transports named directly for thin failure-path coverage. The GetService arm pins the "hand out the
/// native capability" contract -- production already implements it correctly; without this lock a
/// regression to the base default would silently decline the <see cref="IIbmMqConnectionProvider"/>
/// capability.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class IbmMqTransportSenderShould : IAsyncDisposable
{
	private const string TestDestination = "ORDERS.QUEUE";
	private readonly IIbmMqConnectionProvider _fakeConnectionProvider;
	private readonly IbmMqTransportSender _sut;

	public IbmMqTransportSenderShould()
	{
		_fakeConnectionProvider = A.Fake<IIbmMqConnectionProvider>();
		_sut = new IbmMqTransportSender(
			_fakeConnectionProvider,
			TestDestination,
			NullLogger<IbmMqTransportSender>.Instance);
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();

	[Fact]
	public void Expose_destination_from_constructor()
	{
		_sut.Destination.ShouldBe(TestDestination);
	}

	[Fact]
	public void Throw_when_connection_provider_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new IbmMqTransportSender(null!, TestDestination, NullLogger<IbmMqTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_destination_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new IbmMqTransportSender(A.Fake<IIbmMqConnectionProvider>(), null!, NullLogger<IbmMqTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new IbmMqTransportSender(A.Fake<IIbmMqConnectionProvider>(), TestDestination, null!));
	}

	[Fact]
	public void Return_connection_provider_via_GetService()
	{
		var result = _sut.GetService(typeof(IIbmMqConnectionProvider));
		result.ShouldBe(_fakeConnectionProvider);
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
