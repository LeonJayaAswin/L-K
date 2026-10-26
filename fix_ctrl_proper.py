with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon.controller') as f:
    c = f.read()

import re
for tid in ['7000000000000000001','7000000000000000002','6000000000000000001']:
    pattern = r'--- !u!1101 &' + tid + r'.*?(?=\n--- !u!110)'
    c2, n = re.subn(pattern, '', c, flags=re.DOTALL)
    if n > 0:
        c = c2
        print('removed', tid)

old_any = '''  m_AnyStateTransitions: []'''
new_any = '''  m_AnyStateTransitions:
  - {fileID: 8000000000000000001}
  - {fileID: 8000000000000000002}'''
if old_any in c and '8000000000000000001' not in c:
    c = c.replace(old_any, new_any)
    print('added any')

trans1 = '''
--- !u!1101 &8000000000000000001
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: Hurt
    m_EventTreshold: 0
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: 5000000000000000001}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.05
  m_TransitionOffset: 0
  m_ExitTime: 0.75
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
'''
trans2 = '''
--- !u!1101 &8000000000000000002
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: Death
    m_EventTreshold: 0
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: 5000000000000000002}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.05
  m_TransitionOffset: 0
  m_ExitTime: 0.75
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
'''
if '8000000000000000001' not in c:
    c += trans1
if '8000000000000000002' not in c:
    c += trans2

trans_hurt_back = '''
--- !u!1101 &8000000000000000003
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
if '8000000000000000003' not in c:
    c += trans_hurt_back

old_hurt_trans = '''  m_Transitions:
  - {fileID: 6000000000000000001}'''
new_hurt_trans = '''  m_Transitions:
  - {fileID: 8000000000000000003}'''
if old_hurt_trans in c:
    c = c.replace(old_hurt_trans, new_hurt_trans)
    print('fixed hurt')

with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon.controller','w') as f:
    f.write(c)
print('done')
