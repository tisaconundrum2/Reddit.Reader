"""
Creates a GitHub Release for the current run and uploads MP3 files as assets.
Uses the GitHub REST API via GITHUB_TOKEN.
"""

import os
from datetime import datetime, timezone
from pathlib import Path

import requests
from dotenv import load_dotenv

load_dotenv()

GITHUB_TOKEN = os.environ["GITHUB_TOKEN"]
GITHUB_REPO = os.environ["GITHUB_REPOSITORY"]  # e.g. "owner/repo", set by Actions
GITHUB_API = "https://api.github.com"


def _auth_headers() -> dict:
    return {
        "Authorization": f"Bearer {GITHUB_TOKEN}",
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
    }


def create_release(tag: str, name: str) -> dict:
    """Creates a new GitHub Release and returns the release JSON."""
    url = f"{GITHUB_API}/repos/{GITHUB_REPO}/releases"
    payload = {
        "tag_name": tag,
        "name": name,
        "body": f"Automated Reddit Reader — {name}",
        "draft": False,
        "prerelease": False,
    }
    resp = requests.post(url, headers=_auth_headers(), json=payload, timeout=30)
    resp.raise_for_status()
    return resp.json()


def upload_asset(upload_url: str, mp3_path: Path) -> str:
    """
    Uploads a single MP3 to a release's upload URL.
    Returns the browser_download_url of the uploaded asset.
    """
    # upload_url from the API looks like: https://uploads.github.com/repos/.../assets{?name,label}
    base_url = upload_url.split("{")[0]
    params = {"name": mp3_path.name, "label": mp3_path.stem}
    headers = {
        **_auth_headers(),
        "Content-Type": "audio/mpeg",
    }
    with mp3_path.open("rb") as f:
        resp = requests.post(
            base_url, headers=headers, params=params, data=f, timeout=120
        )
    resp.raise_for_status()
    return resp.json()["browser_download_url"]


def upload_mp3s(mp3_paths: list[Path]) -> dict[str, str]:
    """
    Creates a dated GitHub Release and uploads all MP3s.
    Returns {post_id: download_url} mapping.
    """
    now = datetime.now(timezone.utc)
    tag = f"run-{now.strftime('%Y%m%d-%H%M%S')}"
    name = f"Reddit Reader — {now.strftime('%Y-%m-%d %H:%M UTC')}"

    print(f"[release] Creating release '{name}' (tag: {tag})")
    release = create_release(tag, name)
    upload_url = release["upload_url"]

    urls: dict[str, str] = {}
    for mp3 in mp3_paths:
        post_id = mp3.stem
        print(f"[release] Uploading {mp3.name}...")
        download_url = upload_asset(upload_url, mp3)
        urls[post_id] = download_url
        print(f"[release]   -> {download_url}")

    return urls
