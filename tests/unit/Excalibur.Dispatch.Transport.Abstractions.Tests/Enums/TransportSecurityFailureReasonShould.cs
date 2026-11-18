using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class TransportSecurityFailureReasonShould
{
    [Theory]
    [InlineData(TransportSecurityFailureReason.Unspecified, 0)]
    [InlineData(TransportSecurityFailureReason.TlsNotEnabled, 1)]
    [InlineData(TransportSecurityFailureReason.DefaultCredentials, 2)]
    public void Should_Have_Correct_Values(TransportSecurityFailureReason reason, int expected)
    {
        ((int)reason).ShouldBe(expected);
    }

    // STRENGTHENED rather than renumbered. This asserted only that the enum has N members, which any
    // renumbering satisfies and which says nothing about WHICH members exist -- a member renamed, or
    // one swapped for another, passes a count. The named set is the property worth binding, because
    // these values are published: a consumer switches on them, so removing or renumbering one is a
    // breaking change and should fail here rather than at their build.
    [Fact]
    public void Expose_exactly_the_named_failure_reasons()
    {
        Enum.GetValues<TransportSecurityFailureReason>().ShouldBe(
            [
                TransportSecurityFailureReason.Unspecified,
                TransportSecurityFailureReason.TlsNotEnabled,
                TransportSecurityFailureReason.DefaultCredentials,
            ],
            ignoreOrder: false,
            "these values are published and consumers switch on them, so adding one is deliberate and "
                + "removing or renumbering one is breaking");
    }
}
