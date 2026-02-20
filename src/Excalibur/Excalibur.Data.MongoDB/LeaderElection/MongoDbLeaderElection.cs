// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.LeaderElection;

/// <summary>
/// MongoDB implementation of <see cref="ILeaderElection"/> using findAndModify with TTL.
/// </summary>
/// <remarks>
/// <para>
/// Uses MongoDB's findOneAndUpdate with upsert semantics and condition checks to implement
/// distributed leader election. A TTL index on the lock collection ensures stale leaders
/// are automatically cleaned up.
/// </para>
/// <para>
/// The lease renewal loop runs on a background task, periodically extending the TTL
/// while this instance holds leadership.
/// </para>
/// </remarks>
public sealed partial class MongoDbLeaderElection : ILeaderElection, IAsyncDisposable
{
	private readonly IMongoCollection<MongoDbLeaderElectionDocument> _collection;
	private readonly string _resourceName;
	private readonly MongoDbLeaderElectionOptions _options;
	private readonly LeaderElectionOptions _electionOptions;
	private readonly ILogger<MongoDbLeaderElection> _logger;

#if NET9_0_OR_GREATER
	private readonly Lock _lock = new();
#else
	private readonly object _lock = new();
#endif

	private CancellationTokenSource? _renewalCts;
	private Task? _renewalTask;
	private bool _isStarted;
	private volatile bool _isLeader;
	private string? _currentLeaderId;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbLeaderElection"/> class.
	/// </summary>
	/// <param name="client">The MongoDB client.</param>
	/// <param name="resourceName">The resource name for the election lock.</param>
	/// <param name="mongoOptions">The MongoDB leader election options.</param>
	/// <param name="electionOptions">The leader election options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbLeaderElection(
		IMongoClient client,
		string resourceName,
		IOptions<MongoDbLeaderElectionOptions> mongoOptions,
		IOptions<LeaderElectionOptions> electionOptions,
		ILogger<MongoDbLeaderElection> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
		ArgumentNullException.ThrowIfNull(mongoOptions);
		ArgumentNullException.ThrowIfNull(electionOptions);
		ArgumentNullException.ThrowIfNull(logger);

		_resourceName = resourceName;
		_options = mongoOptions.Value;
		_options.Validate();
		_electionOptions = electionOptions.Value;
		_logger = logger;

		CandidateId = _electionOptions.InstanceId ?? (Environment.MachineName + "-" + Guid.NewGuid().ToString("N")[..8]);

		var database = client.GetDatabase(_options.DatabaseName);
		_collection = database.GetCollection<MongoDbLeaderElectionDocument>(_options.CollectionName);

		EnsureTtlIndex();
	}

	/// <inheritdoc/>
	public event EventHandler<LeaderElectionEventArgs>? BecameLeader;

	/// <inheritdoc/>
	public event EventHandler<LeaderElectionEventArgs>? LostLeadership;

	/// <inheritdoc/>
	public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

	/// <inheritdoc/>
	public string CandidateId { get; }

	/// <inheritdoc/>
	public bool IsLeader => _isLeader;

	/// <inheritdoc/>
	public string? CurrentLeaderId
	{
		get
		{
			lock (_lock)
			{
				return _currentLeaderId;
			}
		}
	}

	/// <inheritdoc/>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_isStarted)
		{
			return Task.CompletedTask;
		}

		_isStarted = true;
		_renewalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		_renewalTask = RunElectionLoopAsync(_renewalCts.Token);

		LogStarted(_resourceName, CandidateId);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_isStarted)
		{
			return;
		}

		_isStarted = false;

		if (_renewalCts != null)
		{
			await _renewalCts.CancelAsync().ConfigureAwait(false);
		}

		if (_renewalTask != null)
		{
			try
			{
				await _renewalTask.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected during shutdown
			}
		}

		// Release leadership if held
		if (_isLeader)
		{
			await ReleaseLockAsync().ConfigureAwait(false);
			SetLeader(false, null);
		}

		LogStopped(_resourceName, CandidateId);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_isStarted)
		{
			try
			{
				await StopAsync(CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				LogDisposeError(ex);
			}
		}

		_renewalCts?.Dispose();
	}

	private async Task RunElectionLoopAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				if (_isLeader)
				{
					var renewed = await TryRenewLockAsync(cancellationToken).ConfigureAwait(false);
					if (!renewed)
					{
						SetLeader(false, null);
					}
				}
				else
				{
					var acquired = await TryAcquireLockAsync(cancellationToken).ConfigureAwait(false);
					if (acquired)
					{
						SetLeader(true, CandidateId);
					}
					else
					{
						// Check who the current leader is
						var currentLeader = await GetCurrentLeaderAsync(cancellationToken).ConfigureAwait(false);
						UpdateCurrentLeader(currentLeader);
					}
				}

				var interval = _isLeader
					? TimeSpan.FromSeconds(_options.RenewIntervalSeconds)
					: _electionOptions.RetryInterval;

				await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogElectionError(ex);
				await Task.Delay(_electionOptions.RetryInterval, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private async Task<bool> TryAcquireLockAsync(CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		var expiresAt = now.AddSeconds(_options.LeaseDurationSeconds);

		// Try to create or take over expired lock
		var filter = Builders<MongoDbLeaderElectionDocument>.Filter.And(
			Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.ResourceName, _resourceName),
			Builders<MongoDbLeaderElectionDocument>.Filter.Or(
				Builders<MongoDbLeaderElectionDocument>.Filter.Lt(d => d.ExpiresAt, now),
				Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.CandidateId, CandidateId)));

		var update = Builders<MongoDbLeaderElectionDocument>.Update
			.Set(d => d.CandidateId, CandidateId)
			.Set(d => d.AcquiredAt, now)
			.Set(d => d.ExpiresAt, expiresAt)
			.Set(d => d.LastRenewedAt, now);

		var options = new FindOneAndUpdateOptions<MongoDbLeaderElectionDocument>
		{
			IsUpsert = true,
			ReturnDocument = ReturnDocument.After
		};

		try
		{
			var result = await _collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken)
				.ConfigureAwait(false);

			return result?.CandidateId == CandidateId;
		}
		catch (MongoCommandException ex) when (ex.Code == 11000)
		{
			// Duplicate key: another candidate holds the lock
			return false;
		}
	}

	private async Task<bool> TryRenewLockAsync(CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		var expiresAt = now.AddSeconds(_options.LeaseDurationSeconds);

		var filter = Builders<MongoDbLeaderElectionDocument>.Filter.And(
			Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.ResourceName, _resourceName),
			Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.CandidateId, CandidateId));

		var update = Builders<MongoDbLeaderElectionDocument>.Update
			.Set(d => d.ExpiresAt, expiresAt)
			.Set(d => d.LastRenewedAt, now);

		var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		return result.MatchedCount > 0;
	}

	private async Task ReleaseLockAsync()
	{
		var filter = Builders<MongoDbLeaderElectionDocument>.Filter.And(
			Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.ResourceName, _resourceName),
			Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.CandidateId, CandidateId));

		_ = await _collection.DeleteOneAsync(filter).ConfigureAwait(false);
	}

	private async Task<string?> GetCurrentLeaderAsync(CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;

		var filter = Builders<MongoDbLeaderElectionDocument>.Filter.And(
			Builders<MongoDbLeaderElectionDocument>.Filter.Eq(d => d.ResourceName, _resourceName),
			Builders<MongoDbLeaderElectionDocument>.Filter.Gte(d => d.ExpiresAt, now));

		var doc = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
		return doc?.CandidateId;
	}

	private void SetLeader(bool isLeader, string? leaderId)
	{
		var wasLeader = _isLeader;
		string? previousLeaderId;

		lock (_lock)
		{
			previousLeaderId = _currentLeaderId;
			_isLeader = isLeader;
			_currentLeaderId = leaderId;
		}

		if (isLeader && !wasLeader)
		{
			BecameLeader?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
			LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeaderId, CandidateId, _resourceName));
			LogBecameLeader(_resourceName, CandidateId);
		}
		else if (!isLeader && wasLeader)
		{
			LostLeadership?.Invoke(this, new LeaderElectionEventArgs(CandidateId, _resourceName));
			LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(CandidateId, leaderId, _resourceName));
			LogLostLeadership(_resourceName, CandidateId);
		}
	}

	private void UpdateCurrentLeader(string? leaderId)
	{
		string? previousLeaderId;
		lock (_lock)
		{
			previousLeaderId = _currentLeaderId;
			if (previousLeaderId == leaderId)
			{
				return;
			}

			_currentLeaderId = leaderId;
		}

		LeaderChanged?.Invoke(this, new LeaderChangedEventArgs(previousLeaderId, leaderId, _resourceName));
	}

	private void EnsureTtlIndex()
	{
		var indexKeysDefinition = Builders<MongoDbLeaderElectionDocument>.IndexKeys.Ascending(d => d.ExpiresAt);
		var indexOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "ttl_expiresAt" };
		var indexModel = new CreateIndexModel<MongoDbLeaderElectionDocument>(indexKeysDefinition, indexOptions);

		try
		{
			_collection.Indexes.CreateOne(indexModel);
		}
		catch (MongoCommandException)
		{
			// Index may already exist with different options - tolerate
		}
	}

	[LoggerMessage(DataMongoDbEventId.LeaderElectionStarted, LogLevel.Information,
		"MongoDB leader election started for resource '{ResourceName}' with candidate '{CandidateId}'")]
	private partial void LogStarted(string resourceName, string candidateId);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionStopped, LogLevel.Information,
		"MongoDB leader election stopped for resource '{ResourceName}' with candidate '{CandidateId}'")]
	private partial void LogStopped(string resourceName, string candidateId);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionBecameLeader, LogLevel.Information,
		"Candidate became leader for resource '{ResourceName}' with candidate '{CandidateId}'")]
	private partial void LogBecameLeader(string resourceName, string candidateId);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionLostLeadership, LogLevel.Warning,
		"Candidate lost leadership for resource '{ResourceName}' with candidate '{CandidateId}'")]
	private partial void LogLostLeadership(string resourceName, string candidateId);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionError, LogLevel.Error, "Error in leader election loop")]
	private partial void LogElectionError(Exception exception);

	[LoggerMessage(DataMongoDbEventId.LeaderElectionDisposeError, LogLevel.Warning, "Error during leader election dispose")]
	private partial void LogDisposeError(Exception exception);
}
