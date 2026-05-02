"""
Orchestrator: fetch → (per post: clean → tts → upload → rss) 
Processes and persists each post incrementally so a failure
only loses unprocessed posts, not already-completed ones.
"""

import sys
from pathlib import Path

from dotenv import load_dotenv

load_dotenv()

# Ensure scripts/ is on the path when run from repo root
sys.path.insert(0, str(Path(__file__).parent))

from build_rss import add_episode  # changed: singular, per-post
from clean_text import clean_post
from fetch_posts import fetch_all_subreddits
from tts import text_to_mp3
from upload_release import ReleaseUploader  # changed: stateful uploader

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

    # Create a single release upfront so all MP3s go into one release
    print("\n=== Creating GitHub Release ===")
    uploader = ReleaseUploader()
    uploader.create()

    succeeded = 0
    failed = 0

    for i, post in enumerate(posts, 1):
        post_id = post["id"]
        print(f"\n--- Post {i}/{len(posts)}: {post_id} ---")
        print(f"    {post['title'][:70]}")

        # 2. Clean
        try:
            print("  [clean] Cleaning text...")
            post["cleaned_text"] = clean_post(post["title"], post["selftext"])
        except Exception as e:
            print(f"  [clean] FAILED: {e}")
            failed += 1
            continue

        # 3. TTS
        try:
            print("  [tts] Generating MP3...")
            mp3_path = text_to_mp3(post_id, post["cleaned_text"])
        except Exception as e:
            print(f"  [tts] FAILED: {e}")
            failed += 1
            continue

        # 4. Upload
        try:
            print("  [upload] Uploading to GitHub Release...")
            download_url = uploader.upload(mp3_path)
            print(f"  [upload] -> {download_url}")
        except Exception as e:
            print(f"  [upload] FAILED: {e}")
            failed += 1
            continue

        # 5. Immediately update RSS — persisted after each post
        try:
            print("  [rss] Updating feed...")
            add_episode(post, download_url)
        except Exception as e:
            print(f"  [rss] FAILED: {e}")
            # Not counting as failed since audio is already uploaded
            # and the URL is logged above for manual recovery if needed

        succeeded += 1

    print("\n=== Done ===")
    print(f"  Succeeded : {succeeded}")
    print(f"  Failed    : {failed}")

    if succeeded == 0:
        sys.exit(1)


if __name__ == "__main__":
    run()
