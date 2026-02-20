// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Frozen;

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Implementation of <see cref="IInboxConfigurationProvider"/> with cached configuration.
/// </summary>
/// <remarks>
/// This class is thread-safe. All configuration is resolved at startup and stored
/// in a frozen dictionary for fast runtime lookups.
/// </remarks>
internal sealed class InboxConfigurationProvider : IInboxConfigurationProvider
{
	private readonly FrozenDictionary<Type, InboxHandlerSettings> _configurations;

	/// <summary>
	/// Initializes a new instance of the <see cref="InboxConfigurationProvider"/> class.
	/// </summary>
	/// <param name="configurations"> The pre-resolved handler configurations. </param>
	internal InboxConfigurationProvider(Dictionary<Type, InboxHandlerSettings> configurations)
	{
		ArgumentNullException.ThrowIfNull(configurations);
		_configurations = configurations.ToFrozenDictionary();
	}

	/// <inheritdoc />
	public InboxHandlerSettings? GetConfiguration(Type handlerType)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		return _configurations.GetValueOrDefault(handlerType);
	}

	/// <inheritdoc />
	public bool HasConfiguration(Type handlerType)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		return _configurations.ContainsKey(handlerType);
	}
}
