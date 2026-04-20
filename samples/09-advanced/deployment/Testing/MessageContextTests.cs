using Excalibur.Dispatch.Testing;

using Shouldly;

namespace Testing;

/// <summary>
/// Demonstrates creating and verifying <see cref="Excalibur.Dispatch.Abstractions.IMessageContext"/>
/// instances using <see cref="MessageContextBuilder"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MessageContextTests
{
    [Fact]
    public void Builder_generates_defaults_for_ids()
    {
        var context = new MessageContextBuilder().Build();

        // MessageId and CorrelationId are auto-generated when not set
        context.MessageId.ShouldNotBeNullOrWhiteSpace();
        context.CorrelationId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Builder_sets_correlation_and_causation()
    {
        var context = new MessageContextBuilder()
            .WithCorrelationId("corr-abc")
            .WithCausationId("cause-xyz")
            .Build();

        context.CorrelationId.ShouldBe("corr-abc");
        context.CausationId.ShouldBe("cause-xyz");
    }

    [Fact]
    public void Builder_stores_custom_items()
    {
        var context = new MessageContextBuilder()
            .WithItem("TenantCode", "ACME")
            .WithItem("Priority", 5)
            .Build();

        context.Items["TenantCode"].ShouldBe("ACME");
        context.Items["Priority"].ShouldBe(5);
    }

    [Fact]
    public void Builder_sets_message_id_explicitly()
    {
        var context = new MessageContextBuilder()
            .WithMessageId("msg-fixed-001")
            .Build();

        context.MessageId.ShouldBe("msg-fixed-001");
    }

    [Fact]
    public void Builder_sets_tenant_and_user_via_identity_feature()
    {
        var context = new MessageContextBuilder()
            .WithTenantId("tenant-1")
            .WithUserId("user-42")
            .Build();

        // Identity values are stored in the Features dictionary
        context.Features.ShouldNotBeEmpty();
    }
}
