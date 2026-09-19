from pathlib import Path

def pdf_to_raw_text(filepath):
    import pymupdf
    pages = []
    with pymupdf.open(filepath) as doc:
        for number, page in enumerate(doc, 1):
            # Sorting by y can interleave table columns. Validate native order.
            text = page.get_text('text', sort=False)
            if not text.strip():
                raise ValueError(f'Page {number} has no text; OCR/review required')
            pages.append((text, number, Path(filepath).name))
    return pages




