using Excalibur.Jobs;

using FakeItEasy;

using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Excalibur.Tests.Unit.Jobs;

public class IJobConfigHostedWatcherServiceShould
{
	[Fact]
	public void ImplementIHostedServiceInterface()
	{
		// Arrange
		var service = A.Fake<IJobConfigHostedWatcherService>();

		// Act & Assert
		_ = service.ShouldBeAssignableTo<IHostedService>();
	}

	[Fact]
	public void ImplementIDisposableInterface()
	{
		// Arrange
		var service = A.Fake<IJobConfigHostedWatcherService>();

		// Act & Assert
		_ = service.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public async Task SupportStartAsyncMethod()
	{
		// Arrange
		var service = A.Fake<IJobConfigHostedWatcherService>();
		_ = A.CallTo(() => service.StartAsync(A<CancellationToken>._)).Returns(Task.CompletedTask);

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => service.StartAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SupportStopAsyncMethod()
	{
		// Arrange
		var service = A.Fake<IJobConfigHostedWatcherService>();
		_ = A.CallTo(() => service.StopAsync(A<CancellationToken>._)).Returns(Task.CompletedTask);

		// Act
		await service.StopAsync(CancellationToken.None).ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => service.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void SupportDisposeMethod()
	{
		// Arrange
		var service = A.Fake<IJobConfigHostedWatcherService>();

		// Act
		service.Dispose();

		// Assert
		_ = A.CallTo(() => service.Dispose()).MustHaveHappenedOnceExactly();
	}
}
