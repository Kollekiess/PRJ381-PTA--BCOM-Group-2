import os
from dotenv import load_dotenv

load_dotenv()

print(os.getenv("SUPABASE_URL"))
print(os.getenv("SUPABASE_ANON_KEY")[:20])