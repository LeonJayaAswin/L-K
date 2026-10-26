with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon.controller', 'r') as f:
    c = f.read()

# Add Hurt and Death states to child states
old_child = '''  - serializedVersion: 1
    m_State: {fileID: 4589147391858383018}
    m_Position: {x: 750, y: 120, z: 0}'''
new_child = old_child + '''
  - serializedVersion: 1
    m_State: {fileID: 5000000000000000001}
    m_Position: {x: 220, y: 300, z: 0}
  - serializedVersion: 1
    m_State: {fileID: 5000000000000000002}
    m_Position: {x: 480, y: 300, z: 0}'''
if old_child in c and 'Leon-Hurt' not in c:
    c = c.replace(old_child, new_child)
    print('added child')
else:
    print('skip')

# Get GUIDs
import re
with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon-Hurt.anim.meta') as f:
    gh = re.search(r'guid: ([a-f0-9]+)', f.read()).group(1)
with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon-Death.anim.meta') as f:
    gd = re.search(r'guid: ([a-f0-9]+)', f.read()).group(1)

hurt_state = f'''
--- !u!1102 &5000000000000000001
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Hurt
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions:
  - {{fileID: 6000000000000000001}}
  m_StateMachineBehaviours: []
  m_Position: {{x: 50, y: 50, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {gh}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
'''
death_state = f'''
--- !u!1102 &5000000000000000002
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Death
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions: []
  m_StateMachineBehaviours: []
  m_Position: {{x: 50, y: 50, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {gd}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
'''
if 'Hurt' not in c:
    c += hurt_state + death_state
    print('added states')

# Add transitions
trans_hurt = '''
--- !u!1101 &6000000000000000001
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: 
  m_Conditions: []
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: 3152768117053311496}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.1
  m_TransitionOffset: 0
  m_ExitTime: 0.9
  m_HasExitTime: 1
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 1
'''
if '6000000000000000001' not in c:
    c += trans_hurt

with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon.controller', 'w') as f:
    f.write(c)
print('done')
