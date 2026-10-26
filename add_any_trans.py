with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon.controller') as f:
    c = f.read()

# Add parameters
if '"Hurt"' not in c:
    old = '''  - m_Name: IsJumping
    m_Type: 4
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}'''
    new = old + '''
  - m_Name: Hurt
    m_Type: 9
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}
  - m_Name: Death
    m_Type: 9
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}'''
    if old in c:
        c = c.replace(old, new)

any_trans = '''
--- !u!1101 &7000000000000000001
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
  m_CanTransitionToSelf: 1
'''
if '7000000000000000001' not in c:
    c += any_trans

any_trans2 = '''
--- !u!1101 &7000000000000000002
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
  m_CanTransitionToSelf: 1
'''
if '7000000000000000002' not in c:
    c += any_trans2

with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon.controller','w') as f:
    f.write(c)
print('done')
