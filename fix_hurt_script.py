with open(r'D:\Unity\l&k\L-K\Assets\Scripts\Player\PlayerHealthHurt.cs') as f:
    c = f.read()
c2 = c.replace('animator.Play("Hurt")', 'animator.SetTrigger("Hurt")')
c2 = c2.replace('animator.Play("Death")', 'animator.SetTrigger("Death")')
if c2 != c:
    with open(r'D:\Unity\l&k\L-K\Assets\Scripts\Player\PlayerHealthHurt.cs','w') as f:
        f.write(c2)
    print('fixed')
else:
    print('same')
