# Reddit Reader

An automated podcast-style Reddit reader built with **.NET 10**. On a configurable schedule, posts are fetched from subreddits, grammar-cleaned with Gemini, converted to MP3 by a local Kokoro TTS sidecar, and published to a self-updating RSS feed.

## Architecture

| Component | Technology | Role |
|---|---|---|
| `Reddit.Reader.Builder` | .NET 10 Worker Service | Orchestrates the full pipeline |
| `Kokoro-TTS` | Python / Flask | Local TTS HTTP server (`POST /tts`) |

## How It Works

1. **Fetch** — Pulls posts from configured subreddits via the Reddit3 RapidAPI endpoint
2. **Deduplicate** — Checks `seen_ids.json` / catalog so already-processed posts are skipped
3. **Clean** — Sends each post through Gemini to fix grammar and make it narration-ready
4. **TTS** — POSTs cleaned text to the Kokoro TTS sidecar and saves the returned MP3 locally
5. **RSS** — Appends a new `<item>` (with `<enclosure>`) to `feed.xml`
6. **Catalog** — Records the post in `seen_ids.json` to prevent future duplicates

---

## Requirements

### .NET Worker (`Reddit.Reader.Builder`)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Kokoro TTS sidecar (`Kokoro-TTS/`)
- Python 3.11+
- `espeak-ng`
  ```bash
  # macOS
  brew install espeak-ng

  # Ubuntu/Debian
  sudo apt-get install -y espeak-ng
  ```
- Python dependencies
  ```bash
  pip install -r Kokoro-TTS/requirements.txt
  ```

---

## Configuration

All settings live in `appsettings.json` / `appsettings.Development.json`. Secrets are kept out of source via **dotnet user-secrets** (see below).

| Key | Description | Default |
|---|---|---|
| `Reddit:Subreddits` | Array of subreddit names to read | `["MaliciousCompliance"]` |
| `Reddit:Filter` | Feed filter (`hot`, `new`, `top`) | `hot` |
| `Gemini:Model` | Gemini model name | `gemini-2.0-flash` |
| `KokoroTts:BaseUrl` | URL of the running TTS sidecar | `http://localhost:5000` |
| `KokoroTts:Voice` | Voice ID | `af_heart` |
| `KokoroTts:Speed` | Playback speed (0.5 – 2.0) | `1.0` |
| `Pipeline:OutputDir` | Directory for generated MP3s | `output` |
| `Pipeline:FeedFile` | RSS feed file path | `feed.xml` |
| `Pipeline:FeedBaseUrl` | Public base URL for enclosure links | _(local path used if empty)_ |
| `Pipeline:RunIntervalHours` | Hours between pipeline runs | `24` |
| `Pipeline:PostLimit` | Max posts to process per run | `1` |

---

## API Keys / Secrets

Store secrets with dotnet user-secrets (already initialised in the project):

```bash
cd Reddit.Reader.Builder

dotnet user-secrets set "REDDIT_API_KEY" "<your-key>"
dotnet user-secrets set "GEMINI_API_KEY" "<your-key>"
dotnet user-secrets set "GITHUB_TOKEN"   "<your-pat>"
dotnet user-secrets set "GITHUB_REPOSITORY" "username/Reddit.Reader"
```

| Secret | Where to get it |
|---|---|
| `REDDIT_API_KEY` | [rapidapi.com](https://rapidapi.com) — subscribe to the Reddit3 API |
| `GEMINI_API_KEY` | [aistudio.google.com](https://aistudio.google.com) |
| `GITHUB_TOKEN` | GitHub → Settings → Developer settings → Personal access tokens — **Contents: Read and write** |
| `GITHUB_REPOSITORY` | `username/Reddit.Reader` |

---

## Running Locally

**1. Start the Kokoro TTS sidecar**
```bash
cd Kokoro-TTS
python app.py
# Listening on http://0.0.0.0:5000
```

**2. Run the .NET worker**
```bash
cd Reddit.Reader.Builder
dotnet run
```

Output MP3s are written to `output/`. `feed.xml` and `seen_ids.json` are updated in the project directory.

---

## Running with Docker

Each component has its own `Dockerfile`. Start both together:

```bash
docker compose up --build
```

---

## Output

- `feed.xml` — RSS feed updated after each run. Subscribe in any podcast app that supports RSS.
- `seen_ids.json` — Tracks processed post IDs to prevent duplicates across runs.
- `output/` — Generated MP3 files (one per post).
