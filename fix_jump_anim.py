import re
with open(r'D:\Unity\l&k\L-K\Assets\Sprites\Leon spritesheets\Leon-jump-v1.png.meta', 'r') as f:
    meta = f.read()

sprites = {}
matches = re.findall(r'213: (-?\d+).*?second: Leon-jump-v1_(\d+)', meta, re.DOTALL)
for mid, idx in matches:
    sprites[int(idx)] = mid

ids = []
for i in range(25):
    if i in sprites:
        ids.append(sprites[i])
    else:
        ids.append('0')

mg = re.search(r'guid: ([a-f0-9]+)', meta)
tex_guid = mg.group(1) if mg else 'c6f18e85a7e0831408a1c7b7fa24d54d'

lines = []
lines.append('%YAML 1.1')
lines.append('%TAG !u! tag:unity3d.com,2011:')
lines.append('--- !u!74 &7400000')
lines.append('AnimationClip:')
lines.append('  m_ObjectHideFlags: 0')
lines.append('  m_CorrespondingSourceObject: {fileID: 0}')
lines.append('  m_PrefabInstance: {fileID: 0}')
lines.append('  m_PrefabAsset: {fileID: 0}')
lines.append('  m_Name: Leon-Jump')
lines.append('  serializedVersion: 7')
lines.append('  m_Legacy: 0')
lines.append('  m_Compressed: 0')
lines.append('  m_UseHighQualityCurve: 1')
lines.append('  m_RotationCurves: []')
lines.append('  m_CompressedRotationCurves: []')
lines.append('  m_EulerCurves: []')
lines.append('  m_PositionCurves: []')
lines.append('  m_ScaleCurves: []')
lines.append('  m_FloatCurves: []')
lines.append('  m_PPtrCurves:')
lines.append('  - serializedVersion: 2')
lines.append('    curve:')
for i in range(25):
    t = i * 0.05
    if t == 0:
        lines.append('    - time: 0')
    else:
        lines.append(f'    - time: {t:.2f}')
    lines.append(f'      value: {{fileID: {ids[i]}, guid: {tex_guid}, type: 3}}')
lines.append('    attribute: m_Sprite')
lines.append('    path: ')
lines.append('    classID: 212')
lines.append('    script: {fileID: 0}')
lines.append('    flags: 2')
lines.append('  m_SampleRate: 20')
lines.append('  m_WrapMode: 0')
lines.append('  m_Bounds:')
lines.append('    m_Center: {x: 0, y: 0, z: 0}')
lines.append('    m_Extent: {x: 0, y: 0, z: 0}')
lines.append('  m_ClipBindingConstant:')
lines.append('    genericBindings:')
lines.append('    - serializedVersion: 2')
lines.append('      path: 0')
lines.append('      attribute: 0')
lines.append('      script: {fileID: 0}')
lines.append('      typeID: 212')
lines.append('      customType: 23')
lines.append('      isPPtrCurve: 1')
lines.append('      isIntCurve: 0')
lines.append('      isSerializeReferenceCurve: 0')
lines.append('    pptrCurveMapping:')
for i in range(25):
    lines.append(f'    - {{fileID: {ids[i]}, guid: {tex_guid}, type: 3}}')
lines.append('  m_AnimationClipSettings:')
lines.append('    serializedVersion: 2')
lines.append('    m_AdditiveReferencePoseClip: {fileID: 0}')
lines.append('    m_AdditiveReferencePoseTime: 0')
lines.append('    m_StartTime: 0')
lines.append('    m_StopTime: 1.25')
lines.append('    m_OrientationOffsetY: 0')
lines.append('    m_Level: 0')
lines.append('    m_CycleOffset: 0')
lines.append('    m_HasAdditiveReferencePose: 0')
lines.append('    m_LoopTime: 0')
lines.append('    m_LoopBlend: 0')
lines.append('    m_LoopBlendOrientation: 0')
lines.append('    m_LoopBlendPositionY: 0')
lines.append('    m_LoopBlendPositionXZ: 0')
lines.append('    m_KeepOriginalOrientation: 0')
lines.append('    m_KeepOriginalPositionY: 1')
lines.append('    m_KeepOriginalPositionXZ: 0')
lines.append('    m_HeightFromFeet: 0')
lines.append('    m_Mirror: 0')
lines.append('  m_EditorCurves: []')
lines.append('  m_EulerEditorCurves: []')
lines.append('  m_HasGenericRootTransform: 0')
lines.append('  m_HasMotionFloatCurves: 0')
lines.append('  m_Events: []')

with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon-Jump.anim', 'w') as f:
    f.write('\n'.join(lines) + '\n')

print('done')
