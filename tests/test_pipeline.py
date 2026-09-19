import json
import unittest
from dataclasses import FrozenInstanceError
from types import SimpleNamespace
from services.ingestion_pipeline.chunking import chunking
from services.ingestion_pipeline.models import Chunk, Competency
from services.ingestion_pipeline.embedding import embed_many
from services.ingestion_pipeline.vector_store_to_supabase import upload_chunks


class PipelineTests(unittest.TestCase):
    def test_chunk_json_round_trip(self):
        chunk = self.fixture()[0]
        self.assertIsInstance(chunk, Chunk)
        self.assertIsInstance(chunk.skill, Competency)
        payload = json.loads(json.dumps(chunk.to_dict()))
        restored = Chunk.from_dict(payload)
        self.assertEqual(restored, chunk)
        self.assertEqual(restored.page_number, chunk.page_start)
        restored.review_issues.append('test')
        self.assertEqual(chunk.review_issues, [])
        self.assertEqual(payload['review_issues'], [])
        with self.assertRaises(FrozenInstanceError):
            restored.skill.label = 'Changed'

    def test_reference_json_round_trip(self):
        chunk = chunking([('Reference text', 1, 'ref')], profile_id='ADM',
                         document_id='ref', source_type='reference')[0]
        restored = Chunk.from_dict(json.loads(json.dumps(chunk.to_dict())))
        self.assertEqual(restored, chunk)
        self.assertIsNone(restored.skill)
        restored.content = 'Updated text'
        self.assertTrue(restored.embedding_text.endswith('Updated text'))

    def test_number_without_space(self):
        rows = self.parse([('Module 2 – Management\n10.Planifier\n10.1.Définir une stratégie.', 10, 'a.pdf')])
        self.assertEqual(rows[0].competency.code, '10')
        self.assertEqual(rows[0].skill.code, '10.1')


    def parse(self, pages):
        return chunking(pages, profile_id='ADM', document_id='adm-profile')

    def fixture(self):
        return self.parse([('Module 1 – Finance\nÉléments de compétence Habiletés requises\n1. Le bilan\n1.1. Expliquer le bilan.', 2, 'a.pdf')])

    def test_empty(self):
        self.assertEqual(self.parse([]), [])

    def test_hierarchy_multiline_and_pages(self):
        result = self.parse([
            ('Module 1 – Finance\n1. Comprendre\nle bilan\n1.1. Expliquer\n• les actifs', 6, 'a.pdf'),
            ('Administration\n7\nModule 1 – Finance\nHabiletés requises\n• les passifs\n1.2. Calculer le solde.', 7, 'a.pdf')])
        self.assertEqual(len(result), 2)
        self.assertEqual(result[0].competency.label, 'Comprendre le bilan')
        self.assertEqual((result[0].page_start, result[0].page_end), (6, 7))
        self.assertIn('• les actifs\n• les passifs', result[0].content)
        self.assertNotIn('Administration', result[0].content)
        self.assertEqual(result[1].skill.id, 'ADM:skill:1.2')

    def test_summary_not_parsed(self):
        records = self.parse([('Module 1 – Finance\nÉléments de compétence abordés dans ce module :\n1. Résumé\nModule 1 – Finance\nÉléments de compétence Habiletés requises\n1. Bilan\n1.1. Calculer.', 1, 'a.pdf')])
        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].competency.label, 'Bilan')

    def test_stable_skill_versioned_chunk(self):
        first = self.fixture()[0]
        second = self.parse([('Module 1 – Finance\n1. Le bilan\n1.1. Nouveau texte.', 3, 'a.pdf')])[0]
        self.assertEqual(first.skill.id, second.skill.id)
        self.assertNotEqual(first.chunk_id, second.chunk_id)

    def test_missing_parent_and_duplicate_flagged(self):
        records = self.parse([('Module 1 – Finance\n1.1. Orphelin\n1.1. Doublon', 1, 'a.pdf')])
        self.assertIn('missing_parent', records[0].review_issues)
        self.assertIn('duplicate_skill', records[1].review_issues)

    def test_documents_cannot_mix(self):
        with self.assertRaises(ValueError):
            self.parse([('one', 1, 'a'), ('two', 1, 'b')])


    def test_reference_boundaries(self):
        records = chunking([('A' * 80 + '\n\n' + 'B' * 80, 2, 'ref'), ('C' * 120, 3, 'ref')],
                           profile_id='ADM', document_id='ref', source_type='reference', max_chars=100)
        self.assertEqual(len(records), 3)
        self.assertIn('oversized_unit', records[-1].review_issues)
        self.assertIn('unmapped_reference', records[0].review_issues)

    def test_embedding_empty_and_order(self):
        self.assertEqual(embed_many([]), [])
        client = SimpleNamespace(embeddings=SimpleNamespace(create=lambda **kwargs: SimpleNamespace(
            data=[SimpleNamespace(index=1, embedding=[2]*1536), SimpleNamespace(index=0, embedding=[1]*1536)])))
        self.assertEqual(embed_many(['a', 'b'], client=client)[0][0], 1)
        with self.assertRaises(ValueError):
            embed_many([' '], client=client)



    def test_upload_metadata_and_conflict_key(self):
        calls = []
        class Table:
            def upsert(self, rows, **kwargs):
                calls.append((rows, kwargs))
                return self
            def execute(self):
                return None
        client = SimpleNamespace(table=lambda name: Table())
        api = SimpleNamespace(embeddings=SimpleNamespace(create=lambda **kwargs: SimpleNamespace(
            data=[SimpleNamespace(index=0, embedding=[0]*1536)])))
        self.assertEqual(upload_chunks(self.fixture(), client=client, embedding_client=api), 1)
        self.assertEqual(calls[0][1]['on_conflict'], 'chunk_id')
        self.assertEqual(calls[0][0][0]['metadata']['skill']['id'], 'ADM:skill:1.1')


if __name__ == '__main__':
    unittest.main()
