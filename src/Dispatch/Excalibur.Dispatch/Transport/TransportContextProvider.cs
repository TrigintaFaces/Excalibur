// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Default implementation of <see cref="ITransportContextProvider"/> that resolves
/// transport bindings from the <see cref="TransportBindingRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation looks for the <c>TransportBindingName</c> property on the message
/// context, which should be set by transport adapters when messages are received.
/// </para>
/// <para>
/// If no binding name is found in the context, or if the binding is not registered,
/// this method returns <see langword="null"/>, indicating that the message was
/// dispatched directly (not via a transport adapter).
/// </para>
/// </remarks>
public sealed class TransportContextProvider : ITransportContextProvider
{
	/// <summary>
	/// The well-known property name used by transport adapters to indicate the binding name.
	/// </summary>
	public const string TransportBindingNameProperty = "TransportBindingName";

	private readonly TransportBindingRegistry _registry;

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportContextProvider"/> class.
	/// </summary>
	/// <param name="registry">The transport binding registry.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is null.</exception>
	public TransportContextProvider(TransportBindingRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);
		_registry = registry;
	}

	/// <inheritdoc/>
	public ITransportBinding? GetTransportBinding(IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Check for binding name set by transport adapter
		var bindingName = context.GetProperty<string>(TransportBindingNameProperty);
		if (string.IsNullOrEmpty(bindingName))
		{
			return null;
		}

		// Resolve binding from registry
		return _registry.TryGetBinding(bindingName, out var binding) ? binding : null;
	}
}
