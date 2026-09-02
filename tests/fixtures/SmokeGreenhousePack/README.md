# Developer smoke fixture

This original, playable map exists only to verify native content-pack discovery,
the required Back, Buildings, Front, and first-Farm-warp map contract, and the
live greenhouse switch workflow. It references Stardew Valley's built-in
`townInterior` tilesheet, fills the whole walkable area, marks the centre and
keeps a visible path to the Farm exit at the bottom edge. It must not be
distributed as a user-facing pack.

Select it only through the public SDVKit review as
`--content-pack .\tests\fixtures\SmokeGreenhousePack`; do not copy it into a
normal or mod-manager-owned `Mods` directory.
