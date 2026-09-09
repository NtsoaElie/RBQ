from supabase import create_client, Client
from dotenv import load_dotenv
import os

load_dotenv()

key = os.getenv("SUPABASE_KEY")
url = os.getenv("SUPABASE_URL")

supabase: Client = create_client(url, key)
response = supabase.table("document_chunks").select("*").execute()
print(response)
