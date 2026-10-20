using System.Security.Claims;
using Excalibur.A3.Authentication;
using Excalibur.Dispatch;

// A fixed local sample subject, not a token parser or production authentication implementation.
internal sealed class SampleSubject : IAuthenticationToken, ITenantContext
{
	public AuthenticationState AuthenticationState => AuthenticationState.Identified;
	public string? Jwt { get; set; }
	public IEnumerable<Claim>? Claims => [];
	public string? FirstName => "Alice";
	public string? LastName => "Johnson";
	public string FullName => "Alice Johnson";
	public string? Login => "alice";
	public string? UserId => "user-alice";
	public string? TenantId => "tenant-1";
	public bool HasTenant => true;
	public bool IsAnonymous() => false;
	public bool IsAuthenticated() => true;
}
