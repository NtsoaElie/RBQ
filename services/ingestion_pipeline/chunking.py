"""Structure-first RBQ parsing; returns typed Chunk objects."""
import hashlib
import re
from dataclasses import replace
from .models import Chunk, Competency

MODULE = re.compile(r'^Module\s+(\d+)\s*[–—:-]\s*(.+)', re.I)
NUMBER = re.compile(r'^(\d+(?:\.\d+)*)(?:\.\s*|\s+)(\S.*)')

def digest(value):
    return hashlib.sha256(value.encode('utf-8')).hexdigest()

def chunking(page_content, *, profile_id, document_id, source_type='profile',
             document_version=None, source_url=None, skill_ids=(), max_chars=2000) -> list[Chunk]:
    """Parse one document. Reference-to-skill mappings must be explicit.
    Character budget is not a token count; oversized semantic units are flagged.
    """
    pages = list(page_content)
    if not pages:
        return []
    if source_type not in ('profile', 'reference'):
        raise ValueError('source_type must be profile or reference')
    if not profile_id or not document_id or max_chars < 100:
        raise ValueError('IDs required; max_chars must be >= 100')
    if len({p[2] for p in pages}) != 1:
        raise ValueError('Parse each document separately')
    version = document_version or digest('\n'.join(p[0] for p in pages))
    chunks: list[Chunk] = []

    def emit(text, start, end, module=None, competency=None, skill=None):
        text = text.strip()
        if not text:
            return
        breadcrumb = [profile_id] + [v.label for v in (module, competency) if v]
        ids = [skill.id] if skill else list(skill_ids)
        issues = []
        if source_type == 'profile' and (not module or not competency):
            issues.append('missing_parent')
        if source_type == 'reference' and not ids:
            issues.append('unmapped_reference')
        if len(text) > max_chars:
            issues.append('oversized_unit')
        chunk = Chunk(chunk_id=digest(f'{document_id}|{version}|{start}|{end}|{ids}|{text}'),
                      document_id=document_id, document_version=version,
                      profile_id=profile_id, source_type=source_type,
                      source_url=source_url, filename=pages[0][2],
                      page_start=start, page_end=end,
                      module=module,
                      competency=competency,
                      skill=skill, skill_ids=ids,
                      breadcrumb=breadcrumb, content=text,
                      review_issues=issues, parser_version='rbq-v1')
        chunks.append(chunk)

    if source_type == 'reference':
        parts, start, end = [], None, None
        for text, page, _ in pages:
            for paragraph in re.split(r'\n\s*\n', text):
                paragraph = paragraph.strip()
                if not paragraph:
                    continue
                if parts and sum(map(len, parts)) + len(paragraph) > max_chars:
                    emit('\n\n'.join(parts), start, end)
                    parts = []
                if not parts:
                    start = page
                parts.append(paragraph)
                end = page
        emit('\n\n'.join(parts), start, end)
        return chunks

    module = competency = skill = None
    lines, start, end = [], None, None
    heading_lines = []
    summary = False

    def flush():
        if skill:
            emit('\n'.join(lines), start, end, module, competency, skill)

    for text, page, _ in pages:
        for raw in text.splitlines():
            line = raw.strip()
            if not line or line.isdigit() or line.casefold() == profile_id.casefold():
                continue
            if 'Éléments de compétence' in line or 'Habiletés requises' in line:
                summary = 'abordés' in line
                continue
            if line in ('Administration', 'Gestion de projets et de chantiers'):
                continue
            match = MODULE.match(line)
            if match:
                code, label = match.groups()
                if module and module.code == code:
                    continue
                flush()
                module = Competency(id=f'{profile_id}:module:{code}', code=code, label=label)
                competency = skill = None
                lines, heading_lines = [], []
                summary = False
                continue
            if summary:
                continue
            match = NUMBER.match(line)
            if match and module:
                code, label = match.groups()
                if '.' not in code:
                    flush()
                    competency = Competency(id=f'{module.id}:competency:{code}', code=code, label=label)
                    skill = None
                    lines, heading_lines = [], [label]
                else:
                    flush()
                    if competency and not code.startswith(competency.code + '.'):
                        competency = None
                    skill = Competency(id=f'{profile_id}:skill:{code}', code=code, label=label)
                    lines, start, end = [line], page, page
                continue
            if skill:
                lines.append(line)
                end = page
            elif competency:
                heading_lines.append(line)
                competency = replace(competency, label=' '.join(heading_lines))
    flush()
    seen = set()
    for chunk in chunks:
        key = chunk.skill.id
        if key in seen:
            chunk.review_issues.append('duplicate_skill')
        seen.add(key)
    return chunks

