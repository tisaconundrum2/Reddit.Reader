"""
Fetches posts from Reddit via RapidAPI (reddit3.p.rapidapi.com).
API call format: ?url=<encoded reddit URL>&filter=hot
Response format: body[] array with postId, title, selfText fields.
Deduplicates against seen_ids.json to avoid re-reading posts.
"""

import json
import os
from pathlib import Path
from urllib.parse import quote

import requests
from dotenv import load_dotenv

load_dotenv()

RAPIDAPI_KEY = os.environ["REDDIT_API_KEY"]
RAPIDAPI_HOST = "reddit3.p.rapidapi.com"
API_URL = "https://reddit3.p.rapidapi.com/v1/reddit/posts"
REDDIT_BASE = "https://www.reddit.com/r/"
SEEN_IDS_FILE = Path(__file__).parent.parent / "seen_ids.json"


def _load_seen_ids() -> set:
    if SEEN_IDS_FILE.exists():
        with SEEN_IDS_FILE.open() as f:
            return set(json.load(f))
    return set()


def _save_seen_ids(seen: set) -> None:
    with SEEN_IDS_FILE.open("w") as f:
        json.dump(sorted(seen), f, indent=2)


def fetch_posts(subreddit: str, filter: str = "hot") -> list[dict]:
    """
    Returns a list of new (unseen) posts from the given subreddit.
    Each item: {"id": str, "subreddit": str, "title": str, "selftext": str}
    """
    reddit_url = f"{REDDIT_BASE}{subreddit}/"
    headers = {
        "x-rapidapi-key": RAPIDAPI_KEY,
        "x-rapidapi-host": RAPIDAPI_HOST,
    }
    params = {
        "url": reddit_url,
        "filter": filter,
    }

    response = requests.get(API_URL, headers=headers, params=params, timeout=30)
    print(f"[fetch] r/{subreddit} — HTTP {response.status_code}")
    if not response.text.strip():
        print(f"[fetch] Empty response body for r/{subreddit}, skipping.")
        return []
    response.raise_for_status()
    try:
        data = response.json()
    except Exception:
        print(f"[fetch] Non-JSON response for r/{subreddit}: {response.text[:200]}")
        return []

    # Response shape: { "meta": {...}, "body": [ { "id": ..., "title": ..., "selftext": ... }, ... ] }
    posts_raw = data.get("body") or []
    seen = _load_seen_ids()
    new_posts = []

    for post in posts_raw:
        post_id = (post.get("id") or "").strip()
        title = (post.get("title") or "").strip()
        selftext = (post.get("selftext") or "").strip()

        if not post_id or post_id in seen or not title:
            continue

        new_posts.append(
            {
                "id": post_id,
                "subreddit": subreddit,
                "title": title,
                "selftext": selftext,
            }
        )
        seen.add(post_id)

    _save_seen_ids(seen)
    return new_posts


def fetch_all_subreddits(subreddits_file: Path, limit: int = 5) -> list[dict]:
    """
    Reads subreddits from a file (one per line) and fetches new posts from each.
    limit is ignored (the API returns a fixed page); kept for interface compatibility.
    """
    subreddits = [
        line.strip()
        for line in subreddits_file.read_text().splitlines()
        if line.strip() and not line.startswith("#")
    ]

    all_posts = []
    for subreddit in subreddits:
        print(f"[fetch] Fetching r/{subreddit}...")
        try:
            posts = fetch_posts(subreddit)
            print(f"[fetch]   {len(posts)} new post(s)")
            all_posts.extend(posts)
        except requests.HTTPError as e:
            print(f"[fetch]   ERROR r/{subreddit}: {e}")

    return all_posts


if __name__ == "__main__":
    subreddits_path = Path(__file__).parent.parent / "subreddits.txt"
    posts = fetch_all_subreddits(subreddits_path)
    print(json.dumps(posts, indent=2))
