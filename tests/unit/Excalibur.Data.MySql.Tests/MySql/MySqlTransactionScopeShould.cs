// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.MySql;


namespace Excalibur.Data.Tests.MySql;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MySqlTransactionScopeShould
{
	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new MySqlTransactionScope(IsolationLevel.ReadCommitted, null!));
	}

	[Fact]
	public void HaveActiveStatus()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);

		scope.Status.ShouldBe(TransactionStatus.Active);
	}

	[Fact]
	public void HaveTransactionId()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);

		scope.TransactionId.ShouldNotBeNullOrWhiteSpace();
		scope.TransactionId.Length.ShouldBe(32); // Guid "N" format
	}

	[Fact]
	public void HaveCorrectIsolationLevel()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.Serializable, logger);

		scope.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
	}

	[Fact]
	public void HaveStartTime()
	{
		var before = DateTimeOffset.UtcNow;
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);
		var after = DateTimeOffset.UtcNow;

		scope.StartTime.ShouldBeGreaterThanOrEqualTo(before);
		scope.StartTime.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void HaveDefaultTimeout()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);

		scope.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowCustomTimeout()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);

		scope.Timeout = TimeSpan.FromMinutes(2);
		scope.Timeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public async Task EnlistProviderAsync_ThrowsForNullProvider()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);

		await Should.ThrowAsync<ArgumentNullException>(
			() => scope.EnlistProviderAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task EnlistProviderAsync_Succeeds()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);
		var provider = A.Fake<IPersistenceProvider>();

		await Should.NotThrowAsync(
			() => scope.EnlistProviderAsync(provider, CancellationToken.None));
	}

	[Fact]
	public async Task EnlistConnectionAsync_ThrowsForNullConnection()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);

		await Should.ThrowAsync<ArgumentNullException>(
			() => scope.EnlistConnectionAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void GetEnlistedProviders_ReturnsEmptyInitially()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);

		scope.GetEnlistedProviders().ShouldBeEmpty();
	}

	[Fact]
	public async Task GetEnlistedProviders_ReturnsEnlistedProvider()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);
		var provider = A.Fake<IPersistenceProvider>();

		await scope.EnlistProviderAsync(provider, CancellationToken.None);

		scope.GetEnlistedProviders().ShouldContain(provider);
	}

	[Fact]
	public async Task CommitAsync_SetsStatusToCommitted()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);

		await scope.CommitAsync(CancellationToken.None);

		scope.Status.ShouldBe(TransactionStatus.Committed);
	}

	[Fact]
	public async Task RollbackAsync_SetsStatusToRolledBack()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		using var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);

		await scope.RollbackAsync(CancellationToken.None);

		scope.Status.ShouldBe(TransactionStatus.RolledBack);
	}

	[Fact]
	public void Dispose_DoesNotThrow()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);

		Should.NotThrow(() => scope.Dispose());
	}

	[Fact]
	public void DoubleDispose_DoesNotThrow()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);

		scope.Dispose();
		Should.NotThrow(() => scope.Dispose());
	}

	[Fact]
	public async Task DisposeAsync_DoesNotThrow()
	{
		var logger = EnabledTestLogger.Create<MySqlTransactionScope>();
		var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, logger);

		await Should.NotThrowAsync(() => scope.DisposeAsync().AsTask());
	}

	[Fact]
	public async Task EnlistProviderAsync_ThrowsWhenDisposed()
	{
		var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, EnabledTestLogger.Create<MySqlTransactionScope>());
		scope.Dispose();

		await Should.ThrowAsync<ObjectDisposedException>(() =>
			scope.EnlistProviderAsync(A.Fake<IPersistenceProvider>(), CancellationToken.None));
	}

	[Fact]
	public async Task EnlistConnectionAsync_ThrowsWhenDisposed()
	{
		var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, EnabledTestLogger.Create<MySqlTransactionScope>());
		scope.Dispose();

		await Should.ThrowAsync<ObjectDisposedException>(() =>
			scope.EnlistConnectionAsync(A.Fake<System.Data.IDbConnection>(), CancellationToken.None));
	}

	[Fact]
	public async Task CommitAsync_ThrowsWhenDisposed()
	{
		var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, EnabledTestLogger.Create<MySqlTransactionScope>());
		scope.Dispose();

		await Should.ThrowAsync<ObjectDisposedException>(() =>
			scope.CommitAsync(CancellationToken.None));
	}

	[Fact]
	public async Task RollbackAsync_ThrowsWhenDisposed()
	{
		var scope = new MySqlTransactionScope(IsolationLevel.ReadCommitted, EnabledTestLogger.Create<MySqlTransactionScope>());
		scope.Dispose();

		await Should.ThrowAsync<ObjectDisposedException>(() =>
			scope.RollbackAsync(CancellationToken.None));
	}
}

