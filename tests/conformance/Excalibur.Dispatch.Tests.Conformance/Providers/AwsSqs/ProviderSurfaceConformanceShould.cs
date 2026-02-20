using System.Linq;

using Excalibur.Dispatch.Transport.Aws;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Providers.AwsSqs;

public class ProviderSurfaceConformanceShould
{
	[Fact]
	public void Contain_TransportSender_Implementation()
	{
		var asm = typeof(AwsEventBridgeOptions).Assembly;
		var types = asm.GetTypes().Where(t => !t.IsAbstract && typeof(ITransportSender).IsAssignableFrom(t)).ToArray();
		types.Length.ShouldBeGreaterThan(0, "AWS SQS transport should implement ITransportSender");
	}

	[Fact]
	public void Contain_TransportReceiver_Implementation()
	{
		var asm = typeof(AwsEventBridgeOptions).Assembly;
		var types = asm.GetTypes().Where(t => !t.IsAbstract && typeof(ITransportReceiver).IsAssignableFrom(t)).ToArray();
		types.Length.ShouldBeGreaterThan(0, "AWS SQS transport should implement ITransportReceiver");
	}
}
