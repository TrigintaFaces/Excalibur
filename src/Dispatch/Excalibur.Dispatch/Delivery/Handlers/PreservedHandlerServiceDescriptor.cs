// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>Retains the handler's activation metadata across the DI descriptor boundary.</summary>
internal sealed class PreservedHandlerServiceDescriptor : ServiceDescriptor
{
	internal PreservedHandlerServiceDescriptor(Type serviceType,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType, ServiceLifetime lifetime)
		: base(serviceType, handlerType, lifetime) => HandlerType = handlerType;

	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
	internal Type HandlerType { get; }
}
