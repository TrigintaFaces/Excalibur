// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Implementation of <see cref="IInboxConfigurationBuilder"/> for configuring selective inbox.
/// </summary>
/// <remarks>
/// <para>
/// This builder is used at application startup to configure inbox settings.
/// It is not thread-safe and should only be used during the DI configuration phase.
/// </para>
/// <para>
/// Selection rules are evaluated in precedence order when building:
/// <list type="number">
/// <item><description>ForHandler (exact type match) - HIGHEST</description></item>
/// <item><description>ForHandlersMatching (predicate)</description></item>
/// <item><description>ForMessageType (message type)</description></item>
/// <item><description>ForNamespace (namespace prefix) - LOWEST</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class InboxConfigurationBuilder : IInboxConfigurationBuilder
{
	/// <summary>
	/// Selection rule with priority for ordering.
	/// </summary>
	private sealed record SelectionRule(
		int Priority,
		Func<Type, bool> Matches,
		InboxHandlerConfiguration Configuration);

	private readonly Dictionary<Type, InboxHandlerConfiguration> _exactTypeConfigs = [];
	private readonly List<SelectionRule> _predicateRules = [];
	private readonly List<(Type MessageType, InboxHandlerConfiguration Config)> _messageTypeConfigs = [];
	private readonly List<(string Prefix, InboxHandlerConfiguration Config)> _namespaceConfigs = [];

	/// <inheritdoc />
	public IInboxHandlerConfiguration ForHandler<THandler>()
		where THandler : class
	{
		var handlerType = typeof(THandler);
		if (!_exactTypeConfigs.TryGetValue(handlerType, out var config))
		{
			config = new InboxHandlerConfiguration();
			_exactTypeConfigs[handlerType] = config;
		}

		return config;
	}

	/// <inheritdoc />
	public IInboxConfigurationBuilder ForHandlersMatching(
		Func<Type, bool> predicate,
		Action<IInboxHandlerConfiguration> configure)
	{
		ArgumentNullException.ThrowIfNull(predicate);
		ArgumentNullException.ThrowIfNull(configure);

		var config = new InboxHandlerConfiguration();
		configure(config);
		_predicateRules.Add(new SelectionRule(2, predicate, config));
		return this;
	}

	/// <inheritdoc />
	public IInboxConfigurationBuilder ForNamespace(
		string namespacePrefix,
		Action<IInboxHandlerConfiguration> configure)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(namespacePrefix);
		ArgumentNullException.ThrowIfNull(configure);

		var config = new InboxHandlerConfiguration();
		configure(config);
		_namespaceConfigs.Add((namespacePrefix, config));
		return this;
	}

	/// <inheritdoc />
	public IInboxConfigurationBuilder ForMessageType<TMessage>(
		Action<IInboxHandlerConfiguration> configure)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(configure);

		var config = new InboxHandlerConfiguration();
		configure(config);
		_messageTypeConfigs.Add((typeof(TMessage), config));
		return this;
	}

	/// <summary>
	/// Builds the configuration provider from all registered rules.
	/// </summary>
	/// <param name="handlerTypes">
	/// All handler types registered in the system. The builder will evaluate
	/// selection rules against these types and cache the results.
	/// </param>
	/// <returns> A configuration provider with cached settings. </returns>
	internal InboxConfigurationProvider Build(IEnumerable<Type> handlerTypes)
	{
		ArgumentNullException.ThrowIfNull(handlerTypes);

		var configs = new Dictionary<Type, InboxHandlerSettings>();

		foreach (var handlerType in handlerTypes)
		{
			var settings = ResolveConfiguration(handlerType);
			if (settings is not null)
			{
				configs[handlerType] = settings;
			}
		}

		return new InboxConfigurationProvider(configs);
	}

	/// <summary>
	/// Determines if a handler type processes a specific message type.
	/// </summary>
	private static bool HandlerProcessesMessageType(Type handlerType, Type messageType)
	{
		// Check all interfaces implemented by the handler
		var interfaces = handlerType.GetInterfaces();
		foreach (var iface in interfaces)
		{
			if (!iface.IsGenericType)
			{
				continue;
			}

			// Check for IEventHandler<T>, ICommandHandler<T,R>, etc.
			var genericArgs = iface.GetGenericArguments();
			if (genericArgs.Length > 0 && genericArgs[0] == messageType)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Resolves configuration for a handler type using selection precedence.
	/// </summary>
	private InboxHandlerSettings? ResolveConfiguration(Type handlerType)
	{
		// 1. Exact type match (highest priority)
		if (_exactTypeConfigs.TryGetValue(handlerType, out var exactConfig))
		{
			return exactConfig.Build();
		}

		// 2. Predicate match
		foreach (var rule in _predicateRules)
		{
			if (rule.Matches(handlerType))
			{
				return rule.Configuration.Build();
			}
		}

		// 3. Message type match
		foreach (var (messageType, msgConfig) in _messageTypeConfigs)
		{
			if (HandlerProcessesMessageType(handlerType, messageType))
			{
				return msgConfig.Build();
			}
		}

		// 4. Namespace prefix match (lowest priority)
		var handlerNamespace = handlerType.Namespace ?? string.Empty;
		foreach (var (prefix, nsConfig) in _namespaceConfigs)
		{
			if (handlerNamespace.StartsWith(prefix, StringComparison.Ordinal))
			{
				return nsConfig.Build();
			}
		}

		// No matching configuration
		return null;
	}
}
