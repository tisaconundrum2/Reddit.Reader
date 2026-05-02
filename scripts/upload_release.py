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


class ReleaseUploader:
    """
    Manages a single GitHub Release for the current pipeline run.
    Call create() once, then upload() for each MP3 as it's ready.
    """

    def __init__(self) -> None:
        self._upload_url: str | None = None
        self._release_tag: str | None = None

    def create(self) -> None:
        now = datetime.now(timezone.utc)
        tag = f"run-{now.strftime('%Y%m%d-%H%M%S')}"
        name = f"Reddit Reader — {now.strftime('%Y-%m-%d %H:%M UTC')}"

        print(f"[release] Creating release '{name}' (tag: {tag})")
        release = create_release(tag, name)
        self._upload_url = release["upload_url"]
        self._release_tag = tag
        print(f"[release] Release created: {release['html_url']}")

    def upload(self, mp3_path: Path) -> str:
        """
        Uploads a single MP3 to the release.
        Returns the browser_download_url.
        """
        if not self._upload_url:
            raise RuntimeError("Must call create() before upload()")
        return upload_asset(self._upload_url, mp3_path)


# ---------------------------------------------------------------------------
# Kept for backwards-compat if anything still imports upload_mp3s directly
# ---------------------------------------------------------------------------
def upload_mp3s(mp3_paths: list[Path]) -> dict[str, str]:
    uploader = ReleaseUploader()
    uploader.create()
    return {mp3.stem: uploader.upload(mp3) for mp3 in mp3_paths}
