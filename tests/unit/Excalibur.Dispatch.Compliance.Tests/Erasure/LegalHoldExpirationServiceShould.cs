using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class LegalHoldExpirationServiceShould
{
	[Fact]
	public void Throw_for_null_scope_factory()
	{
		Should.Throw<ArgumentNullException>(() =>
			new LegalHoldExpirationService(
				null!,
				Microsoft.Extensions.Options.Options.Create(new LegalHoldExpirationOptions()),
				NullLogger<LegalHoldExpirationService>.Instance));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();

		Should.Throw<ArgumentNullException>(() =>
			new LegalHoldExpirationService(
				scopeFactory,
				null!,
				NullLogger<LegalHoldExpirationService>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();

		Should.Throw<ArgumentNullException>(() =>
			new LegalHoldExpirationService(
				scopeFactory,
				Microsoft.Extensions.Options.Options.Create(new LegalHoldExpirationOptions()),
				null!));
	}

	[Fact]
	public async Task Exit_immediately_when_disabled()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();
		var options = new LegalHoldExpirationOptions { Enabled = false };
		var sut = new LegalHoldExpirationService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<LegalHoldExpirationService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// No scope should be created when disabled
		A.CallTo(() => scopeFactory.CreateScope()).MustNotHaveHappened();
	}

	[Fact]
	public async Task Process_expired_holds()
	{
		var holdStore = A.Fake<ILegalHoldStore>();
		var queryStore = A.Fake<ILegalHoldQueryStore>();

		A.CallTo(() => holdStore.GetService(typeof(ILegalHoldQueryStore)))
			.Returns(queryStore);

		var expiredHold = CreateLegalHold(isActive: true, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

		A.CallTo(() => queryStore.GetExpiredHoldsAsync(A<CancellationToken>._))
			.Returns(new List<LegalHold> { expiredHold });

		A.CallTo(() => holdStore.UpdateHoldAsync(A<LegalHold>._, A<CancellationToken>._))
			.Returns(true);

		var (scopeFactory, _) = SetupScopeFactory(holdStore);

		var options = new LegalHoldExpirationOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		};

		var sut = new LegalHoldExpirationService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<LegalHoldExpirationService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(300), CancellationToken.None).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => holdStore.UpdateHoldAsync(
			A<LegalHold>.That.Matches(h => !h.IsActive && h.ReleasedBy!.Contains("auto-expiration")),
			A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Skip_processing_when_no_expired_holds()
	{
		var holdStore = A.Fake<ILegalHoldStore>();
		var queryStore = A.Fake<ILegalHoldQueryStore>();

		A.CallTo(() => holdStore.GetService(typeof(ILegalHoldQueryStore)))
			.Returns(queryStore);

		A.CallTo(() => queryStore.GetExpiredHoldsAsync(A<CancellationToken>._))
			.Returns(new List<LegalHold>());

		var (scopeFactory, _) = SetupScopeFactory(holdStore);

		var options = new LegalHoldExpirationOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		};

		var sut = new LegalHoldExpirationService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<LegalHoldExpirationService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(300), CancellationToken.None).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => holdStore.UpdateHoldAsync(A<LegalHold>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void Have_default_options()
	{
		var options = new LegalHoldExpirationOptions();

		options.PollingInterval.ShouldBe(TimeSpan.FromHours(1));
		options.Enabled.ShouldBeTrue();
	}

	private static LegalHold CreateLegalHold(
		bool isActive = true,
		DateTimeOffset? expiresAt = null) =>
		new()
		{
			HoldId = Guid.NewGuid(),
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test legal hold",
			IsActive = isActive,
			CreatedBy = "test-user",
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
			DataSubjectIdHash = "hash-1",
			ExpiresAt = expiresAt
		};

	private static (IServiceScopeFactory, IServiceScope) SetupScopeFactory(ILegalHoldStore holdStore)
	{
		var serviceProvider = A.Fake<IServiceProvider>();
		A.CallTo(() => serviceProvider.GetService(typeof(ILegalHoldStore)))
			.Returns(holdStore);

		var scope = A.Fake<IServiceScope>();
		A.CallTo(() => scope.ServiceProvider).Returns(serviceProvider);

		var scopeFactory = A.Fake<IServiceScopeFactory>();
		A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);

		return (scopeFactory, scope);
	}
}
