using System.Text.RegularExpressions;
using NetCord;
using NetCord.Rest;

// ---------------------------------------------------------------------------
// ReleaseForwarderDiscord (NetCord edition)
//
// Minimal one-shot tool intended for a GitHub Actions runner: it uses
// NetCord's stateless RestClient (no gateway connection - it sends one
// message and exits), posts a release embed with the changelog, and
// attaches any files in a given directory whose filename matches a regex
// pattern. Built to be Native AOT-publishable.
//
// Config is read from environment variables so it's easy to wire up from a
// workflow's `env:` block.
// ---------------------------------------------------------------------------

string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
string? channelIdRaw = Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID");
string releaseName = Environment.GetEnvironmentVariable("RELEASE_NAME") ?? "New Release";
string releaseTag = Environment.GetEnvironmentVariable("RELEASE_TAG") ?? "";
string releaseUrl = Environment.GetEnvironmentVariable("RELEASE_URL") ?? "";
string changelog = Environment.GetEnvironmentVariable("CHANGELOG_BODY") ?? "No changelog provided.";
string assetsDir = Environment.GetEnvironmentVariable("ASSETS_DIR") ?? Directory.GetCurrentDirectory();
string filePattern = Environment.GetEnvironmentVariable("FILE_PATTERN") ?? ".*";

if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("ERROR: DISCORD_TOKEN environment variable is required.");
    return 1;
}

if (!ulong.TryParse(channelIdRaw, out ulong channelId))
{
    Console.Error.WriteLine("ERROR: DISCORD_CHANNEL_ID must be a valid numeric channel ID.");
    return 1;
}

Regex regex;
try
{
    regex = new Regex(filePattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"ERROR: Invalid FILE_PATTERN regex '{filePattern}': {ex.Message}");
    return 1;
}

List<string> matchedFiles = Directory.Exists(assetsDir)
    ? Directory.GetFiles(assetsDir)
        .Where(f => regex.IsMatch(Path.GetFileName(f)))
        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
        .ToList()
    : [];

Console.WriteLine($"Assets dir: {assetsDir}");
Console.WriteLine($"Pattern:    {filePattern}");
Console.WriteLine($"Matched {matchedFiles.Count} file(s):");
foreach (var f in matchedFiles)
{
    Console.WriteLine($"  - {Path.GetFileName(f)}");
}

using var client = new RestClient(new BotToken(token));

try
{
    var embed = BuildEmbed(releaseName, releaseTag, releaseUrl, changelog);

    if (matchedFiles.Count == 0)
    {
        var message = new MessageProperties
        {
            Embeds = [embed],
        };
        await client.SendMessageAsync(channelId, message);
    }
    else
    {
        // Discord allows at most 10 attachments per message, so batch if needed.
        const int maxAttachmentsPerMessage = 10;
        var batches = matchedFiles
            .Select((path, index) => (path, index))
            .GroupBy(x => x.index / maxAttachmentsPerMessage)
            .Select(g => g.Select(x => x.path).ToList())
            .ToList();

        for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            // Streams must stay open until after SendMessageAsync completes.
            var streams = batches[batchIndex]
                .Select(path => File.OpenRead(path))
                .ToList();

            try
            {
                var attachments = batches[batchIndex]
                    .Zip(streams, (path, stream) => new AttachmentProperties(Path.GetFileName(path), stream))
                    .ToList();

                var message = new MessageProperties
                {
                    Attachments = attachments,
                };

                // Only the first message carries the embed (title/changelog/link).
                if (batchIndex == 0)
                {
                    message.Embeds = [embed];
                }

                await client.SendMessageAsync(channelId, message);
            }
            finally
            {
                foreach (var stream in streams)
                {
                    stream.Dispose();
                }
            }
        }
    }

    Console.WriteLine("Release notification sent successfully.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: Failed to send Discord message: {ex.Message}");
    return 1;
}

static EmbedProperties BuildEmbed(string releaseName, string releaseTag, string releaseUrl, string changelog)
{
    const int maxDescriptionLength = 4096; // Discord embed description hard limit
    if (changelog.Length > maxDescriptionLength)
    {
        changelog = string.Concat(changelog.AsSpan(0, maxDescriptionLength - 20), "\n... (truncated)");
    }

    string title = string.IsNullOrWhiteSpace(releaseTag) ? releaseName : $"{releaseName} ({releaseTag})";
    // Discord embed titles are capped at 256 characters.
    if (title.Length > 256)
    {
        title = title[..253] + "...";
    }

    var embed = new EmbedProperties
    {
        Title = title,
        Description = changelog,
        Color = new Color(0x3498DB),
        Timestamp = DateTimeOffset.UtcNow,
    };

    if (!string.IsNullOrWhiteSpace(releaseUrl))
    {
        embed.Url = releaseUrl;
    }

    return embed;
}
