// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Declares the stable name a message is known by, independent of the type's namespace, assembly
/// and version.
/// </summary>
/// <remarks>
/// <para>
/// A message's stored identity is declared here and nowhere else. It is required: a name derived from
/// the type would embed its namespace, assembly and assembly version, so moving the type or shipping a
/// new version would change the identity of data already written and make it unreadable.
/// </para>
/// <para>
/// The name is chosen once and is permanent. Give it a publisher prefix and a bounded context --
/// <c>"Contoso.Sales.CustomerCreated"</c> -- so it is unique across every package a consumer might
/// combine, and says who defines the event's meaning. It owes nothing to the code layout, so the type
/// stays free to move. This is the shape Azure Event Grid uses for its own event types, and it
/// satisfies the CloudEvents guidance that <c>type</c> identify the organization defining the event. To rename a message later, declare the new name here and keep the old one with
/// <see cref="MessageNameAliasAttribute"/>, or data written under it becomes unreadable.
/// </para>
/// <para>
/// Names share one global namespace: two message types declaring the same name is refused at
/// registration, because stored data records the name and nothing else, so the two could not be told
/// apart when read back.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [MessageName("customer-created")]
/// public sealed record CustomerCreated(Guid CustomerId, string Name) : IDomainEvent;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class MessageNameAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MessageNameAttribute"/> class.
	/// </summary>
	/// <param name="name">The stable name this event is stored under.</param>
	/// <exception cref="ArgumentException"><paramref name="name"/> is null or whitespace.</exception>
	public MessageNameAttribute(string name)
	{
		Name = MessageNameValidator.Validate(name);
	}

	/// <summary>Gets the stable name this event is stored under.</summary>
	public string Name { get; }
}

/// <summary>
/// Declares a name this message was known by previously, so data already written keeps resolving.
/// </summary>
/// <remarks>
/// <para>
/// Apply this when an event's stored name changes -- because the type moved namespace or assembly,
/// or because its declared <see cref="MessageNameAttribute"/> name was replaced. An alias affects
/// reading only: events are always written under the type's current name, so a retired name stops
/// spreading and the store converges as new events arrive.
/// </para>
/// <para>
/// Apply it once per historical name, and keep them. Removing one makes every event still stored
/// under that name unreadable.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [MessageName("customer-created")]
/// [MessageNameAlias("Contoso.Legacy.Events.CustomerCreated, Contoso.Legacy")]
/// public sealed record CustomerCreated(Guid CustomerId, string Name) : IDomainEvent;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MessageNameAliasAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MessageNameAliasAttribute"/> class.
	/// </summary>
	/// <param name="name">A name this event was previously stored under.</param>
	/// <exception cref="ArgumentException"><paramref name="name"/> is null or whitespace.</exception>
	public MessageNameAliasAttribute(string name)
	{
		Name = MessageNameValidator.Validate(name);
	}

	/// <summary>Gets a name this event was previously stored under.</summary>
	public string Name { get; }
}
