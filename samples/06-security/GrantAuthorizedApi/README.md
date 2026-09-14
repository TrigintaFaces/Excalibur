# Grant-authorized API

Gating ordinary ASP.NET Core endpoints on an Excalibur A3 grant — an MVC controller action and a
minimal API endpoint, both narrowed to the single order named by the route. No Dispatch pipeline is
involved.

## Run it

```bash
dotnet run
```

The sample drives itself: it starts the host, issues the requests below, prints the status code each
one produced, and exits.

```
  grants: alice -> order-1, order-2   bob -> order-1     (all in tenant-a)

Minimal API
  200 OK           /orders/order-2          as alice      — holds the grant
  403 Forbidden    /orders/order-2          as bob        — holds Read on Order, but not on THIS order
  403 Forbidden    /orders/order-1          as alice      — holds the grant, but in another tenant
  401 Unauthorized /orders/order-1          as anonymous  — not authenticated

MVC controller
  200 OK           /mvc/orders/order-2      as alice      — holds the grant
  403 Forbidden    /mvc/orders/order-2      as bob        — holds Read on Order, but not on THIS order
  403 Forbidden    /mvc/orders/order-1      as alice      — holds the grant, but in another tenant
  401 Unauthorized /mvc/orders/order-1      as anonymous  — not authenticated
```

The second line of each block is the one worth reading twice. `bob` genuinely holds `Read` on `Order`
— he is refused because he does not hold it on *this* order, which is the distinction a policy name
alone cannot express.

## What the sample shows

**One registration call.** `AddHttpGrantAuthorization()` bridges the authenticated request principal
into grant evaluation and enables the grant policy-name convention.

**The same policy in both hosting styles.** A minimal API endpoint builds the name so a typo is a
compile error:

```csharp
app.MapGet("/orders/{id}", (string id) => Results.Ok(id))
   .RequireAuthorization(GrantPolicyName.ForRouteValue("Read", "Order", "id"));
```

An attribute argument must be a constant, so a controller action writes the same name as a literal:

```csharp
[HttpGet("/mvc/orders/{id}")]
[Authorize(Policy = "grant:Read:Order:{id}")]
public IActionResult GetById(string id) => Ok(id);
```

**The tenant comes from the request.** `alice` holds the grant in `tenant-a` and is refused when the
same request arrives carrying `tenant-b`.

## What is substituted, and what is not

Two pieces are stand-ins so the sample runs with no infrastructure, and both are called out in the
code:

- **Authentication** is a header-reading scheme. Replace it with JWT bearer, cookies or OpenID
  Connect. Grant authorization reads whatever `ClaimsPrincipal` your scheme produces and never
  authenticates anything itself.
- **The grant store** is an in-memory set behind the same public seam a real one implements. A
  production host registers the A3 grant services and a durable store instead.

The identity and tenant are **not** substituted. They are resolved from the request principal by the
bridge this sample exists to demonstrate.
