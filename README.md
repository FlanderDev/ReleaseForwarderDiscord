# ReleaseForwarderDiscord (NetCord, Native AOT)

Minimal console app that posts a release announcement (title, changelog, and
matching files) to a Discord channel. Built with [NetCord](https://netcord.dev)'s
stateless `RestClient` — no gateway connection, since the job sends one
message and exits — and published as a **Native AOT** binary, so the GitHub
Actions job runs a small, self-contained native executable with no .NET
runtime dependency and near-instant startup.

Why NetCord instead of Discord.Net: Discord.Net's REST models rely on
reflection-based Newtonsoft.Json (de)serialization, which breaks under Native
AOT. NetCord was built with AOT support as an explicit design goal.

## Requirements

- **.NET 10 SDK** to build/publish (NetCord `1.0.0-beta.16` targets `net10.0`
  only).
- On Linux, Native AOT needs `clang` and `zlib1g-dev` installed at publish
  time (see workflow below) — these aren't needed at runtime, only to link
  the binary during `dotnet publish`.

## Setup

1. **Create a Discord bot** at https://discord.com/developers/applications,
   add it to your server, and grant it `View Channel`, `Send Messages`, and
   `Attach Files` permissions on the target channel.
2. Copy the bot token and the target channel's ID.
3. In your GitHub repo, add two secrets:
   - `DISCORD_BOT_TOKEN`
   - `DISCORD_CHANNEL_ID`

## Environment variables

| Variable              | Required | Description                                                            |
|------------------------|----------|--------------------------------------------------------------------------|
| `DISCORD_TOKEN`       | yes      | Bot token                                                                 |
| `DISCORD_CHANNEL_ID`  | yes      | Numeric ID of the target text channel                                    |
| `RELEASE_NAME`        | no       | Shown as the embed title (default: "New Release")                        |
| `RELEASE_TAG`         | no       | Appended to the title in parentheses                                     |
| `RELEASE_URL`         | no       | Makes the embed title a clickable link                                   |
| `CHANGELOG_BODY`      | no       | Embed description; truncated to Discord's 4096-char limit                |
| `ASSETS_DIR`          | no       | Directory to scan for files (default: current directory)                 |
| `FILE_PATTERN`        | no       | Regex tested against each filename (default: `.*`, i.e. match everything)|

Files are batched 10 per message if more than 10 match, since that's
Discord's per-message attachment limit.

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

## In GitHub Actions

See `.github/workflows/notify-discord.yml`. It:

1. Downloads the release's assets with the `gh` CLI.
2. Installs `clang`/`zlib1g-dev` (Native AOT's Linux linker prerequisites).
3. Publishes the app as a Native AOT binary for `linux-x64`.
4. Runs the published binary directly, with config passed via `env:`.

## AOT notes

- No custom `System.Text.Json` source-generation context is needed here —
  this app doesn't serialize anything itself, and NetCord handles its own
  Discord API JSON internally with AOT support as a design goal.
- `System.Text.RegularExpressions.Regex` (used for `FILE_PATTERN`) works
  under Native AOT without extra setup — it falls back to its
  reflection-free interpreter automatically when no source-generated regex
  is available.
- If you ever add your own JSON handling to this app, generate a
  `JsonSerializerContext` for it rather than relying on reflection-based
  serialization, or the AOT publish will emit trim warnings.
