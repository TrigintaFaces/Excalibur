using Excalibur.Dispatch.Compliance.Rectification;

namespace Excalibur.Dispatch.Compliance.Tests.Rectification;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RectificationRecordShould
{
	[Fact]
	public void Store_all_constructor_parameters()
	{
		var rectifiedAt = DateTimeOffset.UtcNow;

		var record = new RectificationRecord(
			"subject-123",
			"email",
			"old@example.com",
			"new@example.com",
			"Incorrect email",
			rectifiedAt);

		record.SubjectId.ShouldBe("subject-123");
		record.FieldName.ShouldBe("email");
		record.OldValue.ShouldBe("old@example.com");
		record.NewValue.ShouldBe("new@example.com");
		record.Reason.ShouldBe("Incorrect email");
		record.RectifiedAt.ShouldBe(rectifiedAt);
	}

	[Fact]
	public void Support_value_equality()
	{
		var rectifiedAt = DateTimeOffset.UtcNow;

		var record1 = new RectificationRecord("sub-1", "name", "old", "new", "fix", rectifiedAt);
		var record2 = new RectificationRecord("sub-1", "name", "old", "new", "fix", rectifiedAt);

		record1.ShouldBe(record2);
	}

	[Fact]
	public void Not_equal_when_field_name_differs()
	{
		var rectifiedAt = DateTimeOffset.UtcNow;

		var record1 = new RectificationRecord("sub-1", "name", "old", "new", "fix", rectifiedAt);
		var record2 = new RectificationRecord("sub-1", "email", "old", "new", "fix", rectifiedAt);

		record1.ShouldNotBe(record2);
	}
}
