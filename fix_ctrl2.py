with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon.controller', 'r') as f:
    c = f.read()

old = 'guid: f8c5e1b31a9b3c34b862f8d0d9e7c6a5'
new = 'guid: ce02c231ae9b226458e6abc745013bc3'
if old in c:
    c2 = c.replace(old, new)
    with open(r'D:\Unity\l&k\L-K\Assets\Animations\Leon\Leon.controller', 'w') as f:
        f.write(c2)
    print('fixed')
else:
    print('no')
