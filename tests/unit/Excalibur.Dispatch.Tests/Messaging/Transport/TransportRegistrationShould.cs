using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportRegistrationShould
{
	[Fact]
	public void CreateWithAllProperties()
	{
		var adapter = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object> { ["timeout"] = 30 };
		var reg = new TransportRegistration(adapter, "kafka", options);

		reg.Adapter.ShouldBe(adapter);
		reg.TransportType.ShouldBe("kafka");
		reg.Options.ShouldBe(options);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		var adapter = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object>();
		var reg1 = new TransportRegistration(adapter, "kafka", options);
		var reg2 = new TransportRegistration(adapter, "kafka", options);

		reg1.ShouldBe(reg2);
	}
}
