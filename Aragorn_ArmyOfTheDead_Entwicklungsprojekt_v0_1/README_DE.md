# Aragorn – Armee der Toten · Entwicklungsprojekt v0.1

**Status: C#-Quellprojekt für RimWorld 1.6; nicht lokal kompiliert oder im Spiel getestet.**

## Was bereits im Quellcode umgesetzt ist

- Neue Waffe `AR_Anduril`: Basis wie das Grasping Dead Greatsword (34 Schaden / 2,85 s), aber 150 % Rüstungsdurchdringung; nur Aragorn darf sie ausrüsten.
- `AR_Aragorn` als eigener PawnKind. Bei Erzeugung erhält er die 13 vereinbarten Biotech-Gene, die Merkmale Jogger (SpeedOffset 2), Fast Learner, Optimist (NaturalMood 1) und Beautiful (Beauty 2), Nahkampf/Schießen 18 mit großer Leidenschaft sowie Crusader Armor und Divine Order Tunic.
- 13 nacheinander freischaltbare Geister in sechs `PawnKindDef`-Klassen, jeweils mit genau der vereinbarten Waffe (König: Eltex-Stab aus Royalty).
- Beschwörungs-Gizmo nur bei Aragorn mit **ausgerüstetem Andúril**. Beschwörungsdauer 6–13 Ingame-Stunden; 13 Stunden Cooldown ab Aktivierung; Rückruf-Gizmo; jede neue Beschwörung erzeugt die Geister mitsamt ihrer festen Waffe neu.
- Geister ohne normale Bedürfnisse, ohne eigene Kleidung, ohne Rüstungsbeute, ohne regulären Waffenwechsel und standardmäßig dauerhaft eingezogen, also ausschließlich zum Kämpfen gedacht. Diese Eingriffe per Harmony sind **noch nicht in einem Spieltest auf Modkonflikte geprüft**.
- Natürliche Rüstung gegen Scharf/Stumpf/Hitze: 20 globale Stufen (Stufe 1: 50 %, Stufe 10: 1000 %, Stufe 20: 2000 %) mit sechs gesonderten Klassenfaktoren.
- Königs-Hediff "Königlicher Befehl": x1,5 / 2 / 2,5 / 3 für Bewegung und Kampfgeschwindigkeit aller anderen aktiven Geister. Automatisch entfernt, wenn kein aktiver lebender König auf der Karte ist (Prüfintervall: 60 Ticks).
- König mit echtem Royalty-Eltex-Stab für normale Nahkampf-Angriffe. Gezielt auslösbare Blitzfähigkeit: 15 Felder Zielreichweite, Zielkreis-Radius 6, 5 Blitze, je 20 Hitzeschaden / 300 % Rüstungsdurchdringung, 7500 Ticks Cooldown. Beim 13. lokalen Testboss automatische Aktivierung.
- Andúril-Rundumschlag: 60 Schaden, 150 % Durchdringung, Radius 3, einfacher Rückstoß, 1500 Ticks vorläufiger Cooldown.
- **Lokale Testbegegnungen** für alle 13 Freischaltungen: jeweils 1 Geisterboss plus 5–50 The-Profaned-Kämpfer mit den Original-Auswahlgewichten des Profaned-`PawnGroupMaker`. Freischaltung bei getötetem oder kampfunfähigem Boss, wenn Aragorn mit Andúril auf derselben Karte ist.
- Provisorisches eigenes Andúril-Icon und türkisfarbene Geisterhaut sowie kleine Partikel.
- Speichern/Laden des Freischaltstands, Timern und aktueller Geister.

## Was ausdrücklich NOCH fehlt

1. **Die eigentliche Weltkarten-Rettungsquest für Aragorn**. In v0.1 wird Aragorn testweise über den Entwickler-Modus erzeugt und bekommt Andúril sofort. Das ist ein Test-Start, keine finale Questbelohnung.
2. **13 separate Weltkarten-Quests und die Kryptenlayouts** (`BotchJob_DarkCrypt` aus KCSG). In v0.1 erscheinen Boss und Profaned-Wachen für den Mechaniktest auf der aktuellen Karte. Das kann gefährlich für deine Kolonie werden!
3. Film-inspirierte **Rüstungs-/Körpertexturen, tatsächliche Transparenz und Animationen**. Bis dahin: einfache türkis gefärbte Human-Pawns und Partikel. Die vorgeschlagenen visuellen Effekte sind noch keine finalen 3D/2D-Grafiken.
4. Ein echter zeichnerischer **Blitzstrahl**. Der aktuelle Prototyp nutzt die vorhandenen Leucht-/Partikeleffekte; die Schadensverteilung ist programmiert.
5. Ein Gameplay-Test und eine erfolgreiche Kompilierung in einer echten RimWorld-1.6-Umgebung.
6. Vollständige Kompatibilität mit Search and Destroy, Run and Gun, Armor Up und allen Kombinationen mit Combat Tweaks. Diese muss im Spiel geprüft werden.

## Projekt kompiliert man ohne eigene IDE über GitHub Actions

**Wichtig:** Dieses ZIP enthält deine bereitgestellte `Assembly-CSharp.dll` und zwei Unity-Referenz-DLLs nur zum Kompilieren. Verwende für das Repository **Privat** und gib die Referenzdateien nicht weiter.

1. Entpacke dieses ZIP und erstelle ein **privates GitHub-Repository**.
2. Lade **den gesamten Inhalt des ausgepackten Ordners** hoch: `Aragorn_ArmyOfTheDead/`, `_references/` und `.github/workflows/build.yml`.
3. Unter **Actions → Build RimWorld Mod → Run workflow** starten.
4. Nach erfolgreichem Build in der Workflow-Ausführung das Artifact **`Aragorn-ArmyOfTheDead-build`** herunterladen.
5. Darin befindet sich der vollständige Modordner `Aragorn_ArmyOfTheDead` mit `1.6/Assemblies/Aragorn_ArmyOfTheDead.dll`.
6. Den Modordner im lokalen RimWorld-`Mods`-Verzeichnis ablegen (auf Mac den Installationsordner am einfachsten in Steam über *Lokale Dateien durchsuchen* öffnen), Spiel neu starten und die Mod aktivieren.
7. Harmony, Biotech, Royalty, The Profaned und The Divine Order müssen aktiviert sein.

**Ein roter Build schlägt fehl:** Bitte den Compiler-Fehlertext aus dem Action-Protokoll zur Fehlersuche schicken. Da vorliegend kein lokaler C#-Compiler installiert ist, kann ich einen erfolgreichen Build nicht zusichern.

## Erster Mechaniktest (ausschließlich in einer Testkopie des Spielstands)

1. Stelle im RimWorld-Hauptmenü unter *Optionen → Entwickler-Modus* den Entwicklermodus an.
2. Öffne einen **Testspielstand** auf der gewünschten Karte und erzeuge über das Spawn-Pawn-Entwicklerwerkzeug `AR_Aragorn`. Warte kurz, bis er seine geplanten Gene, Traits, Kleidung und Andúril erhalten hat.
3. Wähle Aragorn an und klicke **„Nächste Geisterprüfung (Test)“**. Das erzeugt einen Gegnerboss mit der eingestellten Anzahl Profaned-Einheiten in der Nähe!
4. Besiege den Geisterboss, während Aragorn auf der Karte mit Andúril ausgerüstet ist. Seine Freischaltung zählt auch, wenn ein anderer Pawn ihn niederstreckt.
5. Klicke **„Geister beschwören“**. Teste Auswahl, Einziehen, Bewegung, Kampf, Rückruf und erneute Beschwörung nach Cooldown.
6. Für den Königsbuff und die Blitze alle 13 Prüfungen bestehen; alternativ die gespeicherte `unlocked`-Variable später über Debug-Einstellungen anpassen (noch kein eigenes Dev-Gizmo).

## Technische Quellen

- RimWorld-1.6-Decompile: `Verse/Pawn.cs`, `RimWorld/StatWorker.cs`, `RimWorld/Pawn_NeedsTracker.cs`, `Verse/Pawn_EquipmentTracker.cs`, `Verse/Game.cs`, `RimWorld/Targeter.cs` — https://github.com/32bitx64bit/RW-Decompiled-1.6
- The Profaned: `Factions_Profaned.xml`, `PawnKinds_Profaned.xml`, `Weapons_Profaned.xml` aus deiner Archivkopie.
- The Divine Order: Original-Weapon/Apparel-Definitionen aus deiner Archivkopie.
- Zu beachten: Die *Armor Deflection Rework*-Option von Combat Tweaks behandelt natürliche Rüstung im Quellcode gesondert. Unser Mod ist nicht davon abhängig, aber die Wechselwirkung kann weiterhin bestehen.
