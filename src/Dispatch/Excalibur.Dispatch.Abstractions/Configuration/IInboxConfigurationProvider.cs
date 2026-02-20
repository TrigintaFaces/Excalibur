// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions.Configuration;

/// <summary>
/// Provides inbox configuration for handler types at runtime.
/// </summary>
/// <remarks>
/// <para>
/// This service is used by the <see cref="Dispatch.Messaging.Middleware.IdempotentHandlerMiddleware"/>
/// to retrieve configuration for handlers. Configuration is built at startup and cached
/// for runtime performance.
/// </para>
/// <para>
/// Configuration from this provider takes precedence over <see cref="IdempotentAttribute"/>
/// settings on handler classes.
/// </para>
/// </remarks>
public interface IInboxConfigurationProvider
{
	/// <summary>
	/// Gets the inbox configuration for a handler type, if one is configured.
	/// </summary>
	/// <param name="handlerType"> The handler type to get configuration for. </param>
	/// <returns>
	/// The configuration for the handler, or <see langword="null"/> if no configuration
	/// is registered for this handler type.
	/// </returns>
	InboxHandlerSettings? GetConfiguration(Type handlerType);

	/// <summary>
	/// Determines if a handler type has inbox configuration.
	/// </summary>
	/// <param name="handlerType"> The handler type to check. </param>
	/// <returns>
	/// <see langword="true"/> if the handler has configuration;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	bool HasConfiguration(Type handlerType);
}

/// <summary>
/// Immutable settings for a handler's inbox behavior.
/// </summary>
/// <remarks>
/// This record captures all inbox configuration for a handler, built at startup
/// from <see cref="IInboxConfigurationBuilder"/> configuration.
/// </remarks>
public sealed record InboxHandlerSettings
{
	/// <summary>
	/// Gets the retention period for processed message IDs.
	/// </summary>
	/// <value> Default is 24 hours (1440 minutes). </value>
	public TimeSpan Retention { get; init; } = TimeSpan.FromMinutes(1440);

	/// <summary>
	/// Gets a value indicating whether to use in-memory deduplication.
	/// </summary>
	/// <value>
	/// <see langword="true"/> for in-memory storage; <see langword="false"/> for persistent storage.
	/// Default is <see langword="false"/>.
	/// </value>
	public bool UseInMemory { get; init; }

	/// <summary>
	/// Gets the message ID extraction strategy.
	/// </summary>
	/// <value> Default is <see cref="MessageIdStrategy.FromHeader"/>. </value>
	public MessageIdStrategy Strategy { get; init; } = MessageIdStrategy.FromHeader;

	/// <summary>
	/// Gets the header name for message ID extraction.
	/// </summary>
	/// <value> Default is "MessageId". </value>
	public string HeaderName { get; init; } = "MessageId";

	/// <summary>
	/// Gets the type of custom message ID provider, if configured.
	/// </summary>
	/// <value>
	/// The provider type, or <see langword="null"/> if using built-in strategies.
	/// </value>
	public Type? MessageIdProviderType { get; init; }
}
