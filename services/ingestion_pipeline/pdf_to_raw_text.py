import pymupdf

def pdf_to_raw_text(filepath):
    doc = pymupdf.open(filepath)
    filename = doc.name
    page_content = [] #page content
    page_number = 0 #page number counter

    for page in doc: 
        text = page.get_text("text")
        page_number = page_number + 1
        current_page = page_number
        page_content.append((text, current_page, filename))
        # print(repr(text))
    return page_content





