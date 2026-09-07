using NetCord;
using NetCord.Rest;

// ---------------------------------------------------------------------------
// ReleaseForwarderDiscord (NetCord edition)
//
// Posts a release announcement embed to Discord.
// ---------------------------------------------------------------------------

string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
string? channelIdRaw = Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID");
string releaseName = Environment.GetEnvironmentVariable("RELEASE_NAME") ?? "New Release";
string releaseTag = Environment.GetEnvironmentVariable("RELEASE_TAG") ?? "";
string releaseUrl = Environment.GetEnvironmentVariable("RELEASE_URL") ?? "";
string changelog = Environment.GetEnvironmentVariable("CHANGELOG_BODY") ?? "No changelog provided.";
string assetLinks = Environment.GetEnvironmentVariable("ASSET_LINKS") ?? "";   // markdown links

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

using var client = new RestClient(new BotToken(token));

try
{
    var embed = BuildEmbed(releaseName, releaseTag, releaseUrl, changelog, assetLinks);

    var message = new MessageProperties
    {
        Embeds = [embed],
    };

    await client.SendMessageAsync(channelId, message);

    Console.WriteLine("Release notification sent successfully.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: Failed to send Discord message: {ex.Message}");
    return 1;
}

static EmbedProperties BuildEmbed(
    string releaseName,
    string releaseTag,
    string releaseUrl,
    string changelog,
    string assetLinks)
{
    const int maxDescriptionLength = 4096;

    // Append download links if provided
    if (!string.IsNullOrWhiteSpace(assetLinks))
    {
        changelog = string.IsNullOrWhiteSpace(changelog)
            ? assetLinks
            : $"{changelog.TrimEnd()}\n\n**Downloads**\n{assetLinks.Trim()}";
    }

    if (changelog.Length > maxDescriptionLength)
    {
        changelog = string.Concat(changelog.AsSpan(0, maxDescriptionLength - 20), "\n... (truncated)");
    }

    string title = string.IsNullOrWhiteSpace(releaseTag)
        ? releaseName
        : $"{releaseName} ({releaseTag})";

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