# Aragorn-Rettungsmission – Wassergraben und Gefängnis im Raum (v0.39)

**Aufbau:** Vor dem Kartenerstellen wird das vollständige 77 × 65-Felder-Baufeld inklusive Sicherheitsrand geräumt. Die Festung wird in der Mitte aufgebaut, unabhängig von natürlichem Fels, Dächern und Flüssen.

## Draufsicht, schematisch

```text
              NORDEN
      ~~~~~~~~~~~~~~~~~~~~~~~~~~~
      ~      TIEFWASSER         ~
      ~  +-------------------+  ~
      ~  | Wachhaus  Wachhaus|  ~
      ~  |                   |  ~
      ~  |    +---------+    |  ~
      ~  |    | BERGFRIED|    |  ~
      ~  |    | +-----+ |    |  ~
      ~  |    | |ARAGORN| |   |  ~
      ~  |    | +--T--+ |    |  ~
      ~  |    +----T----+    |  ~
      ~  | Wachhaus  Wachhaus|  ~
      ~  +--------TTT--------+  ~
      ~~~~~~~~~~BBB~~~~~~~~~~~~~
                BBB
              SÜDEN
```

- **~** = durchgehender, unpassierbarer Wassergraben aus `WaterDeep` (6–7 Felder tief)
- **BBB** = einzige, drei Felder breite, begehbare Brücke im Süden
- **T** = verschließbare Türen, davor gegnerische Profaned-Wachen
- **äußerer Mauerring** = 53×41 Felder mit dreifachem Südtor
- **innerer Bergfried** = überdachter 27×21-Felder-Raum mit einer Tür
- **Aragorns Gefängnis** = eigener, komplett ummauerter und überdachter 13×9-Felder-Raum *im Bergfried*
- **vier Wachhäuser** = Profaned-Laternen, verfluchte Feuer und Deckung im äußeren Hof

## Spielablauf

1. Karawane nähert sich der Festung; das tiefe Wasser umschließt den gesamten Bau.
2. Sie muss die einzige Südbrücke benutzen und den bewachten Haupteingang überwinden.
3. Im Hof halten Profaned-Wachen an fünf verschiedenen Stellungen Wache.
4. Eine zweite Tür führt in den überdachten Bergfried, dahinter die dritte Tür in Aragorns Gefängnis.
5. Die bisherigen Freischaltbedingungen bleiben: Wenn alle Profaned-Wachen gefallen sind, wird Aragorn befreit und schließt sich an.

**Technik:** Die Brückenoberfläche wird bevorzugt mit Vanilla-`Bridge` erzeugt; falls sie im Deep-Water-Bereich nicht begehbar ist, wird der Übergang als begehbarer Steinweg hergestellt. Alle anderen Grabenfelder bleiben `WaterDeep`.

**Spielstände:** Bereits besuchte Rettungsorte werden nicht rückwirkend umgebaut. Mit einem Spielstand vor dem ersten Betreten kann diese Version neu generiert werden. Schwert- und Schmiede-Quests sowie die sechs Geisterprüfungen bleiben unangetastet.

**Prüfung:** Automatischer 4-Wege-Pfadtest kontrolliert, dass die Brücke die einzige Landverbindung durch den Wassergraben ist und die äußere Festung, der Bergfried, das innere Gefängnis sowie alle fünf Wachpositionen erreichbar sind.
