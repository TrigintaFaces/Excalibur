using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchMessageShould
{
	[Fact]
	public void HaveDefaultIdAsGuid()
	{
		var message = new DispatchMessage();

		Guid.TryParse(message.Id, out _).ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultEmptyMessageType()
	{
		var message = new DispatchMessage();

		message.MessageType.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultEmptyPayload()
	{
		var message = new DispatchMessage();

		message.Payload.ShouldBeEmpty();
	}

	[Fact]
	public void HaveDefaultCreatedAtTimestamp()
	{
		var message = new DispatchMessage();

		message.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	public void AllowSettingProperties()
	{
		var message = new DispatchMessage
		{
			Id = "custom-id",
			MessageType = "TestMessage",
			Payload = [1, 2, 3],
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
		};

		message.Id.ShouldBe("custom-id");
		message.MessageType.ShouldBe("TestMessage");
		message.Payload.ShouldBe(new byte[] { 1, 2, 3 });
		message.CreatedAt.ShouldBe(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
	}

	[Fact]
	public void GenerateUniqueIds()
	{
		var message1 = new DispatchMessage();
		var message2 = new DispatchMessage();

		message1.Id.ShouldNotBe(message2.Id);
	}
}
