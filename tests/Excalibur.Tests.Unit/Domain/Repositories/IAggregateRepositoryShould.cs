using Excalibur.Domain.Repositories;
using Excalibur.Tests.Shared;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain.Repositories;

public class IAggregateRepositoryShould
{
	[Fact]
	public async Task SupportDeleteOperation()
	{
		// Arrange
		var repository = A.Fake<IAggregateRepository<TestAggregateRoot, string>>();
		var aggregate = new TestAggregateRoot("test-key");
		_ = A.CallTo(() => repository.Delete(aggregate, A<CancellationToken>._)).Returns(Task.FromResult(1));

		// Act
		var result = await repository.Delete(aggregate, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(1);
		_ = A.CallTo(() => repository.Delete(aggregate, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SupportExistsOperation()
	{
		// Arrange
		var repository = A.Fake<IAggregateRepository<TestAggregateRoot, string>>();
		var key = "test-key";
		_ = A.CallTo(() => repository.Exists(key, A<CancellationToken>._)).Returns(Task.FromResult(true));

		// Act
		var result = await repository.Exists(key, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBeTrue();
		_ = A.CallTo(() => repository.Exists(key, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SupportQueryOperation()
	{
		// Arrange
		var repository = A.Fake<IAggregateRepository<TestAggregateRoot, string>>();
		var query = new TestAggregateQuery();
		var expectedResults = new List<TestAggregateRoot> { new("key1"), new("key2") };
		_ = A.CallTo(() => repository.Query(query, A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResults.AsEnumerable()));

		// Act
		var results = await repository.Query(query, CancellationToken.None).ConfigureAwait(true);

		// Assert
		results.ShouldBeEquivalentTo(expectedResults);
		_ = A.CallTo(() => repository.Query(query, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SupportFindAsyncOperation()
	{
		// Arrange
		var repository = A.Fake<IAggregateRepository<TestAggregateRoot, string>>();
		var query = new TestAggregateQuery();
		var expectedResult = new TestAggregateRoot("key1");
		_ = A.CallTo(() => repository.FindAsync(query, A<CancellationToken>._))
			.Returns(Task.FromResult<TestAggregateRoot?>(expectedResult));

		// Act
		var result = await repository.FindAsync(query, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(expectedResult);
		_ = A.CallTo(() => repository.FindAsync(query, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SupportReadOperation()
	{
		// Arrange
		var repository = A.Fake<IAggregateRepository<TestAggregateRoot, string>>();
		var key = "test-key";
		var expectedResult = new TestAggregateRoot(key);
		_ = A.CallTo(() => repository.Read(key, A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResult));

		// Act
		var result = await repository.Read(key, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(expectedResult);
		_ = A.CallTo(() => repository.Read(key, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SupportSaveOperation()
	{
		// Arrange
		var repository = A.Fake<IAggregateRepository<TestAggregateRoot, string>>();
		var aggregate = new TestAggregateRoot("test-key");
		_ = A.CallTo(() => repository.Save(aggregate, A<CancellationToken>._)).Returns(Task.FromResult(1));

		// Act
		var result = await repository.Save(aggregate, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(1);
		_ = A.CallTo(() => repository.Save(aggregate, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void SupportDifferentAggregateTypes()
	{
		// Arrange
		var stringKeyRepo = A.Fake<IAggregateRepository<TestAggregateRoot, string>>();
		var intKeyRepo = A.Fake<IAggregateRepository<TestIntKeyAggregateRoot, int>>();
		var guidKeyRepo = A.Fake<IAggregateRepository<TestGuidKeyAggregateRoot, Guid>>();

		// Act & Assert
		_ = stringKeyRepo.ShouldBeAssignableTo<IAggregateRepository<TestAggregateRoot, string>>();
		_ = intKeyRepo.ShouldBeAssignableTo<IAggregateRepository<TestIntKeyAggregateRoot, int>>();
		_ = guidKeyRepo.ShouldBeAssignableTo<IAggregateRepository<TestGuidKeyAggregateRoot, Guid>>();
	}
}
