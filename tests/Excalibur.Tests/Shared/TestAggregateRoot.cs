using Excalibur.Core.Domain.Events;
using Excalibur.Domain.Model;
using Excalibur.Domain.Repositories;

namespace Excalibur.Tests.Shared;

public sealed class TestAggregateRoot(string key) : IAggregateRoot<string>
{
	private readonly List<IDomainEvent> _domainEvents = new();

	public string Key { get; } = key;

	public string ETag { get; set; } = "default-etag";

	public IEnumerable<IDomainEvent> DomainEvents => _domainEvents;
}

public sealed class TestIntKeyAggregateRoot(int key) : IAggregateRoot<int>
{
	private readonly List<IDomainEvent> _domainEvents = new();

	public int Key { get; } = key;

	public string ETag { get; set; } = "default-etag";

	public IEnumerable<IDomainEvent> DomainEvents => _domainEvents;
}

public sealed class TestGuidKeyAggregateRoot(Guid key) : IAggregateRoot<Guid>
{
	private readonly List<IDomainEvent> _domainEvents = new();

	public Guid Key { get; } = key;

	public string ETag { get; set; } = "default-etag";

	public IEnumerable<IDomainEvent> DomainEvents => _domainEvents;
}

public sealed class TestAggregateQuery : IAggregateQuery<TestAggregateRoot>
{
}

public sealed class TestAggregateRepository : IAggregateRepository<TestAggregateRoot, string>
{
	public Task<int> Delete(TestAggregateRoot aggregate, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(1);
	}

	public Task<bool> Exists(string key, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<TestAggregateRoot> Read(string key, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(new TestAggregateRoot("test-id"));
	}

	public Task<int> Save(TestAggregateRoot aggregate, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(1);
	}

	public Task<IEnumerable<TestAggregateRoot>> Query<TQuery>(TQuery query, CancellationToken cancellationToken = default)
		where TQuery : IAggregateQuery<TestAggregateRoot>
	{
		return Task.FromResult(Enumerable.Empty<TestAggregateRoot>());
	}

	public Task<TestAggregateRoot?> FindAsync<TQuery>(TQuery query, CancellationToken cancellationToken = default)
		where TQuery : IAggregateQuery<TestAggregateRoot>
	{
		return Task.FromResult<TestAggregateRoot?>(null);
	}

#pragma warning disable IDE0060 // Remove unused parameter

	public Task<TestAggregateRoot?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
	{
		return Task.FromResult<TestAggregateRoot?>(null);
	}

#pragma warning disable IDE0060 // Remove unused parameter

	public Task SaveAsync(TestAggregateRoot entity, CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
	{
		return Task.CompletedTask;
	}

#pragma warning disable IDE0060 // Remove unused parameter

	public Task DeleteAsync(TestAggregateRoot entity, CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
	{
		return Task.CompletedTask;
	}
}
