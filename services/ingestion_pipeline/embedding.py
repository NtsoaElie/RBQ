from openai import OpenAI
from dotenv import load_dotenv
import os

load_dotenv() #load variables from .env file

my_api_key = os.getenv("OPEN_API_KEY")
client = OpenAI(api_key=my_api_key)

# response = client.embeddings.create(
#     input="The food was delicious and the waiter...", model="text-embedding-3-small"
# )

def embed(my_input):
    response = client.embeddings.create(
        input=my_input, model="text-embedding-3-small"
    )
    return response.data[0].embedding

# print(response.data[0].embedding)