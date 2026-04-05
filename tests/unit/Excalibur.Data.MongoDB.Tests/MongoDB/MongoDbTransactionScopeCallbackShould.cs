// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.Abstractions.Persistence;

using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Transactions;

/// <summary>
/// Regression tests for MongoDbTransactionScope callback exception logging (Sprint 670, T.5).
/// Verifies that onCommit/onComplete/onRollback callback exceptions are logged
/// instead of being silently swallowed.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Feature", "MongoDB")]
public sealed class MongoDbTransactionScopeCallbackShould : IDisposable
{
	private readonly ILogger<MongoDbPersistenceProvider> _logger;
	private readonly MongoDbPersistenceProvider _provider;

	public MongoDbTransactionScopeCallbackShould()
	{
		_logger = A.Fake<ILogger<MongoDbPersistenceProvider>>();
		A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);
		_provider = new MongoDbPersistenceProvider(_logger);
	}

	public void Dispose()
	{
		_provider.Dispose();
	}

	/// <summary>
	/// Bypasses EnsureSessionAsync by setting _sessionInitialized = true via reflection.
	/// The test-only MongoDbPersistenceProvider constructor doesn't provide a real MongoDB client,
	/// so we skip session initialization to focus on testing callback behavior.
	/// </summary>
	private static void BypassSessionInitialization(ITransactionScope scope)
	{
		var field = scope.GetType().GetField("_sessionInitialized", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("Expected _sessionInitialized field on MongoDbTransactionScope");
		field.SetValue(scope, true);
	}

	[Fact]
	public async Task LogException_WhenOnCommitCallbackThrows()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		BypassSessionInitialization(scope);
		var callbackScope = (ITransactionScopeCallbacks)scope;

		callbackScope.OnCommit(() => throw new InvalidOperationException("Commit callback failed"));

		// Act -- CommitAsync should NOT throw even though callback throws
		await scope.CommitAsync(CancellationToken.None);

		// Assert -- verify the logger was called with the exception
		A.CallTo(_logger)
			.Where(call => call.Method.Name == "Log" &&
				call.Arguments.Any(arg => LogLevel.Error.Equals(arg)))
			.MustHaveHappened();
	}

	[Fact]
	public async Task LogException_WhenOnRollbackCallbackThrows()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		BypassSessionInitialization(scope);
		var callbackScope = (ITransactionScopeCallbacks)scope;

		callbackScope.OnRollback(() => throw new InvalidOperationException("Rollback callback failed"));

		// Act -- RollbackAsync should NOT throw
		await scope.RollbackAsync(CancellationToken.None);

		// Assert
		A.CallTo(_logger)
			.Where(call => call.Method.Name == "Log" &&
				call.Arguments.Any(arg => LogLevel.Error.Equals(arg)))
			.MustHaveHappened();
	}

	[Fact]
	public async Task LogException_WhenOnCompleteCallbackThrows_AfterCommit()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		BypassSessionInitialization(scope);
		var callbackScope = (ITransactionScopeCallbacks)scope;

		callbackScope.OnComplete(_ => throw new InvalidOperationException("Complete callback failed"));

		// Act
		await scope.CommitAsync(CancellationToken.None);

		// Assert
		A.CallTo(_logger)
			.Where(call => call.Method.Name == "Log" &&
				call.Arguments.Any(arg => LogLevel.Error.Equals(arg)))
			.MustHaveHappened();
	}

	[Fact]
	public async Task NotThrow_WhenMultipleCallbacksFail()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		BypassSessionInitialization(scope);
		var callbackScope = (ITransactionScopeCallbacks)scope;

		callbackScope.OnCommit(() => throw new InvalidOperationException("Callback 1 failed"));
		callbackScope.OnCommit(() => throw new ArgumentException("Callback 2 failed"));
		callbackScope.OnComplete(_ => throw new TimeoutException("Callback 3 failed"));

		// Act -- should NOT throw despite multiple callback failures
		await Should.NotThrowAsync(() => scope.CommitAsync(CancellationToken.None));

		// Assert -- all 3 exceptions should be logged
		A.CallTo(_logger)
			.Where(call => call.Method.Name == "Log" &&
				call.Arguments.Any(arg => LogLevel.Error.Equals(arg)))
			.MustHaveHappened(3, Times.Exactly);
	}

	[Fact]
	public async Task ExecuteAllCallbacks_EvenWhenEarlierOneFails()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		BypassSessionInitialization(scope);
		var callbackScope = (ITransactionScopeCallbacks)scope;
		var secondCallbackExecuted = false;

		callbackScope.OnCommit(() => throw new InvalidOperationException("First fails"));
		callbackScope.OnCommit(() =>
		{
			secondCallbackExecuted = true;
			return Task.CompletedTask;
		});

		// Act
		await scope.CommitAsync(CancellationToken.None);

		// Assert -- second callback should still execute despite first throwing
		secondCallbackExecuted.ShouldBeTrue();
	}
}
