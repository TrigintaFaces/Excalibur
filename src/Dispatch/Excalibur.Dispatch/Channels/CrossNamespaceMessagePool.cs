// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Pooling;

using Microsoft.Extensions.ObjectPool;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Adapter pool that bridges between Common.Abstractions and Messaging.Abstractions namespaces.
/// </summary>
internal sealed class CrossNamespaceMessagePool<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
TMessage>(MessagePoolService poolManager)
	: ObjectPool<IDispatchMessage>
	where TMessage : class, IDispatchMessage, new()
{
	private readonly MessagePoolService _poolManager = poolManager ?? throw new ArgumentNullException(nameof(poolManager));

	/// <inheritdoc/>
	public override IDispatchMessage Get() =>

		// Rent from the Common pool and return as Messaging.Abstractions
		_poolManager.RentMessage<TMessage>();

	/// <inheritdoc/>
	public override void Return(IDispatchMessage item)
	{
		// Return to the Common pool if it's the right type
		if (item is TMessage message)
		{
			_poolManager.ReturnMessage(message);
		}
	}
}
