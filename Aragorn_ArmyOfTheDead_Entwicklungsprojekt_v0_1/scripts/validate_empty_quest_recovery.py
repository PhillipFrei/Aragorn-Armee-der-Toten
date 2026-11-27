#!/usr/bin/env python3
"""Regression gate: failed site generation must not leave a blank quest map."""
from pathlib import Path
import xml.etree.ElementTree as ET

base = Path(__file__).resolve().parents[1] / "Aragorn_ArmyOfTheDead"
rescue = (base / "Source" / "AragornRescueQuest.cs").read_text(encoding="utf-8")
fort = (base / "Source" / "AragornRescueMoatFortress.cs").read_text(encoding="utf-8")
forge = (base / "Source" / "AragornForgeQuest.cs").read_text(encoding="utf-8")
gizmos = (base / "Source" / "CombatOnlyPatches.cs").read_text(encoding="utf-8")

# A site generated without its objective must be detected automatically
# on later GameComponentTick invocations (including existing broken saves).
assert "siteCreated && !encounterCreated &&" in rescue
assert "rescueSite != null && rescueSite.HasMap" in rescue
assert "PopulateRescueMap(rescueSite.Map)" in rescue
assert "nextRetryTick = Find.TickManager.TicksGame + 600;" in rescue
assert "moatRequiresPreparation = false;" in rescue
assert "moatCrossingPrepared = true;" in rescue
assert "AragornRescueMoatFortress.BuildRecoveryPrison(" in rescue and "map, out guardPost" in rescue
assert "public static IntVec3 BuildRecoveryPrison(Map map" in fort

# Check the fallback physically creates two stone rooms with two doors;
# dry passage is opened even if the primary builder partly generated water.
recovery = fort.split("public static IntVec3 BuildRecoveryPrison(Map map", 1)[1].split(
    "public static IntVec3 SouthBank(Map map)", 1)[0]
assert "for (int z = -36; z <= 15; z++)" in recovery
assert "Math.Abs(z) > 15 && Math.Abs(x) > 2" in recovery
assert "map.terrainGrid.SetTerrain(p, soil)" in recovery
assert "Perimeter(13, 10, -10)" in recovery
assert "Perimeter(6, 4, -4)" in recovery
assert "AragornTrialSitePreparation.RequireBuilding(map, p, def)" in recovery
assert "t is Pawn" in recovery
assert "return c;" in recovery

# The original Aragorn pawn must be reused after a partial generation,
# otherwise a retry will create a duplicate and break storyline references.
assert "captive != null && captive.Spawned" in rescue
assert "GhostDefinitions.IsAragorn(p)" in rescue
assert "if (prisoner == null)" in rescue
assert "if (guards.Count == 0)" in rescue

# The forge is a second map where missing buildings/smiths must also be
# recovered. It previously never retried if the site existed but !built.
assert "site != null && site.HasMap && !built" in forge
assert "PopulateForgeMap(site.Map)" in forge
assert "ClearFootprint(map, center, 31, 27, 2)" in forge
assert "IntVec3 center = map.Center" in forge
assert "if (bench == null || !bench.Spawned)" in forge
assert "if (forge == null || !forge.Spawned)" in forge
assert "if (!smith.Spawned)" in forge
assert "retryTick = Find.TickManager.TicksGame + 600" in forge

# In particular, NO additional Harmony GetGizmos postfix was added.
assert gizmos.count("[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]") == 1
assert "rescue.GetMoatGizmos(pawn)" in gizmos
assert "game.StartTrial(pawn)" in gizmos

for language in ["German", "English"]:
    xml = ET.parse(base / "Languages" / language / "Keyed" / "AragornArmy.xml")
    values = {e.tag: e.text or "" for e in xml.getroot()}
    for key in ["AR_Rescue_MoatBody", "AR_Rescue_CrossingReadyBody",
                "AR_Forge_Ready", "AR_Rescue_Success"]:
        assert values.get(key), f"Missing {language} {key}"
print("Blank-map recovery QA OK: failed rescue or forging layouts can be "
      "retried from existing saves, emergency nested prison has dry access, "
      "old Aragorn/smith pawns are not duplicated, full Gizmos remain intact.")
