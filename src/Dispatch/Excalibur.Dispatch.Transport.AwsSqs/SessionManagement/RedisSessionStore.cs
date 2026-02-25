// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Redis-based session store implementation.
/// </summary>
public sealed class RedisSessionStore : ISessionStore
{
	private readonly IDatabase _database;
	private readonly ILogger<RedisSessionStore> _logger;
	private readonly RedisSessionStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisSessionStore" /> class.
	/// </summary>
	/// <param name="connectionMultiplexer"> The Redis connection multiplexer. </param>
	/// <param name="logger"> The logger. </param>
	/// <param name="options"> The options. </param>
	public RedisSessionStore(
		IConnectionMultiplexer connectionMultiplexer,
		ILogger<RedisSessionStore> logger,
		IOptions<RedisSessionStoreOptions> options)
	{
		ArgumentNullException.ThrowIfNull(connectionMultiplexer);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(options);

		_database = connectionMultiplexer.GetDatabase();
		_logger = logger;
		_options = options.Value;
	}

	/// <summary>
	/// Creates a new session with the specified ID and timeout.
	/// </summary>
	/// <param name="sessionId"> The session ID. </param>
	/// <param name="timeout"> The session timeout. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The created session. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public async Task<SessionData> CreateAsync(string sessionId, TimeSpan timeout, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(sessionId);

		var session = new SessionData
		{
			Id = sessionId,
			State = AwsSessionState.Active,
			CreatedAt = DateTimeOffset.UtcNow,
			LastAccessedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.Add(timeout),
		};

		await CreateOrUpdateAsync(session, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Created session '{SessionId}' with timeout {Timeout}", sessionId, timeout);

		return session;
	}

	/// <summary>
	/// Updates an existing session.
	/// </summary>
	/// <param name="session"> The session to update. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The updated session. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public async Task<SessionData> UpdateAsync(SessionData session, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(session);

		await CreateOrUpdateAsync(session, cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Updated session '{SessionId}'", session.Id);

		return session;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Session data serialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Session data serialization uses reflection to dynamically access and serialize types")]
	public async Task CreateOrUpdateAsync(SessionData session, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(session);

		var key = GetSessionKey(session.Id);
		var json = JsonSerializer.Serialize(session);
		var expiry = session.ExpiresAt?.Subtract(DateTimeOffset.UtcNow);

		_ = await _database.StringSetAsync(key, json, expiry).ConfigureAwait(false);

		_logger.LogTrace("Stored session '{SessionId}' in Redis with key '{Key}'", session.Id, key);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Session data deserialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Session data deserialization uses reflection to dynamically create and populate types")]
	public async Task<SessionData?> TryGetAsync(string sessionId, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(sessionId);

		var key = GetSessionKey(sessionId);
		var json = await _database.StringGetAsync(key).ConfigureAwait(false);

		if (!json.HasValue)
		{
			_logger.LogTrace("Session '{SessionId}' not found in Redis", sessionId);
			return null;
		}

		try
		{
			var session = JsonSerializer.Deserialize<SessionData>(json.ToString());
			_logger.LogTrace("Retrieved session '{SessionId}' from Redis", sessionId);
			return session;
		}
		catch (JsonException ex)
		{
			_logger.LogWarning(ex, "Failed to deserialize session '{SessionId}' from Redis", sessionId);
			return null;
		}
	}

	/// <inheritdoc />
	public async Task DeleteAsync(string sessionId, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(sessionId);

		var key = GetSessionKey(sessionId);
		var deleted = await _database.KeyDeleteAsync(key).ConfigureAwait(false);

		if (deleted)
		{
			_logger.LogDebug("Deleted session '{SessionId}' from Redis", sessionId);
		}
		else
		{
			_logger.LogTrace("Session '{SessionId}' was not found when attempting to delete", sessionId);
		}
	}

	/// <inheritdoc />
	public async Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(sessionId);

		var key = GetSessionKey(sessionId);
		return await _database.KeyExistsAsync(key).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> GetCountAsync(CancellationToken cancellationToken)
	{
		// Note: This is an expensive operation in Redis for production use
		var pattern = GetSessionKey("*");
		var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
		return await Task.Factory.StartNew(
				() => server.Keys(pattern: pattern).Count(),
				cancellationToken,
				TaskCreationOptions.DenyChildAttach,
				TaskScheduler.Default)
			.ConfigureAwait(false);
	}

	private string GetSessionKey(string sessionId) => $"{_options.KeyPrefix}:sessions:{sessionId}";
}
