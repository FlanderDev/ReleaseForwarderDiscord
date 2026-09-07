# ReleaseForwarderDiscord (NetCord, Native AOT)

Minimal console app that posts a release announcement (title, changelog, and
download links) to a Discord channel. Built with [NetCord](https://netcord.dev)'s
stateless `RestClient` — no gateway connection, since the job sends one
message and exits — and published as a **Native AOT** binary, so the GitHub
Actions job runs a small, self-contained native executable with no .NET
runtime dependency and near-instant startup.

Why NetCord instead of Discord.Net: Discord.Net's REST models rely on
reflection-based Newtonsoft.Json (de)serialization, which breaks under Native
AOT. NetCord was built with AOT support as an explicit design goal.

## Requirements

* **.NET 10 SDK** to build/publish (NetCord 1.0.0-beta.16 targets net10.0
  only).
* On Linux, Native AOT needs clang and zlib1g-dev installed at publish
  time (see workflow below) — these aren't needed at runtime, only to link
  the binary during `dotnet publish`.

## Setup

1. **Create a Discord bot** at [https://discord.com/developers/applications](https://discord.com/developers/applications),
   add it to your server, and grant it **View Channel** and **Send Messages**
   permissions on the target channel.
2. Copy the bot token and the target channel's ID.
3. In your GitHub repository, add two secrets:
   * `DISCORD_BOT_TOKEN`
   * `DISCORD_CHANNEL_ID`

## Using as a Reusable Workflow (Recommended)

This repository provides a reusable workflow that other repositories can call.

### Calling example

In any other repository, create (or update) a workflow that triggers on release:

```yaml
# .github/workflows/release.yml

name: Release & Notify Discord

on:
  release:
    types: [published]

jobs:
  notify-discord:
    uses: FlanderDev/ReleaseForwarderDiscord/.github/workflows/notify-discord.yml@main
    secrets:
      DISCORD_BOT_TOKEN: ${{ secrets.DISCORD_BOT_TOKEN }}
      DISCORD_CHANNEL_ID: ${{ secrets.DISCORD_CHANNEL_ID }}
    with:
      # Optional – only link these file types (default is everything)
      file_pattern: '\.(zip|tar\.gz|exe|msi|dmg)$'
```

### Available inputs

| Input          | Required | Default | Description                                      |
|----------------|----------|---------|--------------------------------------------------|
| `file_pattern` | No       | `.*`    | Regex tested against each asset filename         |

### Required secrets

| Secret               | Required | Description              |
|----------------------|----------|--------------------------|
| `DISCORD_BOT_TOKEN`  | Yes      | Discord bot token        |
| `DISCORD_CHANNEL_ID` | Yes      | Target text channel ID   |

> **Note:** The calling workflow **must** be triggered by a `release` event
> so that `github.event.release.*` is available.

## Environment variables (when running the binary directly)

| Variable             | Required | Description                                              |
|----------------------|----------|----------------------------------------------------------|
| `DISCORD_TOKEN`      | Yes      | Bot token                                                |
| `DISCORD_CHANNEL_ID` | Yes      | Numeric ID of the target text channel                    |
| `RELEASE_NAME`       | No       | Shown as the embed title (default: "New Release")        |
| `RELEASE_TAG`        | No       | Appended to the title in parentheses                     |
| `RELEASE_URL`        | No       | Makes the embed title a clickable link                   |
| `CHANGELOG_BODY`     | No       | Embed description; truncated to Discord's 4096-char limit|
| `ASSET_LINKS`        | No       | Markdown list of download links (appended under **Downloads**) |

The tool no longer uploads files. Instead it places clickable download links
in the embed description.

## Building locally

```bash
# Ordinary build/run (JIT, any OS with the .NET 10 SDK)
dotnet run

# Native AOT publish (must run on the target OS/arch, e.g. linux-x64 on a
# Linux x64 machine, unless you set up cross-compilation)
dotnet publish -c Release -r linux-x64
./bin/Release/net10.0/linux-x64/publish/ReleaseForwarderDiscord
```

Set the same environment variables as before (`DISCORD_TOKEN`,
`DISCORD_CHANNEL_ID`, `RELEASE_NAME`, etc.) before running either form.

## In GitHub Actions (standalone / reusable)

See `.github/workflows/notify-discord.yml`. It is designed both as a
standalone workflow and as a reusable workflow (`workflow_call`).

When used it:

1. Builds a list of markdown download links from the release assets
   (optionally filtered by `file_pattern`).
2. Installs clang / zlib1g-dev (Native AOT's Linux linker prerequisites).
3. Publishes the app as a Native AOT binary for `linux-x64`.
4. Runs the published binary, passing the release metadata and the generated
   links via environment variables.

## AOT notes

* No custom System.Text.Json source-generation context is needed here —
  this app doesn't serialize anything itself, and NetCord handles its own
  Discord API JSON internally with AOT support as a design goal.
* If you ever add your own JSON handling to this app, generate a
  `JsonSerializerContext` for it rather than relying on reflection-based
  serialization, or the AOT publish will emit trim warnings.
