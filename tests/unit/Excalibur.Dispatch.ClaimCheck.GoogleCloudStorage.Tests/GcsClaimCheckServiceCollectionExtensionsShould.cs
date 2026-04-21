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
			services.AddGcsClaimCheck((Action<IClaimCheckGcsBuilder>)null!));
	}

	[Fact]
	public void AddGcsClaimCheck_ShouldRegisterProviderAndConfigureOptions()
	{
		var services = new ServiceCollection();

		_ = services.AddGcsClaimCheck(gcs =>
		{
			gcs.BucketName("dispatch-bucket")
			   .Prefix("tenant-b/");
		});

		var providerDescriptor = services.SingleOrDefault(descriptor =>
			descriptor.ServiceType == typeof(IClaimCheckProvider));

		providerDescriptor.ShouldNotBeNull();
		providerDescriptor.ImplementationType.ShouldBe(typeof(GcsClaimCheckStore));
		providerDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);

		using var provider = services.BuildServiceProvider();

		var gcsOptions = provider.GetRequiredService<IOptions<GcsClaimCheckOptions>>().Value;

		gcsOptions.BucketName.ShouldBe("dispatch-bucket");
		gcsOptions.Prefix.ShouldBe("tenant-b/");
	}

	[Fact]
	public void AddGcsClaimCheck_WithBucketOnly_ShouldKeepClaimCheckDefaults()
	{
		var services = new ServiceCollection();

		_ = services.AddGcsClaimCheck(gcs => gcs.BucketName("dispatch-bucket"));

		using var provider = services.BuildServiceProvider();
		var claimCheckOptions = provider.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;

		claimCheckOptions.IdPrefix.ShouldBe("cc-");
		claimCheckOptions.PayloadThreshold.ShouldBe(256 * 1024);
	}
}
