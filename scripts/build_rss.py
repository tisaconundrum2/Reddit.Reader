"""
Builds and updates the RSS feed (feed.xml) with new episode entries.
Each entry has an <enclosure> pointing to the GitHub Release MP3 asset URL.
"""

from datetime import datetime, timezone
from pathlib import Path

from feedgen.feed import FeedGenerator

FEED_FILE = Path(__file__).parent.parent / "feed.xml"
FEED_TITLE = "Reddit Reader — Automated Podcast"
FEED_LINK = "https://github.com"  # updated at runtime if GITHUB_REPOSITORY is set
FEED_DESCRIPTION = "Daily Reddit posts read aloud by Kokoro TTS, cleaned by Gemini."
FEED_LANGUAGE = "en"


def _make_generator(existing_entries: list[dict] | None = None) -> FeedGenerator:
    fg = FeedGenerator()
    fg.load_extension("podcast")
    fg.title(FEED_TITLE)
    fg.link(href=FEED_LINK, rel="alternate")
    fg.description(FEED_DESCRIPTION)
    fg.language(FEED_LANGUAGE)

    if existing_entries:
        for entry in existing_entries:
            fe = fg.add_entry(order="append")
            fe.id(entry["id"])
            fe.title(entry["title"])
            fe.description(entry.get("description", ""))
            fe.published(entry["published"])
            fe.enclosure(entry["url"], entry["length"], "audio/mpeg")

    return fg


def _load_existing_entries() -> list[dict]:
    """
    Parses existing feed.xml entries so they are preserved across runs.
    Uses a lightweight xml parse rather than pulling in a full rss parser.
    """
    import xml.etree.ElementTree as ET

    if not FEED_FILE.exists():
        return []

    try:
        tree = ET.parse(FEED_FILE)
        root = tree.getroot()
        channel = root.find("channel")
        if channel is None:
            return []

        entries = []
        for item in channel.findall("item"):
            guid = item.findtext("guid") or ""
            title = item.findtext("title") or ""
            desc = item.findtext("description") or ""
            pub_date = item.findtext("pubDate") or ""
            enclosure = item.find("enclosure")
            url = enclosure.get("url", "") if enclosure is not None else ""
            length = enclosure.get("length", "0") if enclosure is not None else "0"

            if guid and url:
                entries.append(
                    {
                        "id": guid,
                        "title": title,
                        "description": desc,
                        "published": pub_date,
                        "url": url,
                        "length": length,
                    }
                )
        return entries
    except ET.ParseError:
        return []


def add_episodes(posts: list[dict], mp3_urls: dict[str, str]) -> None:
    """
    Appends new episodes to feed.xml for posts that have a corresponding MP3 URL.

    posts: list of {"id", "title", "selftext", "subreddit"}
    mp3_urls: {post_id: download_url}
    """
    existing = _load_existing_entries()
    existing_ids = {e["id"] for e in existing}

    new_entries = []
    for post in posts:
        post_id = post["id"]
        if post_id not in mp3_urls or post_id in existing_ids:
            continue

        url = mp3_urls[post_id]
        pub = datetime.now(timezone.utc).isoformat()
        title = f"[r/{post['subreddit']}] {post['title']}"
        description = post.get("selftext", "")[:500]  # truncate for feed summary

        new_entries.append(
            {
                "id": post_id,
                "title": title,
                "description": description,
                "published": pub,
                "url": url,
                "length": "0",  # size unknown at this stage; acceptable for RSS enclosure
            }
        )

    if not new_entries:
        print("[rss] No new episodes to add.")
        return

    all_entries = existing + new_entries
    fg = _make_generator(all_entries)
    fg.rss_file(str(FEED_FILE), pretty=True)
    print(f"[rss] Updated {FEED_FILE} with {len(new_entries)} new episode(s).")


def add_episode(post: dict, download_url: str) -> None:
    """
    Adds a single episode to feed.xml immediately after its MP3 is uploaded.
    Safe to call repeatedly; skips if the post ID is already in the feed.
    """
    existing = _load_existing_entries()
    existing_ids = {e["id"] for e in existing}

    post_id = post["id"]
    if post_id in existing_ids:
        print(f"[rss] {post_id} already in feed, skipping.")
        return

    pub = datetime.now(timezone.utc).isoformat()
    entry = {
        "id": post_id,
        "title": f"[r/{post['subreddit']}] {post['title']}",
        "description": post.get("selftext", "")[:500],
        "published": pub,
        "url": download_url,
        "length": "0",
    }

    all_entries = existing + [entry]
    fg = _make_generator(all_entries)
    fg.rss_file(str(FEED_FILE), pretty=True)
    print(f"[rss] Added episode {post_id} to feed.")
