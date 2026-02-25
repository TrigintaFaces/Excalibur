using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageMetadataShould
{
	[Fact]
	public void CreateWithAllParameters()
	{
		var metadata = new MessageMetadata(
			MessageId: "msg-1",
			CorrelationId: "corr-1",
			CausationId: "cause-1",
			TraceParent: "00-trace-1",
			TenantId: "tenant-1",
			UserId: "user-1",
			ContentType: "application/json",
			SerializerVersion: "2.0.0",
			MessageVersion: "1.0.0",
			ContractVersion: "1.1.0");

		metadata.MessageId.ShouldBe("msg-1");
		metadata.CorrelationId.ShouldBe("corr-1");
		metadata.CausationId.ShouldBe("cause-1");
		metadata.TraceParent.ShouldBe("00-trace-1");
		metadata.TenantId.ShouldBe("tenant-1");
		metadata.UserId.ShouldBe("user-1");
		metadata.ContentType.ShouldBe("application/json");
		metadata.SerializerVersion.ShouldBe("2.0.0");
		metadata.MessageVersion.ShouldBe("1.0.0");
		metadata.ContractVersion.ShouldBe("1.1.0");
	}

	[Fact]
	public void DefaultContractVersionToOneZero()
	{
		var metadata = new MessageMetadata(
			"msg-1", "corr-1", null, null, null, null,
			"application/json", "1.0.0", "1.0.0");

		metadata.ContractVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void CreateFromContext()
	{
		var message = A.Fake<IDispatchMessage>();
		var sp = A.Fake<IServiceProvider>();
		var context = new MessageContext(message, sp);
		context.MessageId = "msg-ctx";
		context.CorrelationId = "corr-ctx";
		context.CausationId = "cause-ctx";
		context.TraceParent = "trace-ctx";
		context.TenantId = "tenant-ctx";
		context.UserId = "user-ctx";
		context.ContentType = "text/plain";
		// SerializerVersion, MessageVersion, ContractVersion are extension methods
		// that read from Properties/Items — set via the extensions
		context.SerializerVersion("2.0.0");
		context.MessageVersion("3.0.0");
		context.ContractVersion("4.0.0");

		var metadata = MessageMetadata.FromContext(context);

		metadata.MessageId.ShouldBe("msg-ctx");
		metadata.CorrelationId.ShouldBe("corr-ctx");
		metadata.CausationId.ShouldBe("cause-ctx");
		metadata.TenantId.ShouldBe("tenant-ctx");
		metadata.UserId.ShouldBe("user-ctx");
		metadata.ContentType.ShouldBe("text/plain");
		metadata.SerializerVersion.ShouldBe("2.0.0");
		metadata.MessageVersion.ShouldBe("3.0.0");
		metadata.ContractVersion.ShouldBe("4.0.0");
	}

	[Fact]
	public void FromContext_GeneratesIdsWhenNull()
	{
		var message = A.Fake<IDispatchMessage>();
		var sp = A.Fake<IServiceProvider>();
		var context = new MessageContext(message, sp);
		// MessageId and CorrelationId are auto-generated in MessageContext
		// ContentType defaults to null, SerializerVersion/MessageVersion/ContractVersion unset

		var metadata = MessageMetadata.FromContext(context);

		metadata.MessageId.ShouldNotBeNullOrWhiteSpace();
		metadata.CorrelationId.ShouldNotBeNullOrWhiteSpace();
		metadata.ContentType.ShouldBe("application/json");
		metadata.SerializerVersion.ShouldBe("1.0.0");
		metadata.MessageVersion.ShouldBe("1.0.0");
		metadata.ContractVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void FromContext_ThrowsOnNull()
	{
		Should.Throw<ArgumentNullException>(() => MessageMetadata.FromContext(null!));
	}

	[Fact]
	public void SupportRecordEquality()
	{
		var m1 = new MessageMetadata("a", "b", null, null, null, null, "json", "1", "1");
		var m2 = new MessageMetadata("a", "b", null, null, null, null, "json", "1", "1");

		m1.ShouldBe(m2);
	}

	[Fact]
	public void SupportWithExpression()
	{
		var original = new MessageMetadata("a", "b", null, null, null, null, "json", "1", "1");
		var modified = original with { MessageId = "c" };

		modified.MessageId.ShouldBe("c");
		modified.CorrelationId.ShouldBe("b");
		original.MessageId.ShouldBe("a");
	}
}
