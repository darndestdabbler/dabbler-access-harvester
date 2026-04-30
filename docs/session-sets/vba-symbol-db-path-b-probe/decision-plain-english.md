# Decision (Plain English Version)

> This is the eighth-grade reading-level companion to
> [`decision.md`](decision.md). Same answer, simpler words. The
> original is the official document; this one is here to help
> anyone who finds the original too dense.

## The Big Question

We have an old Microsoft Access database with hundreds of tiny
programs (called "procedures") inside it. We want a modern computer
to understand what those programs do, so we can rewrite them or
document them.

There are two ways we could do that:

- **Way A: Have an AI read the old code and write notes about it.**
  Like asking a really smart friend to read a book and tell you what
  each chapter is about.
- **Way B: Rewrite the old code in a new programming language and
  add tests that prove the rewrite still works.** Like taking a
  recipe written in a language you don't speak, translating it into
  English, and then actually cooking the dish to make sure your
  translation came out right.

Both ways have been argued about for weeks. We needed to pick one.
But before picking, we ran a small experiment: take ONE program,
try Way B on it, and see how it goes. That experiment is what this
project was about.

## What We Actually Did

We picked one small program from the old database. Its job is to
stop the user from changing a company's "type" if that company has
existing orders or products. Pretty simple — maybe 20 lines of old
code.

Then:

1. **First helper (a different AI, called Codex)** read the old
   code and rewrote it in a new language called C#. It also wrote
   five tests to prove the new version works the way the old one
   did.
2. **Second helper (this AI, called Claude)** got handed the
   rewrite and the tests, but NOT the first AI's notes about why
   it made certain choices. Claude read the old code and the new
   code side-by-side and looked for mistakes or missed details.
3. **Claude then wrote four more tests** to lock down things the
   first AI's tests didn't cover.

We did it this way on purpose: two different AIs from two
different companies, working separately, so that if one of them
made a mistake the other would probably catch it. Like having one
person translate a recipe and a different person actually try to
cook the dish from the translation.

## What We Found

### Way B caught real things Way A would have missed

Three concrete examples:

1. **The "or" trick.** The old code says "block the change if the
   company is active OR has vendor products." The first AI rewrote
   that as a flipped-around version: "if the company is NOT active
   AND has no vendor products, then don't block." That works fine,
   but ONLY because the rewritten version still calls the same
   function in the same order. If a future programmer "simplified"
   it back to the obvious version, they would accidentally break
   it. The new test we wrote catches that mistake before it happens.

   If we had used Way A, the AI would have written a sentence like
   "blocks the change when the company has orders or vendor
   products." True, but useless for catching the trick.

2. **Two different error helpers.** The old code calls an
   error-handling function in two places, with slightly different
   options each time. The first place says "show a popup to the
   user." The second place says "don't show a popup." The first AI
   got both right by coincidence (because of how default settings
   work in C#), but a translator who got it wrong by hard-coding
   "show a popup" in BOTH places would still pass all five of the
   first AI's tests. **Our new test catches that mistake.** This
   is the strongest evidence that Way B's tests have real value.

   Way A's notes would not record this difference at all. Two
   different ways to call the same function are easy to summarize
   into one sentence and lose the distinction.

3. **The "is the dialog open?" check.** The old code, before
   closing a popup, first checks whether the popup is even open.
   That guard is preserved in the rewrite. The first AI's tests
   only check the case where the popup IS open. A translator under
   "simplify the dialog code" pressure could drop the guard and
   pass all the tests. **Our new test catches that mistake too.**

   Way A might write a note like "checks if the dialog is open
   before closing it." Reads correctly. Doesn't actually prove the
   code does it.

### What Way B did NOT do well

Way B doesn't easily produce the kind of high-level "here's WHY
the business cares about this rule" explanation that Way A can.
Our rewrite says "block the change if certain counts are above
zero," but it doesn't explain *why* the business made that rule.
Way A could write a paragraph like "Company Type changes are
blocked because changing the type would mess up downstream
records in the Orders and Products tables." That kind of
business-context summary is real value Way A produces and Way B
doesn't.

### Cost

Way A's first attempt (a different project last week) cost about
**$24** in AI fees and took 7 sessions. Way B (this project) cost
about **23 cents** in AI fees and took 3 sessions, because the
heavy lifting was done by AIs we already pay a flat monthly fee
for, instead of pay-per-use AIs.

So Way B is roughly **100 times cheaper per program** at the
pay-per-use level, with the trade-off that it takes more
wall-clock time per program (you have to wait for two separate
sessions instead of one batch run).

## What We Should Do — Use Both

Pure Way A: too vague to catch real bugs.
Pure Way B: too slow to do for every single procedure, and misses
the business-context explanations.

So: **use both, but for different kinds of code.**

- **Use Way B (rewrite + tests)** for code that *does something*:
  programs that branch on a rule ("if X then block, otherwise
  allow"), programs that change data, programs that pop up
  dialogs, programs that fire when a user clicks something.
  These are the places where bugs hide and where tests have
  real value.
- **Use Way A (AI notes)** for code that's basically just a
  label, a constant, or a simple "give me this value" function.
  These don't really "do" anything that needs testing; a sentence
  of AI notes is enough.

Roughly speaking: about half the programs in our database are the
"does something" kind that need Way B. The other half are simple
enough that Way A is fine. So we'd save a lot of time without
losing the safety net where it matters.

## What's Next

The next project should:

1. Build a way to automatically tell which programs need Way B
   and which can stay on Way A. (Look at whether the program has
   `If` statements, whether it's tied to a button or other user
   action, and whether it actually changes anything.)
2. Build a "translation packet" generator that bundles up several
   related programs into one batch, so we can rewrite a handful at
   a time instead of one at a time.
3. Translate the first batch — probably the other event handlers
   on the same form we used for this experiment. Codex translates
   one batch, Claude reviews. Same pattern as this experiment.
4. After ten or so programs, look at the actual cost and time
   numbers and decide whether the "two AIs working separately"
   pattern is still worth it, or whether one AI doing both jobs
   would be fine for simple programs.

## What Could Have Changed Our Mind

- If the second AI (Claude) had **found a real bug** in the
  first AI's rewrite, we'd be even more sure Way B is the right
  call.
- If the second AI had **found nothing worth testing** — meaning
  the first AI nailed everything on the first pass — we'd lean
  more toward Way A, because the second AI's session would be
  wasted spend.
- If Way B had cost as much as Way A (around $24 instead of 23
  cents), we'd lean back toward Way A, because Way B's main
  argument is "way cheaper plus the tests."

None of those things happened. The experiment paid for itself by
producing four real tests that pin down behaviors the AI-notes
version would not have captured, at about 1/100th the cost per
program.

That's why the answer is: **use both, but use Way B for any
program that actually does something interesting.**
