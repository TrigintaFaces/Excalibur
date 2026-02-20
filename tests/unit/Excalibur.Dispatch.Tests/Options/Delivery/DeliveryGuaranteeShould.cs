using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeliveryGuaranteeShould
{
    [Theory]
    [InlineData(DeliveryGuarantee.AtMostOnce, 0)]
    [InlineData(DeliveryGuarantee.AtLeastOnce, 1)]
    public void HaveCorrectEnumValues(DeliveryGuarantee guarantee, int expected)
    {
        ((int)guarantee).ShouldBe(expected);
    }

    [Fact]
    public void HaveAllValues()
    {
        var values = Enum.GetValues<DeliveryGuarantee>();
        values.Length.ShouldBe(2);
    }

    [Fact]
    public void HaveCorrectDefaultsForOptions()
    {
        var options = new DeliveryGuaranteeOptions();

        options.Guarantee.ShouldBe(DeliveryGuarantee.AtLeastOnce);
        options.EnableIdempotencyTracking.ShouldBeTrue();
        options.IdempotencyKeyRetention.ShouldBe(TimeSpan.FromDays(7));
        options.EnableAutomaticRetry.ShouldBeTrue();
    }

    [Fact]
    public void AllowSettingOptionsProperties()
    {
        var options = new DeliveryGuaranteeOptions
        {
            Guarantee = DeliveryGuarantee.AtMostOnce,
            EnableIdempotencyTracking = false,
            IdempotencyKeyRetention = TimeSpan.FromHours(1),
            EnableAutomaticRetry = false,
        };

        options.Guarantee.ShouldBe(DeliveryGuarantee.AtMostOnce);
        options.EnableIdempotencyTracking.ShouldBeFalse();
        options.IdempotencyKeyRetention.ShouldBe(TimeSpan.FromHours(1));
        options.EnableAutomaticRetry.ShouldBeFalse();
    }
}
