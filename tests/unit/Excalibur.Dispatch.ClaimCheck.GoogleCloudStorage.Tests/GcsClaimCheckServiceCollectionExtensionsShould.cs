using Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage.Tests;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Patterns)]
public sealed class GcsClaimCheckServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddGcsClaimCheck_ShouldThrowForNullArguments()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			GcsClaimCheckServiceCollectionExtensions.AddGcsClaimCheck(
				null!,
				_ => { }));

		Should.Throw<ArgumentNullException>(() =>
			services.AddGcsClaimCheck(null!));
	}

	[Fact]
	public void AddGcsClaimCheck_ShouldRegisterProviderAndConfigureOptions()
	{
		var services = new ServiceCollection();

		_ = services.AddGcsClaimCheck(
			configure: options =>
			{
				options.BucketName = "dispatch-bucket";
				options.Prefix = "tenant-b/";
			},
			configureClaimCheck: options =>
			{
				options.IdPrefix = "gcs-";
				options.PayloadThreshold = 8192;
			});

		var providerDescriptor = services.SingleOrDefault(descriptor =>
			descriptor.ServiceType == typeof(IClaimCheckProvider));

		providerDescriptor.ShouldNotBeNull();
		providerDescriptor.ImplementationType.ShouldBe(typeof(GcsClaimCheckStore));
		providerDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);

		using var provider = services.BuildServiceProvider();

		var gcsOptions = provider.GetRequiredService<IOptions<GcsClaimCheckOptions>>().Value;
		var claimCheckOptions = provider.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;

		gcsOptions.BucketName.ShouldBe("dispatch-bucket");
		gcsOptions.Prefix.ShouldBe("tenant-b/");
		claimCheckOptions.IdPrefix.ShouldBe("gcs-");
		claimCheckOptions.PayloadThreshold.ShouldBe(8192);
	}

	[Fact]
	public void AddGcsClaimCheck_WithoutClaimCheckConfigurator_ShouldKeepClaimCheckDefaults()
	{
		var services = new ServiceCollection();

		_ = services.AddGcsClaimCheck(options => options.BucketName = "dispatch-bucket");

		using var provider = services.BuildServiceProvider();
		var claimCheckOptions = provider.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;

		claimCheckOptions.IdPrefix.ShouldBe("cc-");
		claimCheckOptions.PayloadThreshold.ShouldBe(256 * 1024);
	}
}
