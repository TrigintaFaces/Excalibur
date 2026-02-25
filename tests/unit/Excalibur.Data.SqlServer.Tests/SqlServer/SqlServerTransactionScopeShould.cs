// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.SqlServer.Persistence;

using Microsoft.Extensions.Logging.Abstractions;

using TransactionStatus = Excalibur.Data.Abstractions.Persistence.TransactionStatus;

namespace Excalibur.Data.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class SqlServerTransactionScopeShould
{
	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1), null!));
	}

	[Fact]
	public void InitializeWithCorrectProperties()
	{
		// Arrange & Act
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Assert
		sut.TransactionId.ShouldNotBeNullOrWhiteSpace();
		sut.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
		sut.Status.ShouldBe(TransactionStatus.Active);
		sut.Timeout.ShouldBe(TimeSpan.FromMinutes(1));
		sut.StartTime.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
	}

	[Fact]
	public void HaveUniqueTransactionIds()
	{
		// Arrange
		using var scope1 = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);
		using var scope2 = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Assert
		scope1.TransactionId.ShouldNotBe(scope2.TransactionId);
	}

	[Fact]
	public async Task ThrowWhenEnlistingNullProvider()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			sut.EnlistProviderAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenEnlistingNullConnection()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			sut.EnlistConnectionAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenEnlistingNonSqlConnection()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);
		var connection = A.Fake<IDbConnection>();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.EnlistConnectionAsync(connection, CancellationToken.None));
	}

	[Fact]
	public async Task EnlistProviderSuccessfully()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);
		var provider = A.Fake<IPersistenceProvider>();
		A.CallTo(() => provider.Name).Returns("TestProvider");

		// Act
		await sut.EnlistProviderAsync(provider, CancellationToken.None);

		// Assert
		sut.GetEnlistedProviders().ShouldContain(provider);
	}

	[Fact]
	public async Task NotDuplicateEnlistedProvider()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);
		var provider = A.Fake<IPersistenceProvider>();
		A.CallTo(() => provider.Name).Returns("TestProvider");

		// Act
		await sut.EnlistProviderAsync(provider, CancellationToken.None);
		await sut.EnlistProviderAsync(provider, CancellationToken.None);

		// Assert
		sut.GetEnlistedProviders().Count().ShouldBe(1);
	}

	[Fact]
	public void RegisterOnCommitCallback()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert - should not throw
		sut.OnCommit(() => Task.CompletedTask);
	}

	[Fact]
	public void ThrowWhenOnCommitCallbackIsNull()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.OnCommit(null!));
	}

	[Fact]
	public void RegisterOnRollbackCallback()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert - should not throw
		sut.OnRollback(() => Task.CompletedTask);
	}

	[Fact]
	public void ThrowWhenOnRollbackCallbackIsNull()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.OnRollback(null!));
	}

	[Fact]
	public void RegisterOnCompleteCallback()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert - should not throw
		sut.OnComplete(_ => Task.CompletedTask);
	}

	[Fact]
	public void ThrowWhenOnCompleteCallbackIsNull()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.OnComplete(null!));
	}

	[Fact]
	public void CreateNestedScope()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act
		using var nested = sut.CreateNestedScope(IsolationLevel.Serializable);

		// Assert
		nested.ShouldNotBeNull();
		nested.TransactionId.ShouldNotBe(sut.TransactionId);
	}

	[Fact]
	public async Task ThrowOnOperationsAfterDisposal()
	{
		// Arrange
		var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);
		sut.Dispose();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			sut.EnlistProviderAsync(A.Fake<IPersistenceProvider>(), CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnCreateSavepointWithEmptyName()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(() =>
			sut.CreateSavepointAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnReleaseSavepointWithEmptyName()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(() =>
			sut.ReleaseSavepointAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnRollbackToSavepointWithEmptyName()
	{
		// Arrange
		using var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(() =>
			sut.RollbackToSavepointAsync("", CancellationToken.None));
	}

	[Fact]
	public void SupportDoubleDispose()
	{
		// Arrange
		var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert - should not throw
		sut.Dispose();
		sut.Dispose();
	}

	[Fact]
	public async Task SupportDoubleDisposeAsync()
	{
		// Arrange
		var sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		// Act & Assert - should not throw
		await sut.DisposeAsync();
		await sut.DisposeAsync();
	}
}
