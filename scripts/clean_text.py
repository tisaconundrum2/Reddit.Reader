"""
Cleans and grammar-fixes Reddit post text using Gemini,
making it suitable for text-to-speech narration.
"""

import os

from google import genai
from dotenv import load_dotenv

load_dotenv()

_client = genai.Client(api_key=os.environ["GEMINI_API_KEY"])

_MODEL = "gemini-1.5-flash"
_SYSTEM_PROMPT = (
    "You are preparing text for a text-to-speech podcast narrator. "
    "Fix grammar, punctuation, and spelling. Remove markdown formatting, "
    "URLs, and anything that would sound awkward when read aloud. "
    "Expand abbreviations where sensible. Return only the cleaned text, "
    "no explanations, no introductions, no meta-commentary."
)


def clean_post(title: str, selftext: str) -> str:
    """
    Sends the post title and body to Gemini for narration cleanup.
    Returns a single cleaned string ready for TTS.
    """
    raw = f"Title: {title}\n\n{selftext}" if selftext else f"Title: {title}"
    full_prompt = f"{_SYSTEM_PROMPT}\n\n{raw}"

    response = _client.models.generate_content(
        model=_MODEL,
        contents=full_prompt,
    )
    return response.text.strip()


if __name__ == "__main__":
    sample_title = "TIL that honey never spoils"
    sample_body = (
        "Archaeologists have found 3000-year old honey in Egyptian tombs "
        "that was still perfectly edible!! due to its low moisture & "
        "naturally occurring hydrogen peroxide https://example.com"
    )
    print(clean_post(sample_title, sample_body))
