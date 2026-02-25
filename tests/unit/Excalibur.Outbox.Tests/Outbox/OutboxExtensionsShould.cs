using Excalibur.Dispatch.Delivery;

using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxOptions;

namespace Excalibur.Outbox.Tests.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxExtensionsShould
{
	[Fact]
	public void ReturnDefaultBatchSizeOf100()
	{
		// Arrange
		var options = new DeliveryOutboxOptions();

		// Act
		var batchSize = options.MessageBatchSize();

		// Assert
		batchSize.ShouldBe(100);
	}
}
