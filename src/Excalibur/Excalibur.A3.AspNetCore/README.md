# Excalibur.A3.AspNetCore

Gate ordinary ASP.NET Core endpoints — MVC controller actions and minimal API endpoints — on an
Excalibur A3 grant, for requests that never go through Dispatch.

This package supplies the two pieces the framework-agnostic grant evaluator cannot provide on its own:

- **The identity bridge.** The user and tenant that grant evaluation reads are derived from the
  `ClaimsPrincipal` ASP.NET Core authentication already established for the request. No authentication
  is performed here and no token is reparsed.
- **Per-request resource scope.** A convention-based policy name can name a route parameter, so one
  policy expresses *may approve **this** order* rather than only *may approve orders*.

## Install and register

```csharp
builder.Services
    .AddExcaliburA3()
    .Services
    .AddHttpGrantAuthorization();
```

`AddHttpGrantAuthorization` registers the identity and tenant bridge, the grant-aware authorization
policy provider, the grant handler, and a startup check. Authentication is configured as usual, and
`UseAuthentication` must run before `UseAuthorization`.

## Applying a grant

`[RequireGrant]` is strongly typed and works on both hosting styles:

```csharp
// Controller action
[HttpGet("/orders/{id}")]
[RequireGrant("Read", "Order", "id")]
public IActionResult GetById(string id) => Ok(id);

// Minimal API endpoint
app.MapGet("/orders/{id}", (string id) => Results.Ok(id))
   .RequireAuthorization(new RequireGrantAttribute("Read", "Order", "id"));
```

The third argument names a **route parameter**, read from the request at match time, so one attribute
covers every order. Drop it to require the activity against the resource type alone.

## Policy names

| Name | Meaning |
| --- | --- |
| `grant:Read:Order` | Caller holds the `Read` grant for the `Order` resource type. |
| `grant:Read:Order:{id}` | Caller holds `Read` for the specific order named by route parameter `id`. |
| `grant:Read:Order:order-42` | Caller holds `Read` for the fixed resource `order-42`. |

`[RequireGrant]` sets the equivalent name. Build one yourself with `GrantPolicyName` when a seam accepts
only a string:

```csharp
var policyName = GrantPolicyName.ForRouteValue("Read", "Order", "id");
```

Any policy name that does not begin with `grant:` resolves exactly as before, so policies registered by
name — including those from `AddGrantAuthorization` — keep working, as does a bare
`RequireAuthorization()`.

## Claim mapping

The user and tenant are read from the first matching claim in an ordered candidate list, because
identity providers disagree about which claim carries them.

```csharp
builder.Services.AddHttpGrantAuthorization(options =>
{
    options.UserIdClaimTypes.Insert(0, "urn:my-idp:subject");
    options.TenantIdClaimTypes.Insert(0, "org");

    // Single-tenant host whose identity provider issues no tenant claim.
    options.DefaultTenantId = "default";
});
```

## Failure behaviour

Authorization fails closed, and every denial that is not simply a missing grant is logged with the
reason, so a `403` is never left ambiguous:

- An unauthenticated caller receives a `401` challenge rather than a `403`.
- A caller whose user or tenant cannot be resolved is denied, with a warning naming the cause.
- A resource-scoped policy on an endpoint whose route declares no such parameter is denied, with a
  warning naming the parameter. It is never downgraded to the unscoped check, which would be more
  permissive than the policy asked for.
- A missing registration fails at host start with the call that fixes it, rather than turning every
  request into a `403`.
