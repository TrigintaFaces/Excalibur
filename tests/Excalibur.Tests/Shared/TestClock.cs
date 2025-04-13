namespace Excalibur.Tests.Shared;

public class TestClock : ITestClock
{
	public DateTime UtcNow { get; set; } = DateTime.UtcNow;
}

public interface ITestClock
{
	public DateTime UtcNow { get; }
}
