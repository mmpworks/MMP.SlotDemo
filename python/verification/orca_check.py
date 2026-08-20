import json, itertools, numpy as np
from fractions import Fraction as F
from pathlib import Path

# Repo-relative so this runs from a clone rather than one machine.
GAME = Path(__file__).resolve().parents[2] / 'CSharp' / 'games' / 'orca-dive.json'

d = json.load(open(GAME))
names = [s['name'] for s in d['symbols']]
idx = {n:i for i,n in enumerate(names)}
NS = len(names)
subs = {}   # wild id -> set of ids it substitutes for
for s in d['symbols']:
    if s.get('wild'):
        subs[idx[s['name']]] = {idx[x] for x in s.get('substitutesFor',[])}
groups = {k:[idx[x] for x in v] for k,v in d['groups'].items()}

# compile categories exactly as GameDefinitionBuilder does
cats = []
for e in d['paytable']:
    cont = [False]*NS; req = [False]*NS
    if 'symbol' in e:
        i = idx[e['symbol']]
        cont[i]=True; req[i]=True
        for w,tgt in subs.items():
            if i in tgt: cont[w]=True
        label = e.get('name', e['symbol'])
    else:
        for i in groups[e['group']]:
            cont[i]=True; req[i]=True
        label = e.get('name', e['group'])
    pays=[0]*6
    for k,v in e['pays'].items(): pays[int(k)] = int(v)
    cats.append((label,cont,req,pays))

def evaluate(cells):
    bestpay=0; bestrun=0; bestcat=None
    for label,cont,req,pays in cats:
        run=0; sat=False
        while run < len(cells) and cont[cells[run]]:
            sat |= req[cells[run]]; run+=1
        if not sat: continue
        pay = pays[run] if run < len(pays) else 0
        if pay==0 or pay<bestpay: continue
        if pay==bestpay and run<=bestrun: continue
        bestpay, bestrun, bestcat = pay, run, label
    return bestcat, bestrun, bestpay

# payout lookup over all 10^5 symbol tuples
LUT = np.zeros(10**5, dtype=np.int64)
CATLUT = {}
for tup in itertools.product(range(NS), repeat=5):
    c,r,p = evaluate(list(tup))
    key = tup[0]*10**4+tup[1]*10**3+tup[2]*100+tup[3]*10+tup[4]
    LUT[key]=p
    if p: CATLUT[key]=(c,r)

strips = [[idx[x] for x in strip] for strip in d['reels']]
rows = d['windowRows']; payrow = d['paylines'][0]['rows']
pen = idx['Penguin']

symcol=[]; pencol=[]
for r,strip in enumerate(strips):
    n=len(strip)
    symcol.append(np.array([strip[(stop+payrow[r])%n] for stop in range(n)], dtype=np.int64))
    pencol.append(np.array([any(strip[(stop+row)%n]==pen for row in range(rows)) for stop in range(n)], dtype=bool))

shape=[len(s) for s in strips]
key = np.zeros(shape, dtype=np.int64)
mult=[10**4,10**3,100,10,1]
for r in range(5):
    sh=[1]*5; sh[r]=shape[r]
    key += (symcol[r]*mult[r]).reshape(sh)
pays = LUT[key]
trig = np.ones(shape, dtype=bool)
for r in (0,2,4):
    sh=[1]*5; sh[r]=shape[r]
    trig &= pencol[r].reshape(sh)

total = int(np.prod(shape))
hits = int((pays>0).sum())
ntrig = int(trig.sum())
both = int(((pays>0)&trig).sum())
sumpay = int(pays.sum())
sumpay2 = int((pays.astype(object)**2).sum()) if False else int((pays.astype(np.float64)**2).sum())
sumpaytrig = int(pays[trig].sum())

print('cycle            ', total)
print('line hits        ', hits, hits/total)
print('sum pay          ', sumpay, 'lineRTP', sumpay/total)
print('trigger combos   ', ntrig, ntrig/total, '(6/26)^3 =', 216/17576)
print('both             ', both, both/total)
print('any-award union  ', (hits+ntrig-both), (hits+ntrig-both)/total)

# category counts
from collections import Counter
cnt = Counter()
flat = key.ravel(); pflat = pays.ravel()
uk, uc = np.unique(flat, return_counts=True)
for k,c in zip(uk.tolist(), uc.tolist()):
    if k in CATLUT:
        cnt[CATLUT[k]] += c
for k in sorted(cnt, key=lambda x:(x[0],x[1])):
    print('  ', k, cnt[k])
print('total winning combos', sum(cnt.values()))

# bonus moments
prizes=[]
for p in d['features'][0]['prizes']: prizes += [p['value']]*p['count']
b = d['features'][0]['blanks']['count']; cons = d['features'][0]['blanks']['consolation']
S=sum(prizes); S2=sum(v*v for v in prizes)
single=F(1,b+1); pair=F(2,(b+2)*(b+1))
EW = S*single; EW2 = S2*single + (S*S-S2)*pair
mean = EW+cons; meansq = EW2+2*cons*EW+cons*cons
var = meansq-mean*mean
print('bonus mean', float(mean), 'meansq', float(meansq), 'var', float(var))

pT = F(ntrig,total)
lineRtp = F(sumpay,total)
bonusRtp = pT*mean
print('lineRtp %.6f  bonusRtp %.6f  total %.6f' % (float(lineRtp), float(bonusRtp), float(lineRtp+bonusRtp)))

# sigma of full per-spin return
EL = sumpay/total
EL2 = sumpay2/total
ELT = sumpaytrig/total
m = EL + float(bonusRtp)
m2 = EL2 + 2*ELT*float(mean) + float(pT)*float(meansq)
print('E[L]',EL,'E[L^2]',EL2,'E[LT]',ELT)
print('mean',m,'meanSq',m2,'sigma', (m2-m*m)**0.5)
print('lineSigma', (EL2-EL*EL)**0.5)
print('bonusSigma', (float(pT)*float(meansq)-float(bonusRtp)**2)**0.5)
