import json, itertools, numpy as np
from pathlib import Path

# Reuse orca_check's game loading and category compilation, up to the point where it
# starts printing. Sharing the setup keeps one definition of how the JSON is read.
_setup = (Path(__file__).resolve().parent / 'orca_check.py').read_text()
exec(_setup.split("# category counts")[0])

x = pays.ravel().astype(np.float64)
# bonus award exact distribution by simulation-free DP is heavy; approximate spin return as
# X = L + T*B with B drawn from the pick-bonus law. Build B's law by enumeration over
# the number of prizes collected? Use Monte-Carlo-free bound: use B's first 3 moments via
# the same symmetry argument (triples).
mu = x.mean()
print('line-only: mean %.6f sd %.4f' % (mu, x.std()))
c2 = ((x-mu)**2).mean(); c3=((x-mu)**3).mean()
rho = (np.abs(x-mu)**3).mean()
print('line-only skewness %.3f' % (c3/c2**1.5))
for n in (1e6,1e7,1e8,1e9):
    print('  N=%.0e  BerryEsseen 0.4656*rho/(sd^3 sqrt N) = %.4f' % (n, 0.4656*rho/(c2**1.5*n**0.5)))
top = (x==5000).sum()
print('top-pay(5000x) combos', int(top), 'p', top/x.size, 'expected in 10M spins', 1e7*top/x.size)
print('var share of the 5000x outcome: %.3f' % ((top/x.size)*5000**2/ ( ( (x**2).mean()-mu*mu ) )))
# how much of total variance sits in outcomes rarer than 1-in-100k
p = np.bincount((x).astype(np.int64), minlength=1)
vals = np.nonzero(p)[0]; probs = p[vals]/x.size
contrib = probs*(vals-mu)**2
order = np.argsort(-contrib)
print('top variance contributors (pay, prob, share of line variance):')
for i in order[:8]:
    print('   %6d  %.3e  %.3f' % (vals[i], probs[i], contrib[i]/c2))
