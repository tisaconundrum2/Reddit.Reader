# Reddit Reader

An automated podcast-style Reddit reader. Every 24 hours, new posts are fetched from configurable subreddits, grammar-cleaned with Gemini, converted to MP3 with Kokoro TTS, uploaded as GitHub Release assets, and published to a self-updating RSS feed.

## How It Works

1. **Fetch** — Pulls top posts from subreddits listed in `subreddits.txt` via RapidAPI
2. **Clean** — Sends each post through Gemini to fix grammar and make it narration-ready
3. **TTS** — Converts cleaned text to MP3 using Kokoro-82M (local, CPU)
4. **Release** — Uploads MP3s as assets to a new dated GitHub Release
5. **RSS** — Appends new episodes (with enclosure URLs) to `feed.xml` and commits it back to the repo

---

## Requirements

### System
- Python 3.11+
- `espeak-ng` — required by Kokoro TTS
  ```bash
  # macOS
  brew install espeak-ng

  # Ubuntu/Debian
  sudo apt-get install -y espeak-ng
  ```

### Python packages
```bash
pip install -r requirements.txt
```

### API Keys

Copy `.env` and fill in all values:

| Variable | Where to get it |
|---|---|
| `REDDIT_API_KEY` | [rapidapi.com](https://rapidapi.com) — subscribe to the Reddit3 API |
| `GEMINI_API_KEY` | [aistudio.google.com](https://aistudio.google.com) |
| `GITHUB_TOKEN` | GitHub → Settings → Developer settings → Personal access tokens (Fine-grained) — **Contents: Read and write** on this repo |
| `GITHUB_REPOSITORY` | Your `username/Reddit.Reader` |

---

## Configuration

Edit `subreddits.txt` to control which subreddits are read — one per line, `#` for comments:

```
AskReddit
todayilearned
technology
# worldnews
```

---

## Running Locally

```bash
python scripts/run_pipeline.py
```

Output MP3s are written to `output/` (git-ignored). `feed.xml` and `seen_ids.json` are updated in the repo root.

---

## GitHub Actions (Automated)

The workflow runs daily at **06:00 UTC** via CRON. It can also be triggered manually from the Actions tab.

### Secrets to add in GitHub repo settings

Go to **Settings → Secrets and variables → Actions → New repository secret**:

| Secret | Value |
|---|---|
| `REDDIT_API_KEY` | Your RapidAPI key |
| `GEMINI_API_KEY` | Your Gemini API key |

`GITHUB_TOKEN` is provided automatically — no setup needed.

### Activating the workflow

The workflow only runs on `main`. When ready, merge `dev` → `main` via a pull request.

---

## Output

- `feed.xml` — RSS feed committed to repo root after each run. Subscribe to it in any podcast app that supports RSS.
- GitHub Releases — each run creates a release tagged `run-YYYYMMDD-HHMMSS` with all MP3s attached.
- `seen_ids.json` — tracks processed post IDs to prevent duplicates across runs.
