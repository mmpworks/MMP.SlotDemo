import numpy as np, json
from math import comb
from pathlib import Path

# Reuse orca_check's game loading and category compilation, up to the point where it
# starts printing. Sharing the setup keeps one definition of how the JSON is read.
_setup = (Path(__file__).resolve().parent / 'orca_check.py').read_text()
exec(_setup.split("# category counts")[0])

# ---- exact law of the pick-bonus award ----
prizes=[]
for p in d['features'][0]['prizes']: prizes += [p['value']]*p['count']
B=6; CONS=1; P=len(prizes)
maxs=sum(prizes)
cnt=np.zeros((P+1,maxs+1), dtype=object); cnt[0][0]=1
for v in prizes:
    for k in range(P-1,-1,-1):
        row=cnt[k]
        for s in range(maxs-v,-1,-1):
            if row[s]: cnt[k+1][s+v]+=row[s]
# P(K=k) = 6 * 24!/(24-k)! * (29-k)! / 30!
from math import factorial as f
pk=[6*f(24)//f(24-k)*f(29-k)/f(30) for k in range(P+1)]
law=np.zeros(maxs+1)
for k in range(P+1):
    c=comb(P,k)
    for s in range(maxs+1):
        if cnt[k][s]: law[s]+= pk[k]*float(cnt[k][s])/c
law/=law.sum()
vals=np.arange(maxs+1)+CONS
m1=(law*vals).sum(); m2=(law*vals*vals).sum()
print('bonus exact mean %.6f  meansq %.4f  var %.6f' % (m1,m2,m2-m1*m1))

# ---- joint (linePay, triggered) law from the exhaustive table ----
pv = pays.ravel(); tv = trig.ravel()
keys = pv*2 + tv
uk,uc = np.unique(keys, return_counts=True)
probs = uc/uc.sum()
paycat = (uk//2).astype(np.float64); trigcat=(uk%2).astype(bool)
analytic = (probs*paycat).sum() + (probs*trigcat).sum()*m1
sig2 = (probs*paycat**2).sum() + 2*(probs*paycat*trigcat).sum()*m1 + (probs*trigcat).sum()*m2 - analytic**2
sigma = sig2**0.5
print('analytic total RTP %.7f  sigma %.6f' % (analytic, sigma))

rng=np.random.default_rng(7)
z=2.5758293035489004
for N in (10**6, 10**7, 10**8):
    R=20000
    half = z*sigma/np.sqrt(N)
    # multinomial over line/trigger categories
    counts = rng.multinomial(N, probs, size=R)
    linetot = counts @ paycat
    ntrig = counts[:, trigcat].sum(axis=1)
    # sum of ntrig iid bonus awards: normal-free -> multinomial per replicate is costly;
    # use exact-law multinomial per replicate via Poisson trick on the aggregate
    bonustot = np.empty(R)
    for i in range(R):
        c = rng.multinomial(ntrig[i], law)
        bonustot[i] = (c*vals).sum()
    rtp=(linetot+bonustot)/N
    cov = (np.abs(rtp-analytic)<=half).mean()
    print('N=%.0e  nominal 99.00%%  empirical coverage %.2f%%  (half-width %.5f)  P(below)=%.2f%% P(above)=%.2f%%'
          % (N, 100*cov, half, 100*(rtp<analytic-half).mean(), 100*(rtp>analytic+half).mean()))
