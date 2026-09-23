# Excalibur.Operations.Dashboard.Spa

Embedded single-page app for the **Excalibur Operations Dashboard**. Ships the
read-only dashboard UI as AOT-neutral static assets embedded in the assembly and
serves them under a configurable path prefix with a strict Content-Security-Policy.

## Usage

Map the SPA from any ASP.NET Core endpoint pipeline:

```csharp
var app = builder.Build();

app.MapDashboardSpa();          // served under /dashboard
// or
app.MapDashboardSpa("/ops");    // served under a custom prefix
```

Only `GET` routes are mapped, so serving the UI never exposes a mutating surface.
Hashed assets are served with an immutable long-lived cache; any unmatched
sub-path falls back to the SPA entry document for client-side routing.

## How the assets are built

The UI is a Svelte + Vite app in `ClientApp/`. Its production build output is
committed to `wwwroot/` and embedded via `ManifestEmbeddedFileProvider`.

- A normal `dotnet build` embeds the **committed** `wwwroot/` bytes — no Node
  toolchain required, reproducible, and AOT-neutral.
- To rebuild the UI from source, pass `-p:BuildSpaAssets=true` (requires Node +
  npm); the MSBuild target runs `npm ci && npm run build` before embedding.

```bash
# rebuild the SPA from source, then build the package
dotnet build -p:BuildSpaAssets=true
```

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
