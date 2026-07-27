using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class TransportRegistrationShould
{
	[Fact]
	public void CreateWithAllProperties()
	{
		var adapter = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object> { ["timeout"] = 30 };
		var reg = new TransportRegistration(adapter, "kafka", TransportLocality.Remote, options);

		reg.Adapter.ShouldBe(adapter);
		reg.TransportType.ShouldBe("kafka");
		reg.Locality.ShouldBe(TransportLocality.Remote);
		reg.Options.ShouldBe(options);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		var adapter = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object>();
		var reg1 = new TransportRegistration(adapter, "kafka", TransportLocality.Remote, options);
		var reg2 = new TransportRegistration(adapter, "kafka", TransportLocality.Remote, options);

		reg1.ShouldBe(reg2);
	}
}