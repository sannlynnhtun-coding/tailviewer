"""Disposable synthetic-store probe. Raw schema snapshot is evidence, not the P10 import contract."""
import base64
import hashlib
import json
import sqlite3
import sys
from pathlib import Path

root = Path(sys.argv[1]).resolve()

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def quote(name):
    return '"' + name.replace('"', '""') + '"'

def encode(value):
    return {"blob": base64.b64encode(value).decode("ascii")} if isinstance(value, bytes) else value

def snapshot(path):
    with sqlite3.connect(path.as_uri() + '?mode=ro', uri=True) as db:
        db.execute('PRAGMA query_only=ON')
        tables = db.execute("SELECT name,sql FROM sqlite_master WHERE type='table' ORDER BY name").fetchall()
        return [{"name": name, "sql": sql, "rows": [[encode(v) for v in row] for row in db.execute('SELECT * FROM ' + quote(name) + ' ORDER BY rowid')]} for name, sql in tables]

source = root / 'legacy-valid.db'  # Deliberate NULL-icon corpus.
before = digest(source)
original = snapshot(source)
neutral = root / 'neutral-raw-v1.json'
neutral.write_text(json.dumps({"schema": 1, "tables": original}, sort_keys=True), encoding='utf-8')
payload = json.loads(neutral.read_text(encoding='utf-8'))
assert payload['schema'] == 1
restored = root / 'neutral-reopened.db'
assert not restored.exists(), 'Refusing overwrite'
with sqlite3.connect(restored) as db:
    for table in payload['tables']:
        db.execute(table['sql'])
        for row in table['rows']:
            values = [base64.b64decode(v['blob']) if isinstance(v, dict) else v for v in row]
            db.execute('INSERT INTO ' + quote(table['name']) + ' VALUES (' + ','.join('?' for _ in row) + ')', values)
assert snapshot(restored) == original, 'Raw schema/row/null/blob mismatch'
assert digest(source) == before, 'Source modified'
assert any(row[1] is None for table in original if table['name'] == 'ObjectStore_3' for row in table['rows']), 'NULL fixture missing'
print('PASS raw neutral export/reopen; all schema, ordered rows, NULL and BLOB values equal')
print('source_sha256=' + before)
print('snapshot_sha256=' + digest(neutral))
for table in original:
    print(table['name'] + ' count=' + str(len(table['rows'])))

for name in ('users', 'tokens', 'packages', 'icons', 'descriptions'):
    legacy = root / 'legacy-empty-export' / (name + '.xml')
    modern = root / 'modern-empty-export' / (name + '.xml')
    assert legacy.read_bytes() == modern.read_bytes(), name + ' cross-runtime mismatch'
    print('PASS cross-runtime ' + name + ' sha256=' + digest(legacy))
assert digest(root / 'legacy-empty.db') == digest(root / 'modern-empty-copy.db'), 'Read changed copy'
