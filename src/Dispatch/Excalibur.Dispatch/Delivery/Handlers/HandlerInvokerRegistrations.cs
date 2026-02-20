// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Module initializer that registers known handler invokers for AOT compatibility. This is a temporary solution until the source generator
/// can run properly.
/// </summary>
internal static class HandlerInvokerRegistrations
{
	[ModuleInitializer]
	[SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
		Justification = "Required for AOT handler registration")]
	public static void Initialize()
	{
		// Register known handlers here to avoid reflection at runtime Example registrations (uncomment and add actual handler types):

		// HandlerInvokerRegistry.RegisterInvoker<PingCommandHandler, PingCommand>( (handler, message, ct) => handler.HandleAsync(message, ct));

		// HandlerInvokerRegistry.RegisterInvoker<GetUserQueryHandler, GetUserQuery, UserDto>( (handler, message, ct) =>
		// handler.HandleAsync(message, ct));

		// HandlerInvokerRegistry.RegisterInvoker<UserCreatedEventHandler, UserCreatedEvent>( (handler, message, ct) =>
		// handler.HandleAsync(message, ct));
	}
}
