// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.TestTypes;

/// <summary>
/// Basic properties for message handling in integration tests.
/// Mimics RabbitMQ IBasicProperties for testing purposes.
/// </summary>
public interface IBasicProperties
{
	/// <summary>
	/// Gets or sets the content type.
	/// </summary>
	string? ContentType { get; set; }

	/// <summary>
	/// Gets or sets the content encoding.
	/// </summary>
	string? ContentEncoding { get; set; }

	/// <summary>
	/// Gets or sets the message headers.
	/// </summary>
	IDictionary<string, object?>? Headers { get; set; }

	/// <summary>
	/// Gets or sets the delivery mode.
	/// </summary>
	byte DeliveryMode { get; set; }

	/// <summary>
	/// Gets or sets the message priority.
	/// </summary>
	byte Priority { get; set; }

	/// <summary>
	/// Gets or sets the correlation ID.
	/// </summary>
	string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the reply-to address.
	/// </summary>
	string? ReplyTo { get; set; }

	/// <summary>
	/// Gets or sets the message expiration.
	/// </summary>
	string? Expiration { get; set; }

	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	string? MessageId { get; set; }

	/// <summary>
	/// Gets or sets the message timestamp.
	/// </summary>
	long Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	string? Type { get; set; }

	/// <summary>
	/// Gets or sets the user ID.
	/// </summary>
	string? UserId { get; set; }

	/// <summary>
	/// Gets or sets the app ID.
	/// </summary>
	string? AppId { get; set; }
}

/// <summary>
/// Basic properties for test message handling.
/// </summary>
public class BasicProperties : IBasicProperties
{
	/// <inheritdoc />
	public string? ContentType { get; set; } = "application/json";

	/// <inheritdoc />
	public string? ContentEncoding { get; set; }

	/// <inheritdoc />
	public IDictionary<string, object?>? Headers { get; set; } = new Dictionary<string, object?>();

	/// <inheritdoc />
	public byte DeliveryMode { get; set; }

	/// <inheritdoc />
	public byte Priority { get; set; }

	/// <inheritdoc />
	public string? CorrelationId { get; set; }

	/// <inheritdoc />
	public string? ReplyTo { get; set; }

	/// <inheritdoc />
	public string? Expiration { get; set; }

	/// <inheritdoc />
	public string? MessageId { get; set; }

	/// <inheritdoc />
	public long Timestamp { get; set; }

	/// <inheritdoc />
	public string? Type { get; set; }

	/// <inheritdoc />
	public string? UserId { get; set; }

	/// <inheritdoc />
	public string? AppId { get; set; }
}

/// <summary>
/// Mock connection factory for testing message broker connections.
/// </summary>
public class ConnectionFactory
{
	/// <summary>
	/// Gets or sets the host name.
	/// </summary>
	public string HostName { get; set; } = "localhost";

	/// <summary>
	/// Gets or sets the port.
	/// </summary>
	public int Port { get; set; } = 5672;

	/// <summary>
	/// Gets or sets the username.
	/// </summary>
	public string UserName { get; set; } = "guest";

	/// <summary>
	/// Gets or sets the password.
	/// </summary>
	public string Password { get; set; } = "guest";

	/// <summary>
	/// Gets or sets the virtual host.
	/// </summary>
	public string VirtualHost { get; set; } = "/";

	/// <summary>
	/// Gets or sets a value indicating whether automatic recovery is enabled.
	/// </summary>
	public bool AutomaticRecoveryEnabled { get; set; } = true;

	/// <summary>
	/// Creates a new connection.
	/// </summary>
	/// <returns>A mock connection.</returns>
	public IConnection CreateConnection()
	{
		return new MockConnection(this);
	}
}

/// <summary>
/// Mock connection interface for testing.
/// </summary>
public interface IConnection : IDisposable
{
	/// <summary>
	/// Gets a value indicating whether the connection is open.
	/// </summary>
	bool IsOpen { get; }

	/// <summary>
	/// Creates a new channel/model.
	/// </summary>
	IModel CreateModel();

	/// <summary>
	/// Closes the connection.
	/// </summary>
	void Close();
}

/// <summary>
/// Mock model/channel interface for testing.
/// </summary>
public interface IModel : IDisposable
{
	/// <summary>
	/// Gets a value indicating whether the channel is open.
	/// </summary>
	bool IsOpen { get; }

	/// <summary>
	/// Creates basic properties.
	/// </summary>
	BasicProperties CreateBasicProperties();

	/// <summary>
	/// Closes the channel.
	/// </summary>
	void Close();
}

/// <summary>
/// Mock connection implementation.
/// </summary>
internal sealed class MockConnection : IConnection
{
	private bool _disposed;

	public MockConnection(ConnectionFactory factory)
	{
		IsOpen = true;
	}

	public bool IsOpen { get; private set; }

	public IModel CreateModel() => new MockModel();

	public void Close()
	{
		IsOpen = false;
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			Close();
			_disposed = true;
		}
	}
}

/// <summary>
/// Mock model implementation.
/// </summary>
internal sealed class MockModel : IModel
{
	private bool _disposed;

	public bool IsOpen { get; private set; } = true;

	public BasicProperties CreateBasicProperties() => new();

	public void Close()
	{
		IsOpen = false;
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			Close();
			_disposed = true;
		}
	}
}

/// <summary>
/// Options for the channel message pump used in integration tests.
/// </summary>
public class ChannelMessagePumpOptions
{
	/// <summary>
	/// Gets or sets the maximum number of concurrent consumers.
	/// </summary>
	public int MaxConcurrentConsumers { get; set; } = 1;

	/// <summary>
	/// Gets or sets the batch size for processing messages.
	/// </summary>
	public int BatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets the timeout for message processing.
	/// </summary>
	public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to enable metrics collection.
	/// </summary>
	public bool EnableMetrics { get; set; } = true;
}

/// <summary>
/// Options for session-based message handling in integration tests.
/// </summary>
public class SessionOptions
{
	/// <summary>
	/// Gets or sets the session ID.
	/// </summary>
	public string? SessionId { get; set; }

	/// <summary>
	/// Gets or sets the session timeout.
	/// </summary>
	public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets a value indicating whether to enable session locking.
	/// </summary>
	public bool EnableSessionLocking { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum concurrent sessions.
	/// </summary>
	public int MaxConcurrentSessions { get; set; } = 8;
}
