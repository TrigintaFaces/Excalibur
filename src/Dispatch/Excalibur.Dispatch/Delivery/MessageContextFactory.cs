// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Default implementation of message context factory.
/// </summary>
public sealed class MessageContextFactory(IServiceProvider serviceProvider) : IMessageContextFactory
{
	/// <summary>
	/// Creates a new message context instance.
	/// </summary>
	/// <returns> A new message context. </returns>
	public IMessageContext CreateContext()
	{
		// Use parameterless constructor (for pooling) and initialize with service provider
		var context = new MessageContext();
		context.Initialize(serviceProvider);
		return context;
	}

	/// <summary>
	/// Creates a new message context instance with the specified properties.
	/// </summary>
	/// <param name="properties"> Optional properties to initialize the context with. </param>
	/// <returns> A new message context with the specified properties. </returns>
	public IMessageContext CreateContext(IDictionary<string, object>? properties)
	{
		var context = new MessageContext();
		context.Initialize(serviceProvider);

		// Copy properties to the context if needed
		if (properties != null)
		{
			foreach (var kvp in properties)
			{
				context.Items[kvp.Key] = kvp.Value;
			}
		}

		return context;
	}

	/// <inheritdoc />
	public IMessageContext CreateChildContext(IMessageContext parent)
	{
		ArgumentNullException.ThrowIfNull(parent);
		return parent.CreateChildContext();
	}

	/// <inheritdoc />
	/// <remarks>
	/// This is a no-op for the non-pooled factory. The context will be garbage collected normally.
	/// </remarks>
	public void Return(IMessageContext context)
	{
		// No-op for non-pooled factory - context will be garbage collected
	}
}
