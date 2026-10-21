"""Remove back-facing underside indices only; preserve all vertex data and bounds."""
from pathlib import Path
import re
import struct

root = Path(__file__).resolve().parent.parent
path = root / 'Assets/PerformanceOptimizations/Floor/DirtDetailCaps.asset'
original = path.read_bytes()
text = original.decode('utf-8')
assert 'm_IndexFormat: 0' in text
vertices = bytes.fromhex(re.search(r'_typelessdata: ([0-9a-f]+)', text)[1])
assert len(vertices) == 42 * 56
indices = bytes.fromhex(re.search(r'm_IndexBuffer: ([0-9a-f]+)', text)[1])
assert len(indices) == 120 * 2, 'Expected the original 40-triangle cap mesh'
keep = []
removed = []
for triangle in struct.iter_unpack('<3H', indices):
    position = [struct.unpack_from('<3f', vertices, i * 56) for i in triangle]
    normals = [struct.unpack_from('<3f', vertices, i * 56 + 12) for i in triangle]
    assert all(abs(n[1]) == 1 and n[0] == n[2] == 0 for n in normals)
    assert all(p[1] == position[0][1] for p in position)
    if position[0][1] == 1 and all(n[1] == 1 for n in normals):
        keep.extend(triangle)
    else:
        assert position[0][1] == -1 and all(n[1] == -1 for n in normals)
        removed.extend(triangle)
assert len(keep) == len(removed) == 60
material = (root / 'Assets/Fantasy Forest Environment Free Sample/Materials/dirt01.mat').read_text()
assert '- _Cull: 2' in material and '- _Surface: 0' in material
backup = root / 'Performance/DirtDetailCaps-before-underside-removal.asset.txt'
assert not backup.exists(), 'Do not overwrite rollback evidence'
backup.write_bytes(original)
packed = struct.pack('<60H', *keep).hex()
updated = re.sub(r'm_IndexBuffer: [0-9a-f]+', 'm_IndexBuffer: ' + packed, text)
updated = updated.replace('indexCount: 120', 'indexCount: 60', 1)
# All changes must be confined to index count and index bytes.
assert re.sub(r'm_IndexBuffer: [0-9a-f]+', '', updated).replace('indexCount: 60', 'indexCount: 120', 1) == re.sub(r'm_IndexBuffer: [0-9a-f]+', '', text)
path.write_bytes(updated.encode('utf-8'))
(root / 'Performance/floor-top-validation.txt').write_text(
    'PASS: 40 -> 20 triangles per detail; all 20 original upper triangles retained verbatim.\n'
    'Only downward-facing triangles at y=-1 removed. Vertex attributes, mesh bounds, materials, transforms and terrain density unchanged.\n'
    'Opaque back-face culling means removed faces were invisible from above their planes. Views below a detail plane can differ.\n'
    'No measured VR frame-rate improvement claimed. Rollback mesh preserved beside this report.\n')
print('Validated and applied upper-surface-only index buffer; rollback mesh saved.')
