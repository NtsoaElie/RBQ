"""Domain objects; dictionaries are used only at JSON/database boundaries."""
from dataclasses import asdict, dataclass, field
from typing import Any, Literal


@dataclass(frozen=True, slots=True)
class Competency:
    id: str
    code: str
    label: str


@dataclass(slots=True, kw_only=True)
class Chunk:
    chunk_id: str
    document_id: str
    document_version: str
    profile_id: str
    source_type: Literal['profile', 'reference']
    filename: str
    page_start: int
    page_end: int
    content: str
    source_url: str | None = None
    module: Competency | None = None
    competency: Competency | None = None
    skill: Competency | None = None
    skill_ids: list[str] = field(default_factory=list)
    breadcrumb: list[str] = field(default_factory=list)
    review_issues: list[str] = field(default_factory=list)
    parser_version: str = 'rbq-v1'

    @property
    def page_number(self) -> int:
        return self.page_start

    @property
    def embedding_text(self) -> str:
        return ' > '.join(self.breadcrumb) + '\n' + self.content

    def to_dict(self) -> dict[str, Any]:
        """Keep the existing JSON/Supabase shape, including derived fields."""
        data = asdict(self)
        data['page_number'] = self.page_number
        data['embedding_text'] = self.embedding_text
        return data

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> 'Chunk':
        """Rehydrate nested objects from exported chunk JSON."""
        values = dict(data)
        # Always derive these from the actual source fields.
        values.pop('page_number', None)
        values.pop('embedding_text', None)
        for name in ('module', 'competency', 'skill'):
            node = values.get(name)
            values[name] = Competency(**node) if node is not None else None
        for name in ('skill_ids', 'breadcrumb', 'review_issues'):
            if name in values:
                values[name] = list(values[name])
        return cls(**values)
