<script setup lang="ts">
defineProps<{ title: string; blurb: string }>()

interface Entry {
  title: string
  author: string
  year: string
  kind: 'book' | 'paper' | 'standard' | 'article' | 'patent'
  url: string
  free?: boolean
  about: string
  series?: string
}

interface Shelf {
  name: string
  lede: string
  entries: Entry[]
}

const shelves: Shelf[] = [
  {
    name: 'Slot math, from the designer\'s chair',
    lede: 'How a paytable, a reel set, and a target RTP become one machine. This shelf is the closest to what the series builds.',
    entries: [
      {
        title: 'The Mathematics of Slots: Configurations, Combinations, Probabilities',
        author: 'Cătălin Bărboianu',
        year: '2013',
        kind: 'book',
        url: 'https://books.google.com/books/about/The_Mathematics_of_Slots.html?id=ertEkKX0YHMC',
        about: 'The most complete treatment of slot probability in print: stop configurations, winning-combination counting, and the general formulas behind any PAR sheet. Heavy on notation, worth the effort.',
        series: 'Episodes 3 and 4 are this book\'s core chapters, done in C# instead of algebra.',
      },
      {
        title: 'Elements of Slot Design (3rd edition)',
        author: 'Tom Meacham — slotdesigner.com',
        year: '2022',
        kind: 'book',
        free: true,
        url: 'https://slotdesigner.com/wp/wp-content/uploads/Elements-of-Slot-Design-3rd-Edition.pdf',
        about: 'A free, practical walk from empty spreadsheet to working slot math model: strips, weights, hit rate, volatility, feature budgets. Written by the author of the Slot Designer toolchain.',
        series: 'The closest published analogue to this series. Compare its spreadsheet workflow with our code-first one.',
      },
      {
        title: 'Practical Design of Slot Machine Math',
        author: 'Balabanov, Tomov & Dimitrov',
        year: '2022',
        kind: 'book',
        url: 'https://www.researchgate.net/publication/364824602_Practical_Design_of_Slot_Machine_Math',
        about: 'A short engineering-flavored treatment of building slot math models, including simulation-based verification of analytic figures.',
        series: 'Its analytic-versus-simulation loop is episode 7\'s referee idea in book form.',
      },
      {
        title: 'Creating PAR Sheets — slot math tutorial series',
        author: 'slotgamedesign.com',
        year: '2019',
        kind: 'article',
        free: true,
        url: 'https://slotgamedesign.com/2019/01/19/slot-math-tutorial-creating-par-sheets/',
        about: 'A working designer\'s tutorial series that builds a PAR sheet cell by cell: symbol counts, line pays, hit frequency, RTP decomposition.',
        series: 'The RTP-slice table in the chapter 4 lab is one of these worksheets, computed by the enumerator.',
      },
    ],
  },
  {
    name: 'Casino math, the wider table',
    lede: 'Slots sit inside a bigger discipline: house edge, volatility, and why the casino\'s sums always work out.',
    entries: [
      {
        title: 'Practical Casino Math (2nd edition)',
        author: 'Robert C. Hannum & Anthony N. Cabot',
        year: '2005',
        kind: 'book',
        url: 'https://www.amazon.com/Practical-Casino-Math-Robert-Hannum/dp/0942828534',
        about: 'The standard reference for the mathematics of every casino game: expectation, variance, house edge, hold, and how regulators and operators use those numbers. Out of print and priced like it.',
        series: 'Our sigma-per-unit-wagered and the z·σ/√N band come straight from this tradition.',
      },
      {
        title: 'Casino Mathematics (UNLV guide)',
        author: 'Robert C. Hannum — UNLV Gaming Studies',
        year: '2012',
        kind: 'article',
        free: true,
        url: 'https://www.trendfollowing.com/pdfs/casino_math.pdf',
        about: 'A free condensed version of the Practical Casino Math material: house advantage, confidence limits, volatility index. A fine substitute while the book stays rare.',
        series: 'Its confidence-limit section is the funnel on the proving-ground chart.',
      },
      {
        title: 'Casino Operations Management',
        author: 'Kilby, Fox & Lucas',
        year: '2005',
        kind: 'book',
        url: 'https://www.amazon.com/Casino-Operations-Management-Jim-Kilby/dp/0471266329',
        about: 'The operations side: slot floor economics, hold percentages, par and performance reporting. Where the math meets the business.',
        series: 'Explains who reads a PAR sheet after the designer ships it.',
      },
    ],
  },
  {
    name: 'PAR sheets and how real machines are built',
    lede: 'The primary documents. These sources show in the wild what the series reconstructs.',
    entries: [
      {
        title: 'PAR Sheets, probabilities, and slot machine play',
        author: 'Kevin Harrigan & Mike Dixon — Journal of Gambling Issues',
        year: '2009',
        kind: 'paper',
        free: true,
        url: 'https://www.stoppredatorygambling.org/wp-content/uploads/2012/12/PAR-Sheets-Probabilities-and-Slot-Machine-Play-Implications-for-Problem-and-Non-Problem-Gambling.pdf',
        about: 'The landmark paper: real PAR sheets obtained through freedom-of-information requests, dissected in public. Reel weights, near-miss engineering, and the gap between displayed and actual odds.',
        series: 'The only peer-reviewed look at documents like the one Orca Dive is reconstructed from.',
      },
      {
        title: 'The Design of Slot Machine Games (presentation)',
        author: 'Kevin Harrigan',
        year: '2010',
        kind: 'paper',
        free: true,
        url: 'https://stoppredatorygambling.org/wp-content/uploads/2012/12/Harrigan-presentation-to-the-2010-NH-Gambling-Commission.pdf',
        about: 'The same research delivered as slides for a state gambling commission. The fastest readable tour of virtual reels, weighting, and stop maps.',
        series: 'A good pre-read for episode 3.',
      },
      {
        title: 'Electronic gaming device utilizing a random number generator (US 4,448,419)',
        author: 'Inge Telnaes',
        year: '1984',
        kind: 'patent',
        free: true,
        url: 'https://patents.google.com/patent/US4448419A/en',
        about: 'The virtual-reel patent that created the modern slot machine: map many RNG values onto few physical stops, and the odds no longer depend on the physical reel. Every weighted strip in use today descends from this filing.',
        series: 'Our strips implement this idea. Episode 3\'s even-interleave builder is a Telnaes mapping written out in the open.',
      },
      {
        title: 'How to calculate the return for a slot machine (Appendix 1 & 2)',
        author: 'Michael Shackleford — Wizard of Odds',
        year: 'ongoing',
        kind: 'article',
        free: true,
        url: 'https://wizardofodds.com/games/slots/appendix/2/',
        about: 'Worked examples of computing a machine\'s return from its reel strips and paytable, plus thousands of logged real spins. The public benchmark for open slot analysis.',
        series: 'The same computation the chapter 4 lab performs, on machines you can find on a floor.',
      },
      {
        title: 'Slot machine basics',
        author: 'Michael Shackleford — Wizard of Odds',
        year: 'ongoing',
        kind: 'article',
        free: true,
        url: 'https://wizardofodds.com/games/slots/basics/',
        about: 'The plain-language overview: how stops, weights, and returns relate, and what the published return ranges of real machines look like.',
        series: 'Hand this to a viewer who asks where to start before episode 1.',
      },
    ],
  },
  {
    name: 'Randomness, done properly',
    lede: 'The two algorithms inside SpinRng, from their authors.',
    entries: [
      {
        title: 'Scrambled Linear Pseudorandom Number Generators',
        author: 'David Blackman & Sebastiano Vigna',
        year: '2018',
        kind: 'paper',
        free: true,
        url: 'https://prng.di.unimi.it/',
        about: 'The xoshiro/xoroshiro family, with the PRNG shootout comparing speed and statistical quality across the field. The seeding recipe our ForWorker uses is documented here.',
        series: 'Episode 2\'s xoshiro256** and SplitMix64 seeding come from this page, constants and all.',
      },
      {
        title: 'Fast Random Integer Generation in an Interval',
        author: 'Daniel Lemire',
        year: '2019',
        kind: 'paper',
        free: true,
        url: 'https://arxiv.org/abs/1805.10941',
        about: 'Why the modulo reduction is biased and slow, and the multiply-shift-with-rejection method that replaces it. Short and immediately usable.',
        series: 'NextInt implements this paper, and the chapter 2 bias lab draws its figure 1 live.',
      },
      {
        title: 'PCG: A family of better random number generators',
        author: 'Melissa O\'Neill',
        year: '2014',
        kind: 'paper',
        free: true,
        url: 'https://www.pcg-random.org/',
        about: 'The other modern PRNG family, with the clearest writing anywhere on what "good randomness" means and how generators fail tests. Read it to understand what xoshiro is measured against.',
        series: 'Context for why episode 2 says "published, tested, canonical — this is not the place for creativity."',
      },
    ],
  },
  {
    name: 'Regulation — what "certified" means',
    lede: 'A simulator needs replayable runs; a regulator requires unpredictable ones. These documents cover the regulator\'s side.',
    entries: [
      {
        title: 'GLI-11: Gaming Devices in Casinos (v3.0)',
        author: 'Gaming Laboratories International',
        year: '2016',
        kind: 'standard',
        free: true,
        url: 'https://gaminglabs.com/wp-content/uploads/2018/09/GLI-11-Gaming-Devices-V3-0.pdf',
        about: 'The de-facto worldwide certification standard for gaming devices: RNG requirements, continuous chi-square self-tests, metering, program verification.',
        series: 'SpinRng is labelled simulation-grade rather than certified-gaming because of the requirements in this document.',
      },
      {
        title: 'Technical Standards for Gaming Devices',
        author: 'Nevada Gaming Control Board',
        year: 'current',
        kind: 'standard',
        free: true,
        url: 'https://www.gaming.nv.gov/siteassets/content/home/features/TechnicalStandard1.pdf',
        about: 'Nevada\'s own device standards: randomness, minimum return, fraud prevention. Shorter than GLI-11 and interesting for what a state chooses to pin down in law.',
        series: 'The 99% aggregate cap in our SimulationConfig is a homage to rules like these.',
      },
    ],
  },
  {
    name: 'The human side',
    lede: 'What these machines do to the people in front of them. Anyone building or explaining slots should have read at least the first entry.',
    entries: [
      {
        title: 'Addiction by Design: Machine Gambling in Las Vegas',
        author: 'Natasha Dow Schüll',
        year: '2012',
        kind: 'book',
        url: 'https://press.princeton.edu/books/paperback/9780691278285/addiction-by-design',
        about: 'Fifteen years of ethnography inside the machine-gambling industry: the "machine zone," time-on-device engineering, and how every design lever the other shelves describe gets aimed at a player. Princeton University Press.',
        series: 'The reason this series simulates with play money and says so out loud.',
      },
      {
        title: 'Slot Machines: A Pictorial History of the First 100 Years',
        author: 'Marshall Fey',
        year: '1983–2004 editions',
        kind: 'book',
        url: 'https://www.amazon.com/Slot-Machines-Pictorial-History-First/dp/1889243000',
        about: 'Written by Charles Fey\'s grandson: the Liberty Bell to the video era, with 400 photographs. The mechanical lineage behind every convention the digital machines still imitate.',
        series: 'Where episode 3\'s "canonical" 22-stop strip comes from: the mechanical machines.',
      },
    ],
  },
]

const kindLabel: Record<Entry['kind'], string> = {
  book: 'Book',
  paper: 'Paper',
  standard: 'Standard',
  article: 'Web',
  patent: 'Patent',
}
</script>

<template>
  <article>
    <header class="chapter-head">
      <h2>{{ title }}</h2>
      <p class="chapter-blurb">{{ blurb }}</p>
    </header>

    <section v-for="shelf in shelves" :key="shelf.name" class="shelf">
      <h3>{{ shelf.name }}</h3>
      <p class="shelf__lede">{{ shelf.lede }}</p>

      <div class="shelf__grid">
        <a
          v-for="entry in shelf.entries"
          :key="entry.title"
          class="card"
          :href="entry.url"
          target="_blank"
          rel="noopener noreferrer"
        >
          <div class="card__top">
            <span class="card__kind">{{ kindLabel[entry.kind] }}</span>
            <span v-if="entry.free" class="card__free">free</span>
            <span class="card__year">{{ entry.year }}</span>
          </div>
          <span class="card__title">{{ entry.title }}</span>
          <span class="card__author">{{ entry.author }}</span>
          <p class="card__about">{{ entry.about }}</p>
          <p v-if="entry.series" class="card__series">In this series: {{ entry.series }}</p>
        </a>
      </div>
    </section>

    <section class="chapter-brief">
      <h3>A note on sources</h3>
      <p>
        Orca Dive is an original fictional game whose numbers were reconstructed from
        public statistical analysis; no manufacturer materials were used anywhere in this
        project. The shelves above are the public literature that teaches the same
        techniques. Everything a PAR sheet encodes can be built and checked from open
        sources.
      </p>
    </section>
  </article>
</template>

<style scoped>
.shelf {
  margin-bottom: var(--space-xl);
}

.shelf h3 {
  font-size: 1.15rem;
  margin: 0 0 var(--space-xs);
}

.shelf__lede {
  color: var(--color-text-secondary);
  max-width: 70ch;
  line-height: 1.6;
  font-size: 0.9rem;
  margin-bottom: var(--space-md);
}

.shelf__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: var(--space-md);
}

.card {
  display: grid;
  gap: 0.35rem;
  align-content: start;
  background: var(--color-surface);
  border: var(--rule-hairline);
  padding: var(--space-md);
  color: inherit;
  text-decoration: none;
  transition:
    border-color var(--duration-micro) var(--ease-out),
    background var(--duration-micro) var(--ease-out);
}

.card:hover {
  border-color: var(--color-accent);
  background: var(--color-surface-raised);
}

.card__top {
  display: flex;
  gap: var(--space-sm);
  align-items: baseline;
}

.card__kind {
  font-family: var(--font-display);
  font-size: 0.64rem;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: var(--color-accent);
  border: var(--rule-hairline);
  padding: 0.15rem 0.5rem;
}

.card__free {
  font-family: var(--font-mono);
  font-size: 0.66rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--color-status-installed);
}

.card__year {
  margin-left: auto;
  font-family: var(--font-mono);
  font-size: 0.72rem;
  color: var(--color-text-muted);
}

.card__title {
  font-size: 0.98rem;
  line-height: 1.35;
  color: var(--color-text-primary);
}

.card__author {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
}

.card__about {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
  line-height: 1.55;
  margin: 0.2rem 0 0;
}

.card__series {
  font-size: 0.78rem;
  color: var(--color-accent-dim);
  line-height: 1.5;
  margin: 0.2rem 0 0;
  border-top: var(--rule-hairline);
  padding-top: 0.45rem;
}
</style>
