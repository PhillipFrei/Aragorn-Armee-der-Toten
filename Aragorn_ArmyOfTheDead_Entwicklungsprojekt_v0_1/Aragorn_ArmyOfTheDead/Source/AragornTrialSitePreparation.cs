using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AragornArmy
{
    // Called BEFORE any crypt walls, decorations, enemies or sarcophagi spawn.
    // Vanilla map generation can put mountains, thick rock roofs, ruins,
    // water or vegetation right across the intended mission layout.
    // Previous map builders cleared plants / some Mineable* names only,
    // so "if any Building" silently skipped half the intended walls.
    internal static class AragornTrialSitePreparation
    {
        public static void ClearFootprint(Map map, IntVec3 center,
            int width, int height, int margin = 3, Func<IntVec3, bool> footprint = null)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (width <= 0 || height <= 0 || margin < 0)
                throw new ArgumentOutOfRangeException(nameof(width));

            int minX = center.x - width / 2 - margin;
            int minZ = center.z - height / 2 - margin;
            int maxX = center.x - width / 2 + width - 1 + margin;
            int maxZ = center.z - height / 2 + height - 1 + margin;

            // Do the bounds check *before* touching any tile. An undersized
            // site must fail cleanly and get retried, not leave half a crypt.
            if (minX < 0 || minZ < 0 ||
                maxX >= map.Size.x || maxZ >= map.Size.z)
                throw new InvalidOperationException(
                    "[Aragorn] Quest site too small for complete building " +
                    width + "x" + height + " (+ " + margin + " cell clearance).");

            int removedThings = 0, removedRoofs = 0, changedTerrain = 0;
            TerrainDef buildableGround = TerrainDefOf.Soil;
            if (buildableGround == null)
                throw new InvalidOperationException(
                    "[Aragorn] Vanilla Soil terrain is unavailable.");

            // 1. Remove existing natural / overhead rock first. If mountain
            // supports disappear while their thick roof remains, vanilla may
            // collapse it onto new architecture or pawns. All quest buildings
            // make their own supported roofs later, where appropriate.
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                IntVec3 cell = new IntVec3(x, 0, z);
                if (footprint != null && !footprint(cell)) continue;
                if (map.roofGrid.RoofAt(cell) != null)
                {
                    map.roofGrid.SetRoof(cell, null);
                    removedRoofs++;
                }
            }

            // 2. Clear the ENTIRE construction footprint, not just planned
            // floors. In RimWorld natural mountain rock is a Building, and
            // it may have a defName that does not begin with "Mineable".
            // Avoid touching pawns; the quest map is built before its custom
            // enemies and objectives have been spawned.
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                IntVec3 cell = new IntVec3(x, 0, z);
                if (footprint != null && !footprint(cell)) continue;
                foreach (Thing thing in cell.GetThingList(map).ToList())
                {
                    if (thing == null || !thing.Spawned || thing is Pawn)
                        continue;

                    // Never silently skip an existing wall or boulder.
                    // It would prevent the intended new wall from spawning.
                    try
                    {
                        // Some natural map objects (notably SteamGeyser) cannot
                        // be Destroy()'d. DeSpawn removes those map blockers
                        // without violating their indestructible ThingDef.
                        if (!thing.def.destroyable)
                            thing.DeSpawn(DestroyMode.Vanish);
                        else
                            thing.Destroy(DestroyMode.Vanish);
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException(
                            "[Aragorn] Cannot clear " + thing.def.defName +
                            " at " + cell + " before constructing the trial.", e);
                    }

                    if (thing.Spawned)
                        throw new InvalidOperationException(
                            "[Aragorn] Blocker survived at " + cell +
                            ": " + thing.def.defName);
                    removedThings++;
                }

                // 3. Replace deep water, marsh, rough stone and other
                // non-buildable ground BEFORE new floors or walls are placed.
                if (cell.GetTerrain(map) != buildableGround)
                {
                    map.terrainGrid.SetTerrain(cell, buildableGround);
                    changedTerrain++;
                }

                // A rare impassable terrain from another mod must not result
                // in an incomplete (but seemingly successful) quest building.
                if (!cell.Standable(map) && !cell.GetThingList(map).Any(t => t is Pawn))
                    throw new InvalidOperationException(
                        "[Aragorn] Trial construction cell remains blocked: " + cell);
            }

            // 4. Verify the construction area really is free of OTHER
            // buildings before returning control to any of the three builders.
            // If not, deliberately fail the site build instead of spawning a
            // partially missing building and trapping quest progress.
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                IntVec3 cell = new IntVec3(x, 0, z);
                if (footprint != null && !footprint(cell)) continue;
                Thing blocker = cell.GetThingList(map).FirstOrDefault(t =>
                    t is Building && t.Spawned);
                if (blocker != null)
                    throw new InvalidOperationException(
                        "[Aragorn] Building footprint not free at " + cell +
                        " (" + blocker.def.defName + ").");
            }

            Log.Message("[Aragorn] Prepared " + width + "x" + height +
                " quest structure area (+ " + margin + " margin): " +
                removedThings + " old objects, " + removedRoofs +
                " roofs, " + changedTerrain + " terrain cells cleared.");
        }

        public static void RequireBuilding(Map map, IntVec3 cell, ThingDef expected)
        {
            if (expected == null || !cell.InBounds(map) ||
                !cell.GetThingList(map).Any(t => t.Spawned && t.def == expected))
                throw new InvalidOperationException(
                    "[Aragorn] Missing " + (expected?.defName ?? "building") +
                    " at required quest-structure cell " + cell +
                    ". Refusing to finish incomplete quest map.");
        }
    }
}
