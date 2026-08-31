// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>
/// Serializes an event payload through the type-info resolver a host configured, translating a missing
/// declaration into <see cref="EventTypeNotDeclaredException"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every store that honours a host-supplied resolver reaches the serializer the same way, and each one
/// used to write that call out for itself. Identical code in ten places is where a contract diverges: the
/// stores disagreed for a long time about whether an undeclared type ended the append with a thrown
/// exception or a returned failure, and each provider's own test agreed with its own provider. Routing
/// the call through one place makes the answer a property of this method rather than of whichever store
/// the consumer happens to have chosen.
/// </para>
/// <para>
/// The bytes are unchanged. This performs the same resolver lookup and the same serializer call the
/// stores performed inline, so an event whose type <em>is</em> declared serializes exactly as before —
/// only the failure path is new.
/// </para>
/// </remarks>
public static class ResolvedEventPayload
{
	/// <summary>
	/// Serializes an event using the resolver carried by the supplied options.
	/// </summary>
	/// <param name="domainEvent">The event to serialize.</param>
	/// <param name="jsonOptions">Canonical serializer options carrying the host's type-info resolver.</param>
	/// <param name="aggregateId">The stream the append targets, reported on refusal.</param>
	/// <param name="aggregateType">The aggregate type the append targets, reported on refusal.</param>
	/// <returns>The UTF-8 encoded event payload.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="domainEvent"/> or <paramref name="jsonOptions"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="EventTypeNotDeclaredException">
	/// The resolver does not declare <paramref name="domainEvent"/>'s runtime type. Nothing is written.
	/// </exception>
	public static byte[] Serialize(
		IDomainEvent domainEvent,
		JsonSerializerOptions jsonOptions,
		string? aggregateId,
		string? aggregateType)
	{
		ArgumentNullException.ThrowIfNull(domainEvent);
		ArgumentNullException.ThrowIfNull(jsonOptions);

		var eventType = domainEvent.GetType();

		try
		{
			return JsonSerializer.SerializeToUtf8Bytes(domainEvent, jsonOptions.GetTypeInfo(eventType));
		}
		catch (NotSupportedException ex)
		{
			// The serializer reports a missing declaration as NotSupportedException carrying only a
			// message. Re-raise it with the offending type and the targeted stream as data, so a caller
			// can act on the fault without parsing prose. The original is preserved as the inner
			// exception because it names the resolver that was consulted, which the caller configured.
			throw new EventTypeNotDeclaredException(eventType, aggregateId, aggregateType, ex);
		}
	}
}
