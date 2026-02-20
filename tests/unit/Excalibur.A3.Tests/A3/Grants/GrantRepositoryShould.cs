// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.A3.Authorization.Grants;
using Excalibur.Data;
using Excalibur.Data.Abstractions;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.A3.Grants;

/// <summary>
/// Unit tests for <see cref="GrantRepository"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class GrantRepositoryShould
{
	private readonly IDomainDb _domainDb = A.Fake<IDomainDb>();
	private readonly IGrantRequestProvider _requestProvider = A.Fake<IGrantRequestProvider>();
	private readonly GrantRepository _repository;

	public GrantRepositoryShould()
	{
		A.CallTo(() => _domainDb.Connection).Returns(A.Fake<IDbConnection>());
		_repository = new GrantRepository(
			_domainDb,
			_requestProvider,
			NullLogger<GrantRepository>.Instance);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenDomainDbIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrantRepository(null!, _requestProvider, NullLogger<GrantRepository>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenRequestProviderIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrantRepository(_domainDb, null!, NullLogger<GrantRepository>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrantRepository(_domainDb, _requestProvider, null!));
	}

	[Fact]
	public async Task GetByIdAsync_ThrowsOnNullId()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_repository.GetByIdAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_ThrowsOnEmptyId()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_repository.GetByIdAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowsOnNullAggregate()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_repository.SaveAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExistsAsync_ThrowsOnNullId()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_repository.ExistsAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExistsAsync_ThrowsOnEmptyId()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_repository.ExistsAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowsOnNullAggregate()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_repository.DeleteAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task QueryAsync_ThrowsOnNullQuery()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_repository.QueryAsync<GrantQuery>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task QueryAsync_ReturnsEmptyCollection()
	{
		// Arrange
		var query = new GrantQuery();

		// Act
		var result = await _repository.QueryAsync(query, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task FindAsync_ThrowsOnNullQuery()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_repository.FindAsync<GrantQuery>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task FindAsync_ReturnsNull()
	{
		// Arrange
		var query = new GrantQuery();

		// Act
		var result = await _repository.FindAsync(query, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task MatchingAsync_ThrowsOnNullScope()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_repository.MatchingAsync(null!, "user-1"));
	}

	[Fact]
	public async Task MatchingAsync_ThrowsOnNullUserId()
	{
		// Arrange
		var scope = new GrantScope("tenant", "type", "qualifier");

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(() =>
			_repository.MatchingAsync(scope, null));
	}

	[Fact]
	public async Task ReadAllAsync_ThrowsOnNullUserId()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_repository.ReadAllAsync(null!));
	}

	[Fact]
	public async Task ReadAllAsync_ThrowsOnEmptyUserId()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_repository.ReadAllAsync(""));
	}
}
