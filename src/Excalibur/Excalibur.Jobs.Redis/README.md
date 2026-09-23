# Excalibur.Jobs.Redis

Redis-based distributed job coordination for the Excalibur Jobs framework.

## Features

- Distributed job locking via Redis
- Job instance registry with heartbeat monitoring
- Job distribution across available instances
- Leadership token management

## Usage

```csharp
services.AddJobCoordinationRedis("localhost:6379");
```

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
