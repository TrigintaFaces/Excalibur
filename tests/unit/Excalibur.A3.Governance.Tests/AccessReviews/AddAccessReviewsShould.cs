// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Governance;
using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Stores.InMemory;

using Microsoft.Extensions.Hosting;

namespace Excalibur.A3.Governance.Tests.AccessReviews;

/// <summary>
/// Unit tests for <see cref="AccessReviewGovernanceBuilderExtensions.AddAccessReviews"/>
/// DI registration, options configuration, and ValidateOnStart behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AddAccessReviewsShould : UnitTestBase
{
	private static ServiceProvider BuildProvider(Action<AccessReviewOptions>? configure = null)
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddAccessReviews(configure));
		return services.BuildServiceProvider();
	}

	#region Store Registration

	[Fact]
	public void RegisterInMemoryAccessReviewStore_AsFallback()
	{
		using var provider = BuildProvider();
		provider.GetService<IAccessReviewStore>().ShouldBeOfType<InMemoryAccessReviewStore>();
	}

	[Fact]
	public void PreserveExistingStore_WhenRegisteredBefore()
	{
		var services = new ServiceCollection();
		services.AddSingleton<IAccessReviewStore, StubAccessReviewStore>();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddAccessReviews());

		using var provider = services.BuildServiceProvider();
		provider.GetService<IAccessReviewStore>().ShouldBeOfType<StubAccessReviewStore>();
	}

	#endregion

	#region Options Configuration

	[Fact]
	public void UseDefaultOptions_WhenNoConfigureDelegate()
	{
		using var provider = BuildProvider();
		var options = provider.GetRequiredService<IOptions<AccessReviewOptions>>().Value;

		options.DefaultCampaignDuration.ShouldBe(TimeSpan.FromDays(30));
		options.DefaultExpiryPolicy.ShouldBe(AccessReviewExpiryPolicy.NotifyAndExtend);
		options.ExpiryCheckInterval.ShouldBe(TimeSpan.FromHours(1));
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(5));
		options.AutoStartOnCreation.ShouldBeFalse();
	}

	[Fact]
	public void ApplyCustomOptions()
	{
		using var provider = BuildProvider(opts =>
		{
			opts.DefaultCampaignDuration = TimeSpan.FromDays(14);
			opts.DefaultExpiryPolicy = AccessReviewExpiryPolicy.RevokeUnreviewed;
			opts.ExpiryCheckInterval = TimeSpan.FromMinutes(30);
			opts.MaxRetryAttempts = 5;
			opts.RetryBaseDelay = TimeSpan.FromSeconds(10);
			opts.AutoStartOnCreation = true;
		});

		var options = provider.GetRequiredService<IOptions<AccessReviewOptions>>().Value;
		options.DefaultCampaignDuration.ShouldBe(TimeSpan.FromDays(14));
		options.DefaultExpiryPolicy.ShouldBe(AccessReviewExpiryPolicy.RevokeUnreviewed);
		options.ExpiryCheckInterval.ShouldBe(TimeSpan.FromMinutes(30));
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(10));
		options.AutoStartOnCreation.ShouldBeTrue();
	}

	#endregion

	#region ValidateOnStart

	[Fact]
	public void ThrowOnStart_WhenDurationOutOfRange()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddAccessReviews(opts =>
			{
				opts.DefaultCampaignDuration = TimeSpan.FromSeconds(1); // Below 1 minute minimum
			}));

		using var provider = services.BuildServiceProvider();

		// ValidateOnStart fires when IOptions<T> is validated eagerly
		Should.Throw<OptionsValidationException>(() =>
			provider.GetRequiredService<IOptions<AccessReviewOptions>>().Value);
	}

	[Fact]
	public void ThrowOnStart_WhenRetryAttemptsOutOfRange()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddAccessReviews(opts =>
			{
				opts.MaxRetryAttempts = 0; // Below minimum of 1
			}));

		using var provider = services.BuildServiceProvider();

		Should.Throw<OptionsValidationException>(() =>
			provider.GetRequiredService<IOptions<AccessReviewOptions>>().Value);
	}

	[Fact]
	public void ThrowOnStart_WhenExpiryCheckIntervalOutOfRange()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddAccessReviews(opts =>
			{
				opts.ExpiryCheckInterval = TimeSpan.FromSeconds(1); // Below 10 seconds minimum
			}));

		using var provider = services.BuildServiceProvider();

		Should.Throw<OptionsValidationException>(() =>
			provider.GetRequiredService<IOptions<AccessReviewOptions>>().Value);
	}

	[Fact]
	public void ThrowOnStart_WhenRetryBaseDelayOutOfRange()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddAccessReviews(opts =>
			{
				opts.RetryBaseDelay = TimeSpan.FromMinutes(10); // Above 5 minutes maximum
			}));

		using var provider = services.BuildServiceProvider();

		Should.Throw<OptionsValidationException>(() =>
			provider.GetRequiredService<IOptions<AccessReviewOptions>>().Value);
	}

	#endregion

	#region Background Service Registration

	[Fact]
	public void RegisterExpiryBackgroundService()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddAccessReviews());

		// Verify the descriptor is registered (resolving would require ILogger<T> + IServiceScopeFactory)
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IHostedService) &&
			sd.ImplementationType != null &&
			sd.ImplementationType.Name == "AccessReviewExpiryService");
	}

	#endregion

	#region Notifier Registration

	[Fact]
	public void RegisterNullNotifier_AsFallback()
	{
		using var provider = BuildProvider();
		var notifier = provider.GetService<IAccessReviewNotifier>();
		notifier.ShouldNotBeNull();
		notifier.GetType().Name.ShouldBe("NullAccessReviewNotifier");
	}

	#endregion

	#region Fluent Chaining

	[Fact]
	public void ReturnIGovernanceBuilder_ForFluentChaining()
	{
		var services = new ServiceCollection();
		IGovernanceBuilder? capturedBuilder = null;

		services.AddExcaliburA3Core()
			.AddGovernance(g =>
			{
				var result = g.AddAccessReviews();
				capturedBuilder = result;
			});

		capturedBuilder.ShouldNotBeNull();
		capturedBuilder.ShouldBeAssignableTo<IGovernanceBuilder>();
	}

	[Fact]
	public void ThrowOnNullBuilder()
	{
		IGovernanceBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddAccessReviews());
	}

	#endregion

	#region Test Doubles

	private sealed class StubAccessReviewStore : IAccessReviewStore
	{
		public Task<AccessReviewCampaignSummary?> GetCampaignAsync(string campaignId, CancellationToken cancellationToken) =>
			Task.FromResult<AccessReviewCampaignSummary?>(null);

		public Task SaveCampaignAsync(AccessReviewCampaignSummary campaign, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<IReadOnlyList<AccessReviewCampaignSummary>> GetCampaignsByStateAsync(AccessReviewState? state, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<AccessReviewCampaignSummary>>(Array.Empty<AccessReviewCampaignSummary>());

		public Task<bool> DeleteCampaignAsync(string campaignId, CancellationToken cancellationToken) =>
			Task.FromResult(false);

		public object? GetService(Type serviceType) => null;
	}

	#endregion
}
