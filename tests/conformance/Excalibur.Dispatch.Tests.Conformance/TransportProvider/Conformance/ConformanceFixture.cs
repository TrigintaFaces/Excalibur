namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider;

public class ConformanceFixture
{
	[Fact]
	public async Task SenderReceiver_BasicSendReceive_Succeeds()
	{
		var channel = new FakeChannel();

		await channel.SendAsync(new { Value = 42 }, CancellationToken.None).ConfigureAwait(false);

		var result = await channel.ReceiveAsync<object>(CancellationToken.None).ConfigureAwait(false);
		_ = result.ShouldNotBeNull();
	}
}
