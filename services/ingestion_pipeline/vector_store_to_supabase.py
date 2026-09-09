from supabase import create_client, Client
from dotenv import load_dotenv
from pdf_to_raw_text import pdf_to_raw_text
from chunking import chunking
from embedding import embed
import os

load_dotenv()

key = os.getenv("SUPABASE_PRIVATE_KEY")
url = os.getenv("SUPABASE_URL")

supabase: Client = create_client(url, key)

chunks = chunking(pdf_to_raw_text("C:\\Users\\airme\\Desktop\\ADM_ProfilDeCompetences.pdf"))
for (chunk_text, page_number, filename) in chunks:
    vector = embed(chunk_text)
    data = supabase.table("document_chunks").insert({
        "content": chunk_text, "embedding": vector, "filename": filename, "page_number": page_number
    }).execute()
    


