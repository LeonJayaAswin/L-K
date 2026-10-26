with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon.controller') as f:
    c = f.read()
idx = c.find('m_Name: Hurt')
if idx >= 0:
    start = c.rfind('--- !u!1102', 0, idx)
    end = c.find('\n--- !u!110', idx)
    if end == -1:
        end = idx + 600
    print(c[start:end])
else:
    print('no')
