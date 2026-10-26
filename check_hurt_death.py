import re
with open(r'D:\Unity\l&k\L-K\Assets\Sprites\Leon spritesheets\Leon-Hurt.png.meta') as f:
    m = f.read()
spr = re.findall(r'213: (-?\d+).*?second: Leon-Hurt_(\d+)', m, re.DOTALL)
print('HURT:', len(spr))
for x,y in sorted(spr, key=lambda p:int(p[1]))[:5]:
    print(x,y)
mg = re.search(r'guid: ([a-f0-9]+)', m)
print('guidH:', mg.group(1) if mg else '')

with open(r'D:\Unity\l&k\L-K\Assets\Sprites\Leon spritesheets\Leon-Death.png.meta') as f:
    m2 = f.read()
spr2 = re.findall(r'213: (-?\d+).*?second: Leon-Death_(\d+)', m2, re.DOTALL)
print('DEATH:', len(spr2))
for x,y in sorted(spr2, key=lambda p:int(p[1]))[:5]:
    print(x,y)
mg2 = re.search(r'guid: ([a-f0-9]+)', m2)
print('guidD:', mg2.group(1) if mg2 else '')
