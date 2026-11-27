# v0.38 – Überdachte große Krypten und taktische Profaned-Stellungen

**Ziel:** Nicht mehr bloß rechteckige Arenen. Neue Karten werden VOR Gegnern und Sarkophagen vollständig freigeräumt; dann entstehen geschlossene Räumlichkeiten, tragende Wände, Türen, Innenüberdachung und nach Möglichkeit neu aufgebaute Kartenvernebelung. Geisterfreischaltung und die sechs Questabläufe bleiben unverändert.

| Klassenquest | Kartenfläche | Geschlossene Bereiche | Taktische Besonderheit |
|---|---:|---|---|
| Schwertgeister | **64 × 44** | Genehmigter 52×32-Kern plus Nordwache, südliches Ossuarium und östliche Reliquienkammer | Sieben Skelettstellungen; 3 unzerstörbare Gräber, Profaned-Steinstelen |
| Bogenschützen | **71 × 55** | Drei unterschiedliche überdachte Grabkammern, zwei seitliche Wachstuben | Jede Gruppe steht hinter einer eigenen Barrikadenfront; getrennte LordJob_DefendPoint-Verbände |
| Bombardiere | **65 × 49** | West-/Ost-Ritualkapelle und nördliches Geisterheiligtum überdacht; zentrale Hallen offen | Fünf Barrikadenzonen (Tor, Kreuzung, 2 Kapellen, Heiligtum); Profaned-Magier werden hinter Deckung eingesetzt |
| Axtgeister | **71 × 55** | Zwei verfluchte Ghulhallen, Vorraum, abgeschlossener Duellring | Ghulgruppen verteidigen ihre Halle, zwei 1v1-Duelle in der überdachten Arena |
| Hauptleute | **71 × 55** | Zwei Hauptmannsgrabkammern hinter dreifacher Verteidigungslinie | Drei gestaffelte Angriffsphasen, zwei getrennte Hauptmannsärge |
| König | **71 × 55** | Großer überdachter Thronsaal mit Säulen, zwei Wachstuben | Leibgarde, königlicher Eidsarg, drei Elitewellen und offen gelegene Blitz-Zone |

**Erkundung:** Nach Bauen sämtlicher Räume wird die FogGrid-Refog-Funktion von RimWorld 1.6 aufgerufen; offene Außenflächen bleiben auf der Karte sichtbar, die abgeschlossenen Innenräume sollen zunächst dunkel und unbekannt sein. Diese Fog-Funktion wurde über die tatsächlich existierende Methode `FogGrid.Refog` geprüft. Die wirkliche Fog-Darstellung und das Öffnen der Türen müssen zusätzlich im Spiel getestet werden.

**Profaned-Ästhetik:** Steinböden aus ProfanedTileSlate (Fallback: FlagstoneSlate), nach Möglichkeit BotchJob_ProfanedBrazierDarklight, BotchJob_ProfanedBrazier, BotchJob_ProfanedLantern sowie vorhandene Stele-Defs. Fehlende optionale Deko zerstört niemals einen Questfortschritt oder benötigt neue Mods.

**Gefahr und Gegner-KI:** Die bestehende AragornThreat.ScaleCount bleibt unverändert. Nahkampf-Gegner warten in ihren Kammern; Bogenschützen und Fluchkundige erscheinen auf explizit vorgesehenen begehbaren Feldern hinter Barrikaden. Die `LordJob_DefendPoint`-Gruppen halten die jeweilige Position; es werden keine neuen Laufwege durch unpassierbare Grabobjekte oder abgeschlossene Türen abgekürzt.

**Akzeptanzkriterien:** Alle 13 unzerstörbaren Sarkophage an ihren logischen Orten, Gebäudefläche vollständig freigeräumt, geschlossene Räume erreichbar, durchgängig belegte Außenmauern, passende Profaned-Einrichtung, unveränderte Waffen und Bilddateien. CI prüft die statische Wegeführung und Kompilierung; der RimWorld-Livetest ist weiterhin erforderlich.
