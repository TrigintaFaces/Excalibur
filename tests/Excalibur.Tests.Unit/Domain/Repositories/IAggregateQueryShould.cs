using Excalibur.Core.Domain.Events;
using Excalibur.Domain.Model;
using Excalibur.Domain.Repositories;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain.Repositories;

public class IAggregateQueryShould
{
	[Fact]
	public void BeUsableAsGenericConstraint()
	{
		// Arrange & Act
		var query = new TestQuery();

		// Act & Assert
		_ = query.ShouldBeAssignableTo<IAggregateQuery<TestAggregateRoot>>();
	}

	[Fact]
	public void SupportDifferentAggregateTypes()
	{
		// Arrange & Act
		var stringKeyQuery = new TestQuery();
		var intKeyQuery = new TestIntKeyQuery();
		var guidKeyQuery = new TestGuidKeyQuery();

		// Assert
		_ = stringKeyQuery.ShouldBeAssignableTo<IAggregateQuery<TestAggregateRoot>>();
		_ = intKeyQuery.ShouldBeAssignableTo<IAggregateQuery<TestIntKeyAggregateRoot>>();
		_ = guidKeyQuery.ShouldBeAssignableTo<IAggregateQuery<TestGuidKeyAggregateRoot>>();
	}

	[Fact]
	public void AllowExtendingWithAdditionalProperties()
	{
		// Arrange
		var name = "Test Name";
		var status = "Active";

		// Act
		var query = new ExtendedQuery(name, status);

		// Assert
		_ = query.ShouldBeAssignableTo<IAggregateQuery<TestAggregateRoot>>();
		query.Name.ShouldBe(name);
		query.Status.ShouldBe(status);
	}

	private sealed class TestAggregateRoot(string key) : IAggregateRoot<string>
	{
		private readonly List<IDomainEvent> _domainEvents = new();

		public string Key { get; } = key;

		public string ETag { get; set; } = "default-etag";

		public IEnumerable<IDomainEvent> DomainEvents => _domainEvents;
	}

	private sealed class TestIntKeyAggregateRoot(int key) : IAggregateRoot<int>
	{
		private readonly List<IDomainEvent> _domainEvents = new();

		public int Key { get; } = key;

		public string ETag { get; set; } = "default-etag";

		public IEnumerable<IDomainEvent> DomainEvents => _domainEvents;
	}

	private sealed class TestGuidKeyAggregateRoot(Guid key) : IAggregateRoot<Guid>
	{
		private readonly List<IDomainEvent> _domainEvents = new();

		public Guid Key { get; } = key;

		public string ETag { get; set; } = "default-etag";

		public IEnumerable<IDomainEvent> DomainEvents => _domainEvents;
	}

	private sealed class TestQuery : IAggregateQuery<TestAggregateRoot>
	{
	}

	private sealed class TestIntKeyQuery : IAggregateQuery<TestIntKeyAggregateRoot>
	{
	}

	private sealed class TestGuidKeyQuery : IAggregateQuery<TestGuidKeyAggregateRoot>
	{
	}

	private sealed class ExtendedQuery(string name, string status) : IAggregateQuery<TestAggregateRoot>
	{
		public string Name { get; } = name;
		public string Status { get; } = status;
	}
}
