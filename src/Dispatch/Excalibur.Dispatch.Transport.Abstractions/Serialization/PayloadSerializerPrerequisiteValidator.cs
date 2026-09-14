// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Fails a host at start-up, naming the remedy, when a transport is registered without the payload
/// serializer every transport message bus requires.
/// </summary>
/// <remarks>
/// A transport serializes payloads but does not choose the serializer -- the wire format is the consuming
/// application's decision, and a transport that seated a default would make the format depend on which
/// transport happened to be registered first. So no transport package registers one, and a host that
/// composes a transport without a serialization registration cannot construct its message bus. Without
/// this check that surfaces as a container activation error naming an interface the consumer has never
/// heard of; with it, the host stops with a message naming the call that fixes it.
/// </remarks>
internal sealed class PayloadSerializerPrerequisiteValidator(IServiceProvider services)
	: IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

	/// <inheritdoc />
	public void Validate()
	{
		if (_services.GetService<IPayloadSerializer>() is not null)
		{
			return;
		}

		throw new InvalidOperationException(
			"No IPayloadSerializer is registered, so the transport's message bus cannot be constructed. "
			+ "A transport serializes message payloads but does not choose the serializer: the wire format "
			+ "is the application's decision, and a transport that supplied a default would make the format "
			+ "on the wire depend on which transport was registered first. Add a serialization registration "
			+ "alongside the transport -- services.AddPluggableSerialization() seats the built-in "
			+ "System.Text.Json serializer -- or register your own IPayloadSerializer. The cloud experience "
			+ "bundles (AddDispatchAws, AddDispatchAzure) already do this for you.");
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
