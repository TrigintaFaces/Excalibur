// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Implementation of <see cref="IInboxHandlerConfiguration"/> for configuring handler inbox settings.
/// </summary>
/// <remarks>
/// This class is used at application startup to configure inbox settings.
/// It is not thread-safe and should only be used during the DI configuration phase.
/// </remarks>
internal sealed class InboxHandlerConfiguration : IInboxHandlerConfiguration
{
	private TimeSpan _retention = TimeSpan.FromMinutes(1440); // 24 hours default
	private bool _useInMemory;
	private MessageIdStrategy _strategy = MessageIdStrategy.FromHeader;
	private string _headerName = "MessageId";
	private Type? _messageIdProviderType;

	/// <inheritdoc />
	public IInboxHandlerConfiguration WithRetention(TimeSpan retention)
	{
		if (retention <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(retention),
				Resources.InboxHandlerConfiguration_RetentionMustBePositive);
		}

		_retention = retention;
		return this;
	}

	/// <inheritdoc />
	public IInboxHandlerConfiguration UseInMemory()
	{
		_useInMemory = true;
		return this;
	}

	/// <inheritdoc />
	public IInboxHandlerConfiguration UsePersistent()
	{
		_useInMemory = false;
		return this;
	}

	/// <inheritdoc />
	public IInboxHandlerConfiguration WithStrategy(MessageIdStrategy strategy)
	{
		_strategy = strategy;
		return this;
	}

	/// <inheritdoc />
	public IInboxHandlerConfiguration WithHeaderName(string headerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
		_headerName = headerName;
		return this;
	}

	/// <inheritdoc />
	public IInboxHandlerConfiguration WithMessageIdProvider<TProvider>()
		where TProvider : class, IMessageIdProvider
	{
		_messageIdProviderType = typeof(TProvider);
		_strategy = MessageIdStrategy.Custom;
		return this;
	}

	/// <summary>
	/// Builds the immutable settings from this configuration.
	/// </summary>
	/// <returns>The built settings.</returns>
	internal InboxHandlerSettings Build()
	{
		return new InboxHandlerSettings
		{
			Retention = _retention,
			UseInMemory = _useInMemory,
			Strategy = _strategy,
			HeaderName = _headerName,
			MessageIdProviderType = _messageIdProviderType,
		};
	}
}
