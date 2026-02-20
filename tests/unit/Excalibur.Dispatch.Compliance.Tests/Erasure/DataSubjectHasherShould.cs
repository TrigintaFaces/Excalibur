namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

public class DataSubjectHasherShould
{
    [Fact]
    public void Return_uppercase_hex_encoded_sha256_hash()
    {
        var hash = DataSubjectHasher.HashDataSubjectId("test-user-123");

        hash.ShouldNotBeNullOrWhiteSpace();
        hash.Length.ShouldBe(64); // SHA-256 produces 32 bytes = 64 hex chars
    }

    [Fact]
    public void Return_consistent_hash_for_same_input()
    {
        var hash1 = DataSubjectHasher.HashDataSubjectId("user@example.com");
        var hash2 = DataSubjectHasher.HashDataSubjectId("user@example.com");

        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void Return_different_hashes_for_different_inputs()
    {
        var hash1 = DataSubjectHasher.HashDataSubjectId("user-1");
        var hash2 = DataSubjectHasher.HashDataSubjectId("user-2");

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void Return_uppercase_hex_string()
    {
        var hash = DataSubjectHasher.HashDataSubjectId("some-user");

        // All characters should be uppercase hex (0-9, A-F)
        hash.ShouldAllBe(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'));
    }
}
