using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureSchedulerOptionsShould
{
	[Fact]
	public void Have_default_polling_interval_of_five_minutes()
	{
		var options = new ErasureSchedulerOptions();

		options.PollingInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Have_default_batch_size_of_ten()
	{
		var options = new ErasureSchedulerOptions();

		options.BatchSize.ShouldBe(10);
	}

	[Fact]
	public void Be_enabled_by_default()
	{
		var options = new ErasureSchedulerOptions();

		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Have_default_max_retry_attempts_of_three()
	{
		var options = new ErasureSchedulerOptions();

		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void Have_default_retry_delay_base_of_thirty_seconds()
	{
		var options = new ErasureSchedulerOptions();

		options.RetryDelayBase.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Use_exponential_backoff_by_default()
	{
		var options = new ErasureSchedulerOptions();

		options.UseExponentialBackoff.ShouldBeTrue();
	}

	[Fact]
	public void Have_default_certificate_cleanup_interval_of_24_hours()
	{
		var options = new ErasureSchedulerOptions();

		options.CertificateCleanupInterval.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void Allow_setting_custom_values()
	{
		var options = new ErasureSchedulerOptions
		{
			PollingInterval = TimeSpan.FromMinutes(10),
			BatchSize = 50,
			Enabled = false,
			MaxRetryAttempts = 5,
			RetryDelayBase = TimeSpan.FromMinutes(1),
			UseExponentialBackoff = false,
			CertificateCleanupInterval = TimeSpan.FromHours(12)
		};

		options.PollingInterval.ShouldBe(TimeSpan.FromMinutes(10));
		options.BatchSize.ShouldBe(50);
		options.Enabled.ShouldBeFalse();
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryDelayBase.ShouldBe(TimeSpan.FromMinutes(1));
		options.UseExponentialBackoff.ShouldBeFalse();
		options.CertificateCleanupInterval.ShouldBe(TimeSpan.FromHours(12));
	}
}
