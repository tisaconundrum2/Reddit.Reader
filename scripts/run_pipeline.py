"""
Orchestrator: fetch → clean → tts → upload → rss
Run this script directly or via GitHub Actions.
"""

import sys
from pathlib import Path

from dotenv import load_dotenv

load_dotenv()

# Ensure scripts/ is on the path when run from repo root
sys.path.insert(0, str(Path(__file__).parent))

from build_rss import add_episodes
from clean_text import clean_post
from fetch_posts import fetch_all_subreddits
from tts import text_to_mp3
from upload_release import upload_mp3s

SUBREDDITS_FILE = Path(__file__).parent.parent / "subreddits.txt"
OUTPUT_DIR = Path(__file__).parent.parent / "output"
POSTS_PER_SUBREDDIT = 5


def run() -> None:
    # 1. Fetch new posts
    print("=== Step 1: Fetch posts ===")
    posts = fetch_all_subreddits(SUBREDDITS_FILE, limit=POSTS_PER_SUBREDDIT)
    if not posts:
        print("No new posts found. Exiting.")
        return
    print(f"Total new posts: {len(posts)}")

    # 2. Clean text with Gemini
    print("\n=== Step 2: Clean text with Gemini ===")
    for post in posts:
        print(f"[clean] {post['id']} — {post['title'][:60]}")
        post["cleaned_text"] = clean_post(post["title"], post["selftext"])

    # 3. Generate MP3s with Kokoro TTS
    print("\n=== Step 3: Generate MP3s ===")
    mp3_paths: list[Path] = []
    failed_ids: set[str] = set()
    for post in posts:
        try:
            path = text_to_mp3(post["id"], post["cleaned_text"])
            mp3_paths.append(path)
        except Exception as e:
            print(f"[tts] FAILED {post['id']}: {e}")
            failed_ids.add(post["id"])

    if not mp3_paths:
        print("All TTS conversions failed. Exiting.")
        sys.exit(1)

    # 4. Upload MP3s to a GitHub Release
    print("\n=== Step 4: Upload to GitHub Release ===")
    mp3_urls = upload_mp3s(mp3_paths)

    # 5. Update RSS feed
    print("\n=== Step 5: Update RSS feed ===")
    successful_posts = [p for p in posts if p["id"] not in failed_ids]
    add_episodes(successful_posts, mp3_urls)

    print("\n=== Done ===")
    print(f"  Posts processed : {len(successful_posts)}")
    print(f"  MP3s uploaded   : {len(mp3_urls)}")
    print(f"  Posts failed    : {len(failed_ids)}")


if __name__ == "__main__":
    run()
