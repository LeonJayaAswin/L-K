with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon.controller') as f:
    c = f.read()

sm_start = c.find('--- !u!1107 &-7066575818533391024')
if sm_start >= 0:
    sm_end = c.find('\n--- !u!110', sm_start)
    if sm_end == -1: sm_end = sm_start + 1000
    sm = c[sm_start:sm_end]
    print('has Hurt:', '5000000000000000001' in sm)
    print('has Death:', '5000000000000000002' in sm)
    print(sm)
else:
    print('no')
