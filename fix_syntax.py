with open(r'D:\Unity\l&k\L-K\Assets\Scripts\Player\PlayerMovement2D.cs') as f:
    c = f.read()

old = "            float rate = reversing ? turnSpeed : Mathf.Approximately(moveInput, 0f) ? deceleration * 0.5f : acceleration * 0.7f;"
new = "            float rate = reversing ? turnSpeed : (Mathf.Approximately(moveInput, 0f) ? (deceleration * 0.5f) : (acceleration * 0.7f));"
if old in c:
    c = c.replace(old, new)
    with open(r'D:\Unity\l&k\L-K\Assets\Scripts\Player\PlayerMovement2D.cs','w') as f:
        f.write(c)
    print('fixed')
else:
    print('no')
