using System.Xml.Linq;
using Reddit.Reader.Builder.Models;

namespace Reddit.Reader.Builder.Services;

public sealed class RssFeedService(
    IConfiguration config,
    ILogger<RssFeedService> logger) : IRssFeedService
{
    private const string FeedTitle = "Reddit Reader — Automated Podcast";
    private const string FeedDescription = "Daily Reddit posts read aloud by Kokoro TTS, cleaned by Gemini.";
    private const string FeedLanguage = "en";
    private const string FeedLink = "https://tisaconundrum2.github.io/Reddit.Reader";

    public Task AddEpisodeAsync(RedditPost post, FileInfo mp3File, CancellationToken ct = default)
    {
        var feedFile = config["Pipeline:FeedFile"] ?? "feed.xml";
        var feedBaseUrl = config["Pipeline:FeedBaseUrl"] ?? string.Empty;
        var downloadUrl = string.IsNullOrWhiteSpace(feedBaseUrl)
            ? mp3File.FullName
            : $"{feedBaseUrl.TrimEnd('/')}/{mp3File.Name}";

        var doc = LoadOrCreateFeed(feedFile);
        var channel = doc.Root!.Element("channel")!;

        var existingIds = channel.Elements("item")
            .Select(i => i.Element("guid")?.Value)
            .Where(id => id is not null)
            .ToHashSet();

        if (existingIds.Contains(post.PostId))
        {
            logger.LogInformation("[rss] {PostId} already in feed, skipping.", post.PostId);
            return Task.CompletedTask;
        }

        var title = $"[r/{post.Subreddit}] {post.Title}";
        var description = post.Selftext.Length > 500 ? post.Selftext[..500] : post.Selftext;
        var pubDate = DateTimeOffset.UtcNow.ToString("R"); // RFC 1123

        var item = new XElement("item",
            new XElement("title", title),
            new XElement("guid", post.PostId),
            new XElement("description", description),
            new XElement("pubDate", pubDate),
            new XElement("enclosure",
                new XAttribute("url", downloadUrl),
                new XAttribute("length", "0"),
                new XAttribute("type", "audio/mpeg"))
        );

        channel.Add(item);
        doc.Save(feedFile);

        logger.LogInformation("[rss] Added episode {PostId} to feed.", post.PostId);
        return Task.CompletedTask;
    }

    private static XDocument LoadOrCreateFeed(string path)
    {
        if (File.Exists(path))
        {
            try { return XDocument.Load(path); }
            catch { /* fall through to create a fresh feed */ }
        }

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XAttribute(XNamespace.Xmlns + "itunes", "http://www.itunes.com/dtds/podcast-1.0.dtd"),
                new XElement("channel",
                    new XElement("title", FeedTitle),
                    new XElement("link", FeedLink),
                    new XElement("description", FeedDescription),
                    new XElement("language", FeedLanguage)
                )
            )
        );
    }
}
