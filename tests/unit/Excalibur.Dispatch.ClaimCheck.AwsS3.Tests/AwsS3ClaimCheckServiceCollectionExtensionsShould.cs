using Excalibur.Dispatch.ClaimCheck.AwsS3;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.ClaimCheck.AwsS3.Tests;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Patterns)]
public sealed class AwsS3ClaimCheckServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddAwsS3ClaimCheck_ShouldThrowForNullArguments()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			AwsS3ClaimCheckServiceCollectionExtensions.AddAwsS3ClaimCheck(
				null!,
				_ => { }));

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsS3ClaimCheck(null!));
	}

	[Fact]
	public void AddAwsS3ClaimCheck_ShouldRegisterProviderAndConfigureOptions()
	{
		var services = new ServiceCollection();

		_ = services.AddAwsS3ClaimCheck(
			configure: options =>
			{
				options.BucketName = "dispatch-bucket";
				options.Prefix = "tenant-a/";
			},
			configureClaimCheck: options =>
			{
				options.IdPrefix = "ref-";
				options.PayloadThreshold = 4096;
			});

		var providerDescriptor = services.SingleOrDefault(descriptor =>
			descriptor.ServiceType == typeof(IClaimCheckProvider));

		providerDescriptor.ShouldNotBeNull();
		providerDescriptor.ImplementationType.ShouldBe(typeof(AwsS3ClaimCheckStore));
		providerDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);

		using var provider = services.BuildServiceProvider();

		var awsOptions = provider.GetRequiredService<IOptions<AwsS3ClaimCheckOptions>>().Value;
		var claimCheckOptions = provider.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;

		awsOptions.BucketName.ShouldBe("dispatch-bucket");
		awsOptions.Prefix.ShouldBe("tenant-a/");
		claimCheckOptions.IdPrefix.ShouldBe("ref-");
		claimCheckOptions.PayloadThreshold.ShouldBe(4096);
	}

	[Fact]
	public void AddAwsS3ClaimCheck_WithoutClaimCheckConfigurator_ShouldKeepClaimCheckDefaults()
	{
		var services = new ServiceCollection();

		_ = services.AddAwsS3ClaimCheck(options => options.BucketName = "dispatch-bucket");

		using var provider = services.BuildServiceProvider();
		var claimCheckOptions = provider.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;

		claimCheckOptions.IdPrefix.ShouldBe("cc-");
		claimCheckOptions.PayloadThreshold.ShouldBe(256 * 1024);
	}
}
