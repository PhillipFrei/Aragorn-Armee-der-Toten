# Sechs Eidesprüfungen – v0.37
## Verbindlicher Quest- und Testplan

**Gleichbleibende Regeln:** 13 Geister behalten ihre bisherigen Waffen/Grafiken. Der globale Gefahr-Regler bestimmt Anzahl der Profaned-Verteidiger. Freischaltstand und begonnene Quests bleiben in Saves gespeichert. Jede der sechs Klassenquests verwendet ausschließlich **unzerstörbare Sarkophage**. Ein Sarkophag wird durch die zugehörige Kolonistenaktion **entsiegelt, nicht physisch zerstört**. Sein Status erscheint in der Inspektionsanzeige. Questbrief beim Betreten erklärt Anzahl, Aktion und Abstand. Alte bereits begonnene Karten aus v0.36 werden über die frühere Missionslogik zu Ende geführt.

| Quest | Särge | Freischaltungen | Gelände / Schwerpunkt |
|---|---:|---|---|
| 1. Drei Gräber des Eides | 3 | Schwertgeister 1–3 | Bergkrypta 52×32, 7 Skelettposten mit 4 Skeletten pro Position bei 1× Gefahr (14 gesamt bei 0,5×), 4 Eliteposten |
| 2. Schattenzinnen | 3 | Geisterbogenschützen 4–6 | Drei befestigte Schützenplattformen, viele Profaned-Bogenschützen, ein Grab pro Plattform |
| 3. Feuer des Fluchs | 2 | Bombardiere 7–8 | 65×49-Ritualkrypta mit zwei Magierkapellen und zwei geschützt wartenden Geistern |
| 4. Axtschwur | 2 | Axtgeister 9–10 | Zwei Ghulhallen und ein ummauerter Duellkreis mit zwei Sarkophagen |
| 5. Banner der Verlorenen | 2 | Hauptleute 11–12 | Festung mit drei verteidigten Linien, drei zeitlich getrennten Elitewellen |
| 6. Letzter Eid des Königs | 1 | König 13 | Thronsaal, königliche Elite, drei weitere Angriffswellen, stärker werdender Blitzsturm |

### 1 · Schwertgeister
1. Betritt die bereits abgestimmte Bergkrypta und bekämpfe die getrennten Untotenposten.
2. Nach dem Sieg über die Wachen werden die drei Sarkophage freigegeben.
3. **Beliebige Reihenfolge:** Einen Kolonisten neben einen ungeöffneten Sarg stellen, „Grab öffnen“ wählen. Genau der dazugehörige Schwertgeist erhebt sich; der Sarg bleibt stehen.
4. Alle drei Geister besiegen, dann 3/13 freischalten.

### 2 · Bogenschützen
1. Unter Beschuss der drei Stellungen in die Festung eindringen.
2. Jeweilige Profaned-Bogenschützen am Turm besiegen.
3. Den Sarg der Stellung entsiegeln. Dadurch wird der bislang geschützte Geisterbogenschütze dort verwundbar.
4. Alle drei Stellungen (beliebige Reihenfolge) nehmen und Geister besiegen. Danach 6/13 freigeschaltet.

### 3 · Bombardiere
1. Zwei Seitenkapellen mit Profaned-Fluchkundigen säubern.
2. Einen Sarkophag in einer Kapelle entsiegeln. Der Blitzsturm wird seltener; der Sarg bleibt unversehrt.
3. Zweite Kapelle und zweiten Sarg entsiegeln. Blitze enden und beide Bombardiere im Heiligtum werden verwundbar.
4. Geister und restliche Wachen bezwingen, dann 8/13 freigeschaltet.

### 4 · Axtgeister
1. Erst beide Ghulhallen vor dem Ring säubern.
2. Genau einen Kolonisten zum ersten der zwei Särge in den Duellkreis schicken. Nur der ausgewählte Kämpfer kann den Geist schädigen.
3. Den zweiten Sarg aktivieren und den zweiten Geist einzeln herausfordern. Bei Niederlage eines Herausforderers kann ein neuer Kämpfer den bestehenden Geist herausfordern.
4. Beide Duelle gewinnen, dann 10/13 freigeschaltet.

### 5 · Hauptleute
1. Angreifer an den drei gestaffelten Verteidigungslinien zurückschlagen.
2. Drei Profaned-Elitewellen vollständig besiegen. Neue Wellen besitzen eine eigene Lord-KI.
3. Danach in den inneren Hof zu beiden Hauptmannsärgen gehen und separat entsiegeln.
4. Beide dadurch verwundbar gewordenen Hauptleute bezwingen, dann 12/13 freigeschaltet.

### 6 · König
1. Die königliche Leibgarde im Thronsaal besiegen. Der König ist zunächst unverwundbar.
2. Den einen königlichen Sarkophag entsiegeln; Blitze beginnen.
3. Drei schwere Elitewellen ziehen nacheinander heran, begleitet von immer häufigeren Blitzen.
4. Nach der dritten Welle fällt der Schutz des Königs. Endkampf gegen den König; danach 13/13 freigeschaltet.

### Qualitätskriterien

- Zahl 3/3/2/2/2/1 ist für alle Missionsarten gesichert.
- Sieg ist nur bei vollständiger Freischaltung der passenden Ziele möglich.
- Aktive Questsarkophage sind gegen Schaden, Blitz und Demontage geschützt.
- Harmony-Filter betreffen nur registrierte Questobjekte, keine gewöhnlichen Gräber.
- Gespeicherte Questfreischaltungen bleiben erhalten; Abbruch und Wiederanbieten funktionieren weiter.
- GitHub CI prüft XML, Übersetzungen und Kompilierung; echte KI- und Renderingtests erfordern RimWorld.
