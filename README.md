# Hatband

Hatband is an open-source desktop game library manager in active development. It brings game libraries, metadata, artwork, and playtime information together in one interface.

## Current features

- Connect to Steam with a QR code and sync your game library.
- View game details, installation state, and locally stored library data.
- Search for metadata and artwork from available providers, including Steam and experimental IGN support.
- Browse HowLongToBeat completion-time estimates.
- Store library data in SQLite and artwork as local files.

Steam is currently the only implemented library connector. Other providers supply metadata or artwork and do not sync a store library.

## Build

Prerequisite: .NET 10 SDK.

```sh
dotnet build source/Hatband.slnx
```

The solution is organized into four projects:

- `Hatband.App` — Avalonia desktop application and user interface.
- `Hatband.Core` — domain models and shared contracts.
- `Hatband.Integrations` — external service integrations.
- `Hatband.Infrastructure` — persistence and application services.

## AI-assisted development

AI tools, including OpenAI Codex, are used as supporting engineering tools under maintainer direction. AI-assisted changes are reviewed and validated as part of the development process. Project decisions and final responsibility remain with the maintainers.

## Acknowledgements

Hatband began with the project structure prepared for [Playnite](https://github.com/JosefNemec/Playnite), the open-source game library manager created by [Josef Nemec](https://github.com/JosefNemec). Hatband is now being developed as a separate application. We are grateful to Josef and the Playnite contributors for their original work and for the foundation that helped make Hatband possible.

## License

Hatband is licensed under the MIT License. See [LICENSE.md](LICENSE.md).
