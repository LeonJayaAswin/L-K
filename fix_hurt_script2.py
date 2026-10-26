with open(r'D:\Unity\l&k\L-K\Assets\Scripts\Player\PlayerHealthHurt.cs') as f:
    c = f.read()
c2 = c.replace('animator.SetTrigger("Hurt")', 'animator.Play("Hurt", 0, 0f)')
c2 = c2.replace('animator.SetTrigger("Death")', 'animator.Play("Death", 0, 0f)')
if c2 != c:
    with open(r'D:\Unity\l&k\L-K\Assets\Scripts\Player\PlayerHealthHurt.cs','w') as f:
        f.write(c2)
    print('fixed')
else:
    print('same')
