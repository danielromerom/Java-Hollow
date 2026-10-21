"""Read-only structural checks for the applied terrain prototype substitution."""
from pathlib import Path
import re

root = Path(__file__).resolve().parent.parent
candidate = root / 'Assets/PerformanceOptimizations/Floor/QuadCandidate'
guid = re.search(r'guid: (\w+)', (candidate / 'DirtQuad.prefab.meta').read_text()).group(1)
lines = []
for name in ['New Terrain.asset', 'TerrainData_30530da2-949b-4ba6-9e8a-63f464d6f89b.asset']:
    before = (root / 'Logs/DirtQuadOriginals' / name).read_bytes()
    after = (root / 'Assets/Fantasy Forest Environment Free Sample/Scenes' / name).read_bytes()
    assert len(before) == len(after), name
    changes = [i for i,(a,b) in enumerate(zip(before, after)) if a != b]
    assert changes and len(changes) < 40, (name, len(changes))
    lines.append(f'{name}: same byte length ({len(after)}), {len(changes)} changed bytes; offsets {min(changes)}..{max(changes)}.')
mesh = (candidate / 'DirtQuad.asset').read_text()
assert 'm_VertexCount: 4' in mesh
assert 'indexCount: 6' in mesh
lines.append(f'Quad prefab GUID: {guid}. Mesh: 4 vertices, 6 triangle indices.')
print('\n'.join(lines))
(root / 'Performance/dirt-quad-asset-validation.txt').write_text('\n'.join(lines))
