import pymupdf

doc = pymupdf.open("C:\\Users\\airme\\Desktop\\ADM_ProfilDeCompetences.pdf")
filename = doc.name
page_count = []
page_number = 0

for page in doc: 
    text = page.get_text("text")
    page_number = page_number + 1
    current_page = page_number
    page_count.append((text, current_page, filename))
    print(text)
    

