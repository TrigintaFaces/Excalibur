using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OperationTimeoutExceptionShould
{
	[Fact]
	public void CreateWithDefault()
	{
		var ex = new OperationTimeoutException();

		ex.DispatchStatusCode.ShouldBe(408);
		ex.Message.ShouldContain("timed out");
	}

	[Fact]
	public void CreateWithMessage()
	{
		var ex = new OperationTimeoutException("Custom timeout");

		ex.Message.ShouldBe("Custom timeout");
		ex.DispatchStatusCode.ShouldBe(408);
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		var inner = new TimeoutException("inner");
		var ex = new OperationTimeoutException("msg", inner);

		ex.Message.ShouldBe("msg");
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void CreateWithOperationAndDuration()
	{
		var ex = new OperationTimeoutException("QueryUsers", TimeSpan.FromSeconds(5));

		ex.Operation.ShouldBe("QueryUsers");
		ex.Duration.ShouldBe(TimeSpan.FromSeconds(5));
		ex.Timeout.ShouldBeNull();
		ex.Message.ShouldContain("QueryUsers");
		ex.Message.ShouldContain("5.0s");
	}

	[Fact]
	public void CreateWithOperationDurationAndTimeout()
	{
		var ex = new OperationTimeoutException("QueryUsers", TimeSpan.FromSeconds(35), TimeSpan.FromSeconds(30));

		ex.Operation.ShouldBe("QueryUsers");
		ex.Duration.ShouldBe(TimeSpan.FromSeconds(35));
		ex.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
		ex.Message.ShouldContain("30.0s");
		ex.Message.ShouldContain("35.0s");
	}

	[Fact]
	public void CreateWithCancellationException()
	{
		var oce = new OperationCanceledException("cancelled");
		var ex = new OperationTimeoutException("MyOp", TimeSpan.FromMilliseconds(500), oce);

		ex.Operation.ShouldBe("MyOp");
		ex.Duration.ShouldBe(TimeSpan.FromMilliseconds(500));
		ex.InnerException.ShouldBe(oce);
		ex.Message.ShouldContain("500ms");
	}

	[Fact]
	public void FormatDuration_Milliseconds()
	{
		var ex = new OperationTimeoutException("op", TimeSpan.FromMilliseconds(250));

		ex.Message.ShouldContain("250ms");
	}

	[Fact]
	public void FormatDuration_Minutes()
	{
		var ex = new OperationTimeoutException("op", TimeSpan.FromMinutes(2.5));

		ex.Message.ShouldContain("2.5m");
	}

	[Fact]
	public void FormatDuration_Hours()
	{
		var ex = new OperationTimeoutException("op", TimeSpan.FromHours(1.5));

		ex.Message.ShouldContain("1.5h");
	}

	[Fact]
	public void FromCancellation_CreatesWithInnerException()
	{
		var oce = new OperationCanceledException("cancelled");
		var ex = OperationTimeoutException.FromCancellation("MyOp", TimeSpan.FromSeconds(10), oce);

		ex.Operation.ShouldBe("MyOp");
		ex.InnerException.ShouldBe(oce);
	}

	[Fact]
	public void DatabaseQuery_CreatesWithQueryContext()
	{
		var ex = OperationTimeoutException.DatabaseQuery("GetAllOrders", TimeSpan.FromSeconds(30));

		ex.Operation.ShouldBe("Database:GetAllOrders");
		ex.Duration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void ExternalService_CreatesWithServiceContext()
	{
		var ex = OperationTimeoutException.ExternalService("PaymentGateway", TimeSpan.FromSeconds(15));

		ex.Operation.ShouldBe("ExternalService:PaymentGateway");
		ex.Duration.ShouldBe(TimeSpan.FromSeconds(15));
	}
}
