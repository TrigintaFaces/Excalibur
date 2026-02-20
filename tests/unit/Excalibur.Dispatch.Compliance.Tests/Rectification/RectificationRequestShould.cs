using Excalibur.Dispatch.Compliance.Rectification;

namespace Excalibur.Dispatch.Compliance.Tests.Rectification;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RectificationRequestShould
{
	[Fact]
	public void Store_all_constructor_parameters()
	{
		var request = new RectificationRequest(
			"subject-123",
			"email",
			"old@example.com",
			"new@example.com",
			"Incorrect email address");

		request.SubjectId.ShouldBe("subject-123");
		request.FieldName.ShouldBe("email");
		request.OldValue.ShouldBe("old@example.com");
		request.NewValue.ShouldBe("new@example.com");
		request.Reason.ShouldBe("Incorrect email address");
	}

	[Fact]
	public void Support_value_equality()
	{
		var request1 = new RectificationRequest("sub-1", "name", "old", "new", "fix");
		var request2 = new RectificationRequest("sub-1", "name", "old", "new", "fix");

		request1.ShouldBe(request2);
	}

	[Fact]
	public void Not_equal_when_subject_id_differs()
	{
		var request1 = new RectificationRequest("sub-1", "name", "old", "new", "fix");
		var request2 = new RectificationRequest("sub-2", "name", "old", "new", "fix");

		request1.ShouldNotBe(request2);
	}
}
