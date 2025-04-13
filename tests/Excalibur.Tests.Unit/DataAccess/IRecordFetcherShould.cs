using Excalibur.DataAccess;
using Excalibur.Tests.Shared;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class IRecordFetcherShould
{
	[Fact]
	public async Task FetchBatchAsyncShouldRetrieveRecords()
	{
		// Arrange
		var fetcher = A.Fake<IRecordFetcher<string>>();
		var expectedRecords = new List<string> { "Record1", "Record2", "Record3" };
		_ = A.CallTo(() => fetcher.FetchBatchAsync(A<long>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<string>>(expectedRecords));

		// Act
		var result = await fetcher.FetchBatchAsync(0, 10, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBeEquivalentTo(expectedRecords);
	}

	[Fact]
	public async Task FetchBatchAsyncShouldRespectSkipParameter()
	{
		// Arrange
		var fetcher = A.Fake<IRecordFetcher<string>>();
		long expectedSkip = 5;
		_ = A.CallTo(() => fetcher.FetchBatchAsync(expectedSkip, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<string>>(new List<string>()));

		// Act
		_ = await fetcher.FetchBatchAsync(expectedSkip, 10, CancellationToken.None).ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => fetcher.FetchBatchAsync(expectedSkip, A<int>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FetchBatchAsyncShouldRespectBatchSizeParameter()
	{
		// Arrange
		var fetcher = A.Fake<IRecordFetcher<string>>();
		int expectedBatchSize = 15;
		_ = A.CallTo(() => fetcher.FetchBatchAsync(A<long>._, expectedBatchSize, A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<string>>(new List<string>()));

		// Act
		_ = await fetcher.FetchBatchAsync(0, expectedBatchSize, CancellationToken.None).ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => fetcher.FetchBatchAsync(A<long>._, expectedBatchSize, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FetchBatchAsyncShouldRespectCancellationToken()
	{
		// Arrange
		var fetcher = A.Fake<IRecordFetcher<string>>();
		var cancellationToken = new CancellationToken();
		_ = A.CallTo(() => fetcher.FetchBatchAsync(A<long>._, A<int>._, cancellationToken))
			.Returns(Task.FromResult<IEnumerable<string>>(new List<string>()));

		// Act
		_ = await fetcher.FetchBatchAsync(0, 10, cancellationToken).ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => fetcher.FetchBatchAsync(A<long>._, A<int>._, cancellationToken))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void SupportGenericTypeParameters()
	{
		// Arrange & Act
		var stringFetcher = A.Fake<IRecordFetcher<string>>();
		var intFetcher = A.Fake<IRecordFetcher<int>>();
		var customTypeFetcher = A.Fake<IRecordFetcher<User>>();

		// Assert
		_ = stringFetcher.ShouldBeAssignableTo<IRecordFetcher<string>>();
		_ = intFetcher.ShouldBeAssignableTo<IRecordFetcher<int>>();
		_ = customTypeFetcher.ShouldBeAssignableTo<IRecordFetcher<User>>();
	}
}
