// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Configuration.Transport;

/// <summary>
/// Options-validation hook that surfaces <see cref="AddEventBindings"/> references
/// to unregistered transports at host startup rather than at
/// <see cref="BindingConfigurationBuilder.FromTransport"/> time.
/// </summary>
/// <remarks>
/// <para>
/// Registered via <c>AddEventBindings(...)</c> with <c>ValidateOnStart()</c>. It reads
/// the <see cref="TransportBindingRegistry"/>'s pending-reference list (populated by
/// <see cref="BindingConfigurationBuilder.FromTransport"/> when the named transport
/// is neither a materialized adapter nor a pending factory) and fails with an
/// explicit message naming the missing transports and the registration extensions
/// a consumer can call to resolve the gap.
/// </para>
/// </remarks>
internal sealed class BindingRegistrationValidator : IValidateOptions<BindingRegistrationValidationOptions>
{
	private readonly TransportBindingRegistry _bindingRegistry;
	private readonly ITransportRegistry _transportRegistry;

	public BindingRegistrationValidator(
		TransportBindingRegistry bindingRegistry,
		ITransportRegistry transportRegistry)
	{
		_bindingRegistry = bindingRegistry ?? throw new ArgumentNullException(nameof(bindingRegistry));
		_transportRegistry = transportRegistry ?? throw new ArgumentNullException(nameof(transportRegistry));
	}

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, BindingRegistrationValidationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var pending = _bindingRegistry.GetPendingTransportReferences();
		if (pending.Count == 0)
		{
			return ValidateOptionsResult.Success;
		}

		// Pending-at-binding-time references may have been resolved by a
		// subsequent AddXTransport call — re-check against live registry.
		var unresolved = new List<string>();
		foreach (var transportName in pending)
		{
			var hasAdapter = _transportRegistry.GetTransportAdapter(transportName) is not null;
			if (hasAdapter)
			{
				continue;
			}

			var isPendingFactory = false;
			if (_transportRegistry is TransportRegistry concrete)
			{
				foreach (var factoryName in concrete.GetPendingFactoryNames())
				{
					if (string.Equals(factoryName, transportName, StringComparison.Ordinal))
					{
						isPendingFactory = true;
						break;
					}
				}
			}

			if (!isPendingFactory)
			{
				unresolved.Add(transportName);
			}
		}

		if (unresolved.Count == 0)
		{
			return ValidateOptionsResult.Success;
		}

		var list = string.Join(", ", unresolved.Select(n => $"'{n}'"));
		return ValidateOptionsResult.Fail(
			$"AddEventBindings references transports that are not registered: {list}. " +
			"Register them via services.AddInMemoryTransport(name) / AddKafkaTransport(name, ...) / " +
			"AddRabbitMQTransport(name, ...) / AddAzureServiceBusTransport(name, ...) / etc. " +
			"before building the host.");
	}
}

/// <summary>
/// Marker options type used to participate in the
/// <c>Microsoft.Extensions.Options</c> validation pipeline so binding registration
/// errors surface via <c>ValidateOnStart()</c>.
/// </summary>
internal sealed class BindingRegistrationValidationOptions
{
}
