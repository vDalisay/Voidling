# Appearance genetics and code-filled art decision

**Status:** product decision locked 2026-08-26. This document supersedes the earlier handoff-plan statement that appearance inheritance/dominance was unresolved.

## Genetics baseline

Voidling appearance inheritance follows the Sonic Adventure 2 Battle / modern Chao Garden genetics model as its baseline:

- appearance loci are diploid;
- a child receives exactly one allele from each selected parent for every appearance locus;
- the normal/default colour allele is recessive;
- all non-normal colour alleles are equally dominant, so a heterozygous pair of two different non-normal colours resolves 50/50 at birth while retaining both alleles for later breeding;
- mono-tone and two-tone are equally dominant and resolve 50/50 when heterozygous;
- shiny is dominant over non-shiny;
- the normal/no-special-coat allele is recessive and non-normal special coats are equally dominant;
- founder/store genetics are pure (homozygous) unless an authored special stock explicitly says otherwise;
- the child's genotype and equal-dominance phenotype choices are frozen when its egg is created;
- all result-affecting rolls use stable deterministic RNG substreams.

Voidlings extend this baseline with a **pattern locus**. Pattern 0 means no/default pattern and is recessive; non-zero pattern alleles are equally dominant. This keeps pattern breeding consistent with the Chao colour/coat mental model without coupling Domain code to art assets.

The stable base colour catalogue contains 14 allele slots. Existing indices 0-9 remain unchanged for save compatibility; additional colours are appended only.

## Special coats and rare skins

The coat locus is semantic and extensible. Current built-in values reserve:

- 0: normal/no special coat;
- 1: glow;
- 2: glisten.

Future rare finishes may append semantic coat IDs. They use the same inheritance/dominance machinery. Cosmetic finish animation must never affect racing, simulation RNG, breeding probability after egg creation, or other authoritative gameplay.

The existing `RareTraitData` lineage/mutation system remains separate. A rare lineage trait is not automatically an appearance coat allele and vice versa.

## Art pipeline: outlines plus code-filled layers

Production Voidling sprites should not be authored as one recoloured sprite sheet per colour. Artists provide:

1. the normal outline/detail atlas for world/race/portrait frames;
2. an aligned fill-mask atlas at exactly the same dimensions;
3. optional aligned pattern-mask atlases indexed by semantic pattern allele;
4. a swim-specific source/mask only when swimming genuinely needs different source art.

The fill-mask channels are:

- **R** = primary/body fill;
- **G** = secondary/two-tone fill;
- **B** = authored accent fill.

`VoidlingVisualFactory` is the only presentation entry point that interprets these masks. The palette shader fills the channels from semantic appearance state and can add shiny, glow, or glisten finishes. If masks are absent, the existing whole-sprite tint is the compatibility fallback, so genetics can ship before final production sprites.

Texture paths, mask coordinates, shaders and atlas layout never enter Domain/Application saves. Connected multiplayer sends only semantic appearance phenotype values.

## Adding future content

Adding a colour means appending a palette entry without reordering old indices. Adding a pattern means appending an aligned pattern mask at its semantic pattern index. Adding a coat means appending a semantic coat ID and its presentation material behavior. None of these operations should require changes to breeding inheritance, lineage, race simulation, or consumer-specific sprite code.
