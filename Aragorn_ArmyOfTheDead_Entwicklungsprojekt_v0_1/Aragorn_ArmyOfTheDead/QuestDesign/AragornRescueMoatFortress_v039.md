# Aragorn-Rettungsmission – Wassergraben und verschachteltes Gefängnis (v0.39.1)

**Größe:** 77 × 65 Felder. Ein 6–7 Felder tiefer, anfangs ununterbrochener `WaterDeep`-Ring umgibt einen äußeren Mauerring von 53 × 41 Feldern.

## Grundriss, schematisch

```text
                  NORDEN
      ~~~~~~~~~~~~~~~~~~~~~~~~~~~
      ~        TIEFWASSER       ~
      ~   +-----------------+   ~
      ~   |   [===] [===]   |   ~
      ~   |   Bogenschützen |   ~
      ~   |   +---------+   |   ~
      ~   |   |SKELETTE |   |   ~
      ~   |   | +-----+ |   |   ~
      ~   |   | |ARAGORN|    |   ~
      ~   |   | +--T--+ |   |   ~
      ~   |   +----T----+   |   ~
      ~   |   [===] [===]   |   ~
      ~   |   Bogenschützen |   ~
      ~   +-------TTT-------+   ~
      ~~~~~~~~~~~~~~~~~~~~~~~~~~~
                ??????
            SÜDLICHES UFER
```

- **~** = durchgehender Tiefwassergraben ohne fertige Brücke
- **??????** = vom Spieler zunächst selbst herzustellender Übergang
- **[===]** = vier befestigte Barrikadenfronten mit Profaned-Bogenschützen hinter Deckung; **keine Wachhäuser oder Türme**
- **T** = Türen im äußeren Mauerring, zum Bergfried und zu Aragorns Innengefängnis
- **Bergfried:** überdachter 27×21-Felder-Raum; **zwei Gruppen Skelette greifen Eindringlinge an**
- **Gefängnis:** vollständig eingeschlossene 13×9-Felder-Zelle im Bergfried

## Rettungsablauf

1. Der Spieler trifft auf einen unüberwindbaren Tiefwassergraben. Die Brücke aus v0.39 wurde restlos entfernt; das Wasser bleibt anfangs vollständig `WaterDeep`.
2. Der Spieler bewegt einen Kolonisten an das **südliche Ufer**. Dort erscheint beim Auswählen des Kolonisten das Gizmo **„Übergang bereiten“**.
3. Der Spieler aktiviert das Gizmo, um einen **drei Felder breiten, sieben Felder langen provisorischen Weg** anzulegen. Im Normalspiel kann man über `WaterDeep` keine Vanilla-Brücke bauen; deshalb besitzt die Rettungsmission diese eigene, sichtbare Bauaktion. Es sind **keine zusätzlichen Brückenmods oder Materialien** erforderlich.
4. Über den hergestellten Weg gelangt die Gruppe zum dreiteiligen Außentor.
5. Vier einzelne Gruppen **Profaned-Bogenschützen** halten hinter realen Barrikaden Stellungen, ohne Wachtürme.
6. Im inneren, überdachten Bergfried warten zwei **Skelett-Gruppen** neben der zweiten Mauerschicht auf die Spieler; sie greifen innerhalb ihres Verteidigungsbereichs an.
7. Aragorn liegt weiterhin bewusstlos in der geschlossenen Innenzelle. Sind alle Wachen besiegt, schließt er sich der Kolonie an.

**Technik:** Die Brückenfläche ist zu Beginn unpassierbares `WaterDeep`. Die neue Questaktion verändert bei der Benutzung genau 21 Zellen zu begehbarer Oberfläche, bevorzugt einem Brückenterrain (sonst FlagstoneSlate). Das ist absichtlich nicht der normale Vanilla-Baubefehl, denn Vanilla-Brücken können echtes Tiefwasser nicht überqueren.

**Spielstände:** Begonnene Rettungsmissionen aus v0.39 behalten ihre alte Karte und benötigen keinen neuen Bau-Befehl. Die angepasste Rettungsmap gilt für neu generierte Questorte. Alle übrigen Missionen, Schmiede, Waffen und Grafiken bleiben unangetastet.

**Prüfungen:** Vor der Bauaktion gibt es keinen Pfad vom südlichen Ufer zu Aragorn; danach sind die drei Türen, alle Schützenstellungen und der Gefangene erreichbar. Keine Barrikade darf auf den Bergfriedmauern liegen, keine Skelette im Innengefängnis starten.
