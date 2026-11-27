#!/usr/bin/env python3
"""Regression gate for 6 oath trials / 13 indestructible sarcophagi."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

base = Path(__file__).resolve().parents[1] / "Aragorn_ArmyOfTheDead"
trials = (base / "Source" / "AragornClassTrials.cs").read_text(encoding="utf-8")
gizmos = (base / "Source" / "CombatOnlyPatches.cs").read_text(encoding="utf-8")
codes = (base / "Source" / "AragornGame.cs").read_text(encoding="utf-8")
metadata = ET.parse(base / "About" / "About.xml").getroot()

expected = [3, 3, 2, 2, 2, 1]
assert sum(expected) == 13
assert "new [] { 3, 3, 2, 2, 2, 1 }" in trials
assert "int[] markerCounts = { 3, 3, 2, 2, 2, 1 }" in trials
assert "Scribe_Collections.Look(ref trialActivatedSarcophagi" in trials
assert "Scribe_Values.Look(ref trialKingPhase" in trials
assert "LegacyTrial" in trials, "active v0.36 encounters need legacy save handling"
assert "MarkerActivated(i)" in trials and "SealedSarcophagi()" in trials

# The player action and all effect stages must never physically remove a coffin.
open_grave = trials.split("private void OpenTrialGrave(Pawn pawn)", 1)[1].split(
    "private void BreakTrialSeal(Pawn pawn)", 1)[0]
break_seal = trials.split("private void BreakTrialSeal(Pawn pawn)", 1)[1].split(
    "private void BeginAxeDuel(Pawn pawn)", 1)[0]
assert ".Destroy(" not in open_grave, "opening a grave deletes its coffin"
assert ".Destroy(" not in break_seal, "breaking a seal deletes its coffin"

for required in [
    "IsQuestSarcophagus(target)",
    "AragornOathSarcophagusNoDestroy",
    "AragornOathSarcophagusNoDeconstruction",
    "trialOpenedGraveIndices",
    "TowerStillGuarded(grave)",
    "trialDuelVictories >= 2",
    "trialKingPhase >= 5",
    "trialKingPhase < 5",
    "trialWavesSpawned >= 3",
    "MarkSarcophagusOpened(target.index)",
    "AragornBombardierCryptLayout.Build(map)",
    "AragornSwordCryptLayout.Build(map)",
]:
    assert required in trials, "missing feature: " + required
assert "TrialSarcophagusInspect(__instance)" in gizmos
assert "Unlocked = Math.Max(Unlocked, ClassEnd(trialIndex))" in trials
assert "QuestEndOutcome.Success" in trials
assert metadata.findtext("packageId") == "aragorn.armyofthedead"
deps = [el.findtext("packageId") for el in metadata.findall("./modDependencies/li")]
assert "brrainz.harmony" in deps and "botchjob.profaned" in deps
assert "botchjob.divineorder" in deps
assert "Ludeon.RimWorld.Biotech" not in deps
assert "Ludeon.RimWorld.Royalty" not in deps

for language in ["German", "English"]:
    tree = ET.parse(base / "Languages" / language / "Keyed" / "AragornArmy.xml")
    values = {e.tag: e.text or "" for e in tree.getroot()}
    for key in ["AR_OathGraves_OpenProgress", "AR_OathGraves_GuideBody",
                "AR_OathGraves_KingWave", "AR_OathGraves_Indestructible",
                "AR_Class_Description0", "AR_Class_Description5"]:
        assert values.get(key), f"missing {language} {key}"
print("All six quest state machines, thirteen protected coffin objectives, "
      "old-save migration, translation files and metadata checks passed.")
