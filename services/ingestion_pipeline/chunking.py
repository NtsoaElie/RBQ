import re
from pdf_to_raw_text import pdf_to_raw_text

# page_content = pdf_to_raw_text("C:\\Users\\airme\\Desktop\\ADM_ProfilDeCompetences.pdf")

def chunking(page_content):
   chunks = []
   current_chunk = ""  
   for (text, page_number, filename) in page_content:
         lines = text.splitlines()

         for line in lines: 
            is_top_level = re.search(r'^\d+\.[^\d]', line) #check if top level section
            if is_top_level: #if its top level that means a new chunk has started
               chunks.append((current_chunk, page_number, filename)) # we then append the current chunk to chunks[]
               current_chunk = "" # then we reset current_chunk since top level section means a new chunk has started
            else: 
               current_chunk = current_chunk + line #if it's not a top level section that means its sub level or just plain text, we add it to current chunk
   chunks.append((current_chunk, page_number, filename)) # append the current_chunk to chunks[]
   print(len(chunks))
   print(chunks) 
   return chunks



