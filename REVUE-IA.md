# REVUE-IA.md

Revues argumentées de propositions produites par l'IA. Trois minimum sont
attendues, dont au moins une adaptée ou rejetée : trois acceptations
n'établiraient rien.

Chaque revue nomme ce qui devait être vrai, l'expérience choisie pour le mettre
en défaut, ce qui a réellement été observé, et ce qui reste non vérifié.

Binôme : Olivier Recher (@OlivierRecher) · Ulysse (@Oulssyyy)

**État : 1 revue sur 3.**

---

## Revue 1 — La vue servie au navigateur peut-elle divulguer la flotte du bot ?

**Proposition examinée**

L'ADR 0003, produit par l'IA pendant la séance de cadrage, résout le secret des
positions en supprimant tout paramètre d'identité : *« le serveur renvoie
toujours la vue du joueur dont c'est le tour »*. L'argument avancé était fort —
sans paramètre, il n'y a plus de contrôle à oublier, donc l'invariant devient
structurellement invulnérable.

Cette règle a été reprise telle quelle dans `AGENTS.md` § 4 et a orienté tout le
contrat d'API avant qu'une ligne de code ne soit écrite.

**Hypothèse à vérifier**

Pour que la proposition soit acceptable, il faut que :

> quel que soit l'état de la partie, la `GameView` transmise au navigateur ne
> contienne aucune information que le joueur humain n'a pas le droit de connaître.

**Expérience**

Scénario minimal : une partie en mode `Solo`, l'humain tire une fois, puis on
demande l'état de la partie.

Résultat attendu, écrit avant exécution : la vue décrit l'humain, `IsViewerTurn`
vaut `false` puisque c'est au bot de jouer, et `OwnFleet` contient la flotte de
l'humain.

Erreur que ce contrôle peut détecter : toute implémentation qui projette la vue
d'après le joueur courant sans distinguer humain et bot. Si la règle de l'ADR est
appliquée littéralement, la vue renvoyée après le tir est celle du **bot** — donc
`OwnFleet` contient la flotte du bot, et le navigateur la reçoit.

**Observation**

Le défaut a été trouvé **par inspection, pas par un test** : il est apparu en
concevant le DTO de réponse au tir, au moment de décider quelle vue y attacher.
C'est une distinction qui compte — aucun test existant ne l'aurait signalé,
puisqu'aucun ne portait sur ce cas.

Le test a été écrit ensuite, et la mutation exécutée pour établir qu'il détecte
bien le défaut :

```
Mutation : ViewForClient() => ViewFor(_current)   // la règle de l'ADR, littérale
Résultat : 1 échec / 50
  Échoué ViewForClient_OnceTheBotIsToPlay_StillDescribesTheHuman
Restauration : 50 / 50
```

La règle de l'ADR était donc **fausse en mode solo**, et elle produisait
exactement la fuite qu'elle prétendait rendre impossible.

**Décision et justification**

**Adaptée.** La décision de fond — aucun identifiant de joueur transmis par le
client — est conservée : elle reste la bonne, et elle supprime réellement une
classe d'erreurs. C'est sa formulation qui était incomplète.

La règle exacte retenue est : **le serveur ne sert jamais la vue d'un bot.**
`ViewForClient()` suit le joueur courant s'il est humain, et bascule sur
l'adversaire humain sinon. En mode `Local` les deux joueurs étant humains, le
comportement d'origine est inchangé.

L'ADR 0003 conserve sa décision initiale et gagne une section exposant l'angle
mort et sa correction. Réécrire l'original aurait effacé l'information la plus
utile : l'écart entre ce qui a été décidé sur le papier et ce que
l'implémentation a révélé.

**Preuves et limites**

| | |
|---|---|
| Correction | commit `a44e064` — `BattleShip.Domain/Game.cs`, `ViewForClient()` |
| Tests | `BattleShip.Tests/Domain/GameViewForClientTests.cs`, 4 cas |
| Frontière HTTP | `GetGame_AfterTheHumanFired_StillDescribesTheHuman` |
| ADR corrigé | commit `112a96f` |

Ce qui **reste non vérifié** :

- La garantie porte sur la frontière HTTP — aucun endpoint n'accepte d'identité
  de joueur — et **non** sur le type `Game`, dont `ViewFor(Player)` reste
  accessible à tout code serveur. Un futur endpoint pourrait l'appeler avec le
  mauvais joueur sans qu'aucun test actuel ne le détecte.
- Les tests couvrent les modes `Solo` et `Local` avec deux joueurs. Une partie
  opposant deux bots n'est pas couverte, parce qu'elle n'existe pas au périmètre.
- Aucun contrôle ne vérifie que la sérialisation JSON n'ajoute pas de champ :
  la garantie repose sur la forme du type `GameViewResponse`, qui ne porte aucun
  membre capable de transporter la flotte adverse.

**Ce que cette revue enseigne pour la suite du projet**

Une décision d'architecture formulée en langage naturel peut être convaincante,
cohérente avec toutes les contraintes du sujet, et néanmoins fausse sur un cas
que personne n'a encore instancié. Ici le mot fautif était « joueur » : le
glossaire le définissait comme humain **ou** bot, et la règle de l'ADR employait
le terme comme s'il ne désignait qu'un humain.

`CONTEXT.md` a été précisé en conséquence — voir l'entrée `ViewForClient`.

---

## Revue 2 — à rédiger

## Revue 3 — à rédiger
