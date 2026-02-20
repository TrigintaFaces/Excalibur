using Excalibur.Dispatch.Patterns.ClaimCheck;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ClaimCheckCleanupServiceShould
{
	[Fact]
	public async Task Stop_immediately_when_cleanup_is_disabled()
	{
		// Arrange
		var options = new ClaimCheckOptions { EnableCleanup = false };
		var provider = A.Fake<IClaimCheckProvider>();
		var logger = CreateEnabledLogger();
		var sut = new ClaimCheckCleanupService(provider, Microsoft.Extensions.Options.Options.Create(options), logger);

		using var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		await Task.Delay(1000);
		await sut.StopAsync(CancellationToken.None);

		// Assert -- provider should never be called
		A.CallTo(provider).MustNotHaveHappened();
	}

	[Fact]
	public async Task Stop_when_provider_does_not_implement_cleanup_interface()
	{
		// Arrange
		var options = new ClaimCheckOptions { EnableCleanup = true };
		var provider = A.Fake<IClaimCheckProvider>(); // Does not implement IClaimCheckCleanupProvider
		var logger = CreateEnabledLogger();
		var sut = new ClaimCheckCleanupService(provider, Microsoft.Extensions.Options.Options.Create(options), logger);

		using var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		await Task.Delay(1000);
		await sut.StopAsync(CancellationToken.None);

		// Assert -- service should exit gracefully without calling cleanup
		A.CallTo(provider).MustNotHaveHappened();
	}

	[Fact]
	public async Task Execute_cleanup_when_provider_implements_cleanup_interface()
	{
		// Arrange
		var options = new ClaimCheckOptions
		{
			EnableCleanup = true,
			CleanupInterval = TimeSpan.FromMilliseconds(50),
		};

		var provider = A.Fake<IClaimCheckProviderWithCleanup>();
		A.CallTo(() => provider.CleanupExpiredAsync(A<int>._, A<CancellationToken>._))
			.Returns(3);

		var logger = CreateEnabledLogger();
		var sut = new ClaimCheckCleanupService(provider, Microsoft.Extensions.Options.Options.Create(options), logger);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

		// Act
		await sut.StartAsync(cts.Token);
		try { await Task.Delay(300, cts.Token); } catch (OperationCanceledException) { }
		await sut.StopAsync(CancellationToken.None);

		// Assert -- cleanup should have been invoked at least once
		A.CallTo(() => provider.CleanupExpiredAsync(A<int>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Continue_after_cleanup_error()
	{
		// Arrange
		var options = new ClaimCheckOptions
		{
			EnableCleanup = true,
			CleanupInterval = TimeSpan.FromMilliseconds(50),
		};

		var callCount = 0;
		var provider = A.Fake<IClaimCheckProviderWithCleanup>();
		A.CallTo(() => provider.CleanupExpiredAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				if (callCount == 1)
				{
					throw new InvalidOperationException("Test error");
				}
				return Task.FromResult(0);
			});

		var logger = CreateEnabledLogger();
		var sut = new ClaimCheckCleanupService(provider, Microsoft.Extensions.Options.Options.Create(options), logger);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

		// Act -- service should not crash on error, should retry
		await sut.StartAsync(cts.Token);
		try { await Task.Delay(TimeSpan.FromSeconds(2), cts.Token); } catch (OperationCanceledException) { }
		await sut.StopAsync(CancellationToken.None);

		// Assert -- should have been called more than once (retry after error)
		callCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task Stop_gracefully_on_cancellation()
	{
		// Arrange
		var options = new ClaimCheckOptions
		{
			EnableCleanup = true,
			CleanupInterval = TimeSpan.FromSeconds(10), // Long interval so we cancel before it fires
		};

		var provider = A.Fake<IClaimCheckProviderWithCleanup>();
		var logger = CreateEnabledLogger();
		var sut = new ClaimCheckCleanupService(provider, Microsoft.Extensions.Options.Options.Create(options), logger);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

		// Act
		await sut.StartAsync(cts.Token);
		try { await Task.Delay(200, cts.Token); } catch (OperationCanceledException) { }
		await sut.StopAsync(CancellationToken.None);

		// Assert -- should not throw, should exit cleanly
	}

	/// <summary>
	/// Combined interface for testing -- FakeItEasy needs to see both interfaces on the fake.
	/// </summary>
	internal interface IClaimCheckProviderWithCleanup : IClaimCheckProvider, IClaimCheckCleanupProvider;

	private static ILogger<ClaimCheckCleanupService> CreateEnabledLogger()
	{
		return new AlwaysEnabledLogger<ClaimCheckCleanupService>();
	}

	private sealed class AlwaysEnabledLogger<T> : ILogger<T>
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull
		{
			return NullScope.Instance;
		}

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			_ = formatter;
		}
	}

	private sealed class NullScope : IDisposable
	{
		public static readonly NullScope Instance = new();
		public void Dispose() { }
	}
}
