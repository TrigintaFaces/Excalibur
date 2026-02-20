// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Represents a message bus registration that can be resolved at runtime.
/// </summary>
public interface IMessageBusRegistration
{
	/// <summary>
	/// Gets the message bus name.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Gets a value indicating whether the bus is remote.
	/// </summary>
	bool IsRemote { get; }
}

/// <summary>
/// Default implementation of <see cref="IMessageBusRegistration" />.
/// </summary>
public sealed class MessageBusRegistration : IMessageBusRegistration
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MessageBusRegistration" /> class.
	/// </summary>
	/// <param name="name"> The message bus name. </param>
	/// <param name="isRemote"> Whether the bus is remote. </param>
	public MessageBusRegistration(string name, bool isRemote)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		Name = name;
		IsRemote = isRemote;
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public bool IsRemote { get; }
}
