import re
from pdf_to_raw_text import pdf_to_raw_text
page_content = pdf_to_raw_text("C:\\Users\\airme\\Desktop\\ADM_ProfilDeCompetences.pdf")
chunks = []
current_chunk = ""

for (text, page_number, filename) in page_content:
 lines = text.splitlines()

 for line in lines: 
    #check if line is top level or sub level
     is_top_level = re.search(r'^\d+\.[^\d]', line)
     if is_top_level:
        chunks.append(current_chunk)
        current_chunk = ""
     else: 
        current_chunk = current_chunk + line
chunks.append(current_chunk)


print(len(chunks))
print(chunks)




#chunk builder checker

#chunk array