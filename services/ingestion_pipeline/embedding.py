"""Lazy initialization allows extraction without credentials."""
import os

def get_client():
    from dotenv import load_dotenv
    from openai import OpenAI
    load_dotenv()
    key = os.getenv('OPENAI_API_KEY') or os.getenv('OPEN_API_KEY')
    if not key:
        raise ValueError('Set OPENAI_API_KEY in your environment or .env')
    return OpenAI(api_key=key)

def embed_many(texts, *, client=None, batch_size=64):
    texts = list(texts)
    if batch_size < 1 or any(not t.strip() for t in texts):
        raise ValueError('Positive batch size and nonempty texts required')
    if not texts:
        return []
    client = client or get_client()
    vectors = []
    for offset in range(0, len(texts), batch_size):
        batch = texts[offset:offset + batch_size]
        result = client.embeddings.create(input=batch, model='text-embedding-3-small')
        items = sorted(result.data, key=lambda item: item.index)
        if [i.index for i in items] != list(range(len(batch))):
            raise ValueError('Missing or duplicate embedding indices')
        if any(len(i.embedding) != 1536 for i in items):
            raise ValueError('Expected 1536-dimensional vectors')
        vectors.extend(i.embedding for i in items)
    return vectors

def embed(my_input, *, client=None):
    return embed_many([my_input], client=client)[0]
