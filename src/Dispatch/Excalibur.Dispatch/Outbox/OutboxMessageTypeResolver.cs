// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Outbox;

internal static class OutboxMessageTypeResolver
{
	public static string Register(Type type)
	{
		MessageTypeRegistry.RegisterType(type);
		// Queued rows survive application version changes without conflating different assemblies.
		var name = MessageNameHelper.GetDeclaredName(type)
			?? $"{type.FullName ?? type.Name}, {type.Assembly.GetName().Name}";
		if (!MessageTypeRegistry.TryGetType(name, out var registeredType) || registeredType != type)
		{
			throw new InvalidOperationException($"Outbox message type name '{name}' is ambiguous.");
		}
		return name;
	}

	public static IDispatchMessage Restore(OutboundMessage message, IPayloadSerializer serializer)
	{
		if (!MessageTypeRegistry.TryGetType(message.MessageType, out var type)
			|| !typeof(IDispatchMessage).IsAssignableFrom(type))
		{
			throw new InvalidOperationException($"Outbox message type '{message.MessageType}' is not a registered Dispatch message.");
		}
		var restored = serializer.DeserializeObject(message.Payload, type);
		if (restored is not IDispatchMessage dispatchMessage || restored.GetType() != type)
		{
			throw new InvalidOperationException($"Outbox payload does not match its registered message type '{message.MessageType}'.");
		}
		return dispatchMessage;
	}
}
