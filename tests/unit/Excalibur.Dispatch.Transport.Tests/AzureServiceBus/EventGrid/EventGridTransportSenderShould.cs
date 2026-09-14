// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure;
using Azure.Messaging.EventGrid;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.EventGrid;

/// <summary>
/// Unit tests for <see cref="EventGridTransportSender"/>.
/// Validates constructor validation, destination exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). No seam exists for
/// <see cref="EventGridPublisherClient"/> (it is sealed's cousin -- non-virtual -- so the constructor
/// takes the concrete type directly); the client is constructed with a dummy endpoint/key so no network
/// call happens, per ADR-142 SS D7 (constructing a real SDK object is fine, faking it is not). The
/// GetService arm pins the "hand out the native SDK handle" contract -- production already implements it
/// correctly; without this lock a regression to the base default would silently decline the capability.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class EventGridTransportSenderShould : IAsyncDisposable
{
	private readonly EventGridPublisherClient _client = new(
		new Uri("https://dummy-topic.westus2-1.eventgrid.azure.net/api/events"),
		new AzureKeyCredential("dummy-key"));

	private readonly IOptions<EventGridTransportOptions> _options = Microsoft.Extensions.Options.Options.Create(
		new EventGridTransportOptions { TopicEndpoint = "https://dummy-topic.westus2-1.eventgrid.azure.net/api/events" });

	private readonly EventGridTransportSender _sut;

	public EventGridTransportSenderShould()
	{
		_sut = new EventGridTransportSender(_client, _options, NullLogger<EventGridTransportSender>.Instance);
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();

	[Fact]
	public void Expose_destination_from_options()
	{
		_sut.Destination.ShouldBe(_options.Value.Destination);
	}

	[Fact]
	public void Throw_when_client_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventGridTransportSender(null!, _options, NullLogger<EventGridTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_options_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventGridTransportSender(_client, null!, NullLogger<EventGridTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventGridTransportSender(_client, _options, null!));
	}

	[Fact]
	public void Return_client_via_GetService()
	{
		var result = _sut.GetService(typeof(EventGridPublisherClient));
		result.ShouldBe(_client);
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
