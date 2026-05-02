"""
Local test harness for each pipeline component.
Run individual tests with flags, or all of them in sequence.

Usage:
    python scripts/test_local.py --all
    python scripts/test_local.py --fetch
    python scripts/test_local.py --clean
    python scripts/test_local.py --tts
    python scripts/test_local.py --rss
    python scripts/test_local.py --upload        # requires GITHUB_TOKEN + GITHUB_REPOSITORY
    python scripts/test_local.py --pipeline      # runs full pipeline (real APIs)

Options:
    --subreddit SUBREDDIT   Subreddit to fetch from (default: MaliciousCompliance)
    --post-id   ID          Post ID to use for TTS/upload/RSS tests (default: test_post)
    --voice     VOICE       Kokoro voice to use (default: af_heart)
    --no-cleanup            Keep generated output files after tests
"""

import argparse
import json
import sys
import traceback
from pathlib import Path

# Ensure scripts/ is importable when run from repo root or scripts/
sys.path.insert(0, str(Path(__file__).parent))

REPO_ROOT = Path(__file__).parent.parent
OUTPUT_DIR = REPO_ROOT / "output"

SAMPLE_POST = {
    "id": "test_post",
    "subreddit": "MaliciousCompliance",
    "title": "TIL that honey never spoils",
    "selftext": (
        "Archaeologists have found 3000-year old honey in Egyptian tombs "
        "that was still perfectly edible!! due to its low moisture & "
        "naturally occurring hydrogen peroxide https://example.com"
    ),
}

SAMPLE_CLEANED = (
    "Today I Learned that honey never spoils. "
    "Archaeologists have found three-thousand-year-old honey in Egyptian tombs "
    "that was still perfectly edible, due to its low moisture content and "
    "naturally occurring hydrogen peroxide."
)

PASS = "\033[92mPASS\033[0m"
FAIL = "\033[91mFAIL\033[0m"
SKIP = "\033[93mSKIP\033[0m"


def _header(name: str) -> None:
    print(f"\n{'=' * 60}")
    print(f"  TEST: {name}")
    print(f"{'=' * 60}")


def _result(label: str, status: str, detail: str = "") -> None:
    suffix = f"  — {detail}" if detail else ""
    print(f"  [{status}] {label}{suffix}")


# ---------------------------------------------------------------------------
# Individual test functions
# ---------------------------------------------------------------------------

def test_fetch(subreddit: str = "MaliciousCompliance") -> list[dict]:
    _header("fetch_posts")
    try:
        from dotenv import load_dotenv
        load_dotenv()
        from fetch_posts import fetch_posts
        posts = fetch_posts(subreddit)
        _result(f"fetch_posts(r/{subreddit})", PASS, f"{len(posts)} new post(s) returned")
        if posts:
            p = posts[0]
            print(f"\n  First post preview:")
            print(f"    id      : {p['id']}")
            print(f"    title   : {p['title'][:70]}")
            print(f"    selftext: {p.get('selftext','')[:80]}...")
        return posts
    except KeyError as e:
        _result("fetch_posts", FAIL, f"Missing env var: {e}")
        return []
    except Exception as e:
        _result("fetch_posts", FAIL, str(e))
        traceback.print_exc()
        return []


def test_clean(post: dict | None = None) -> str | None:
    _header("clean_text")
    post = post or SAMPLE_POST
    try:
        from dotenv import load_dotenv
        load_dotenv()
        from clean_text import clean_post
        cleaned = clean_post(post["title"], post["selftext"])
        _result("clean_post", PASS, f"{len(cleaned)} chars returned")
        print(f"\n  Cleaned preview:\n    {cleaned[:200]}")
        return cleaned
    except KeyError as e:
        _result("clean_post", FAIL, f"Missing env var: {e}")
        return None
    except Exception as e:
        _result("clean_post", FAIL, str(e))
        traceback.print_exc()
        return None


def test_tts(post_id: str = "test_post", text: str = SAMPLE_CLEANED,
             voice: str = "af_heart", cleanup: bool = True) -> Path | None:
    _header("tts")
    try:
        from tts import text_to_mp3
        mp3_path = text_to_mp3(post_id, text, voice=voice)
        size_kb = mp3_path.stat().st_size / 1024
        _result("text_to_mp3", PASS, f"{mp3_path.name}  ({size_kb:.1f} KB)")
        if cleanup:
            mp3_path.unlink(missing_ok=True)
            print(f"  (cleaned up {mp3_path.name})")
        return mp3_path if not cleanup else None
    except Exception as e:
        _result("text_to_mp3", FAIL, str(e))
        traceback.print_exc()
        return None


def test_rss(post: dict | None = None, cleanup: bool = True) -> None:
    _header("build_rss")
    post = post or {**SAMPLE_POST, "id": "rss_test_post"}
    fake_url = "https://example.com/rss_test_post.mp3"
    feed_file = REPO_ROOT / "feed.xml"
    existed_before = feed_file.exists()

    try:
        from build_rss import add_episode
        add_episode(post, fake_url)
        assert feed_file.exists(), "feed.xml was not created"
        content = feed_file.read_text()
        assert fake_url in content, "MP3 URL missing from feed"
        assert post["id"] in content, "Post ID missing from feed"
        _result("add_episode", PASS, f"feed.xml updated, entry present")

        # Idempotency check
        add_episode(post, fake_url)
        _result("add_episode (duplicate)", PASS, "correctly skipped duplicate")

        if cleanup and not existed_before:
            feed_file.unlink()
            print("  (cleaned up feed.xml)")
    except Exception as e:
        _result("add_episode", FAIL, str(e))
        traceback.print_exc()


def test_upload(mp3_path: Path | None = None, cleanup: bool = True) -> str | None:
    _header("upload_release")
    import os
    from dotenv import load_dotenv
    load_dotenv()

    token = os.environ.get("GITHUB_TOKEN")
    repo = os.environ.get("GITHUB_REPOSITORY")

    if not token or not repo:
        missing = [v for v in ["GITHUB_TOKEN", "GITHUB_REPOSITORY"]
                   if not os.environ.get(v)]
        _result("upload_release", SKIP, f"Missing env vars: {', '.join(missing)}")
        return None

    # Create a tiny throwaway MP3 if none provided
    owned = False
    if mp3_path is None or not mp3_path.exists():
        try:
            from tts import text_to_mp3
            mp3_path = text_to_mp3("upload_test", "Upload test.", cleanup=False)  # type: ignore[call-arg]
        except TypeError:
            from tts import text_to_mp3
            mp3_path = text_to_mp3("upload_test", "Upload test.")
        owned = True

    try:
        from upload_release import ReleaseUploader
        uploader = ReleaseUploader()
        uploader.create()
        url = uploader.upload(mp3_path)
        _result("ReleaseUploader.create + upload", PASS, url)
        return url
    except Exception as e:
        _result("ReleaseUploader", FAIL, str(e))
        traceback.print_exc()
        return None
    finally:
        if owned and mp3_path and mp3_path.exists() and cleanup:
            mp3_path.unlink(missing_ok=True)


def test_pipeline() -> None:
    _header("run_pipeline (full, real APIs)")
    try:
        from dotenv import load_dotenv
        load_dotenv()
        from run_pipeline import run
        run()
        _result("run_pipeline.run()", PASS)
    except SystemExit as e:
        if e.code == 0 or e.code is None:
            _result("run_pipeline.run()", PASS)
        else:
            _result("run_pipeline.run()", FAIL, f"sys.exit({e.code})")
    except Exception as e:
        _result("run_pipeline.run()", FAIL, str(e))
        traceback.print_exc()


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Local test harness for Reddit.Reader pipeline components."
    )
    parser.add_argument("--fetch",    action="store_true", help="Test fetch_posts")
    parser.add_argument("--clean",    action="store_true", help="Test clean_text (Gemini)")
    parser.add_argument("--tts",      action="store_true", help="Test TTS (Kokoro)")
    parser.add_argument("--rss",      action="store_true", help="Test build_rss / feed.xml")
    parser.add_argument("--upload",   action="store_true", help="Test upload_release (GitHub)")
    parser.add_argument("--pipeline", action="store_true", help="Run full pipeline end-to-end")
    parser.add_argument("--all",      action="store_true",
                        help="Run fetch, clean, tts, rss, upload (skips pipeline)")
    parser.add_argument("--subreddit", default="MaliciousCompliance",
                        help="Subreddit for --fetch test (default: MaliciousCompliance)")
    parser.add_argument("--post-id",   default="test_post",
                        dest="post_id",
                        help="Post ID for TTS/RSS/upload tests (default: test_post)")
    parser.add_argument("--voice",     default="af_heart",
                        help="Kokoro voice for TTS test (default: af_heart)")
    parser.add_argument("--no-cleanup", action="store_true", dest="no_cleanup",
                        help="Keep generated files after tests")
    args = parser.parse_args()

    if not any([args.fetch, args.clean, args.tts, args.rss,
                args.upload, args.pipeline, args.all]):
        parser.print_help()
        sys.exit(0)

    cleanup = not args.no_cleanup
    run_all = args.all

    if run_all or args.fetch:
        test_fetch(args.subreddit)

    if run_all or args.clean:
        test_clean()

    if run_all or args.tts:
        test_tts(post_id=args.post_id, voice=args.voice, cleanup=cleanup)

    if run_all or args.rss:
        test_rss(cleanup=cleanup)

    if run_all or args.upload:
        test_upload(cleanup=cleanup)

    if args.pipeline:
        test_pipeline()

    print()


if __name__ == "__main__":
    main()
