"""
Converts cleaned text to MP3 using Kokoro TTS (hexgrad/Kokoro-82M).
Outputs one MP3 file per post to the output/ directory.
"""

import io
from pathlib import Path

import numpy as np
import soundfile as sf
from kokoro import KPipeline

OUTPUT_DIR = Path(__file__).parent.parent / "output"
SAMPLE_RATE = 24000
DEFAULT_VOICE = "af_heart"

_pipeline: KPipeline | None = None


def _get_pipeline(lang_code: str = "a") -> KPipeline:
    global _pipeline
    if _pipeline is None:
        _pipeline = KPipeline(lang_code=lang_code)
    return _pipeline


def text_to_mp3(post_id: str, text: str, voice: str = DEFAULT_VOICE) -> Path:
    """
    Generates an MP3 from the given text and saves it to output/{post_id}.mp3.
    Returns the path to the generated file.
    """
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    out_path = OUTPUT_DIR / f"{post_id}.mp3"

    pipeline = _get_pipeline()
    chunks: list[np.ndarray] = []

    for _, _, audio in pipeline(text, voice=voice):
        if audio is not None and len(audio) > 0:
            chunks.append(audio)

    if not chunks:
        raise RuntimeError(f"Kokoro produced no audio for post {post_id}")

    full_audio = np.concatenate(chunks)

    # Write to an in-memory WAV buffer first, then save as MP3 via soundfile
    # soundfile writes MP3 natively when the extension is .mp3
    buf = io.BytesIO()
    sf.write(buf, full_audio, SAMPLE_RATE, format="WAV")
    buf.seek(0)

    # Re-read and write as MP3
    audio_data, _ = sf.read(buf)
    sf.write(str(out_path), audio_data, SAMPLE_RATE, format="MP3")

    print(f"[tts] Wrote {out_path} ({len(full_audio) / SAMPLE_RATE:.1f}s)")
    return out_path


if __name__ == "__main__":
    sample_text = (
        "Today I Learned that honey never spoils. "
        "Archaeologists have found three-thousand-year-old honey "
        "in Egyptian tombs that was still perfectly edible, "
        "due to its low moisture content and naturally occurring hydrogen peroxide."
    )
    path = text_to_mp3("test_post", sample_text)
    print(f"Output: {path}")
