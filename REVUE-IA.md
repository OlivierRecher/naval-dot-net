# REVUE-IA.md

Revues argumentées de propositions produites par l'IA. Trois minimum sont
attendues, dont au moins une adaptée ou rejetée : trois acceptations
n'établiraient rien.

Chaque revue nomme ce qui devait être vrai, l'expérience choisie pour le mettre
en défaut, ce qui a réellement été observé, et ce qui reste non vérifié.

Binôme : Olivier Recher (@OlivierRecher) · Ulysse (@Oulssyyy)

**État : 3 revues sur 3.**

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

## Revue 2 — Un relecteur automatique peut-il prescrire un correctif faux à partir d'un diagnostic juste ?

**Proposition examinée**

Revue Copilot de la PR #1, commentaire sur
`BattleShip.Tests/Domain/GameViewForClientTests.cs:52` :

> This assertion is tautological: `botCells.Except(ownCells)` removes every cell
> that could intersect `ownCells`, so it passes even if the bot fleet is present
> in `ownCells`. The test therefore does not protect the confidentiality
> invariant; **assert directly that the two cell sets do not intersect.**

La remarque tient en deux affirmations distinctes, et c'est cette distinction qui
fait l'objet de la revue : un **diagnostic** — l'assertion ne teste rien — et une
**prescription** — la remplacer par une assertion de disjonction.

**Hypothèse à vérifier**

Le diagnostic est vérifiable par lecture :
`ownCells.Intersect(botCells.Except(ownCells))` est vide par construction, quel
que soit le contenu des ensembles. Il n'appelait pas d'expérience.

La prescription, elle, suppose quelque chose que le commentaire n'énonce pas :

> les cases occupées par la flotte de l'humain et celles occupées par la flotte
> du bot sont disjointes.

C'est cette hypothèse-là qui a été mise à l'épreuve.

**Expérience**

Écrire l'assertion exactement telle que proposée, l'exécuter sur le scénario du
test — partie solo, graine `Random(17)`, flotte standard sur grille 10×10 — et
afficher les cases communes.

Résultat attendu, écrit avant exécution : **si les deux flottes partagent au
moins une case, la prescription est fausse**, car elle ferait échouer le test sur
du code correct.

Erreur que ce contrôle peut détecter : une assertion qui confond « deux flottes »
et « deux grilles ».

**Observation**

```
own=17 bot=17 partagees=5 -> (3,4), (3,5), (3,8), (5,9), (6,9)
```

Les deux flottes partagent 5 cases sur 17. C'était prévisible sans exécuter :
chaque joueur possède **sa propre grille**, et la coordonnée (3,4) de la grille
de l'humain n'a aucun rapport avec la coordonnée (3,4) de celle du bot. Deux
flottes placées au hasard sur deux grilles se recouvrent naturellement.

La prescription aurait donc produit un test rouge sur un code juste — et, pire,
un test que le réflexe naturel aurait « corrigé » en changeant la graine jusqu'à
ce qu'il passe.

**Décision et justification**

**Diagnostic accepté, prescription rejetée.**

L'assertion tautologique est supprimée : elle prétendait protéger la
confidentialité de la flotte et ne protégeait rien. Mais l'invariant à tester
n'est pas « les deux flottes sont disjointes » — cet énoncé est faux dans le
domaine. Il est :

> la vue servie au navigateur décrit la flotte de l'humain, et pas celle du bot.

Traduit en :

```csharp
Assert.False(humanFleet.SetEquals(botFleet), "les deux flottes sont identiques : le test ne prouverait rien");
// ...
Assert.True(served.SetEquals(humanFleet));
Assert.False(served.SetEquals(botFleet));
```

La première ligne est un garde sur le test lui-même : si les deux flottes
étaient identiques, les deux assertions suivantes ne discrimineraient plus rien.
C'est le défaut de l'assertion d'origine, transposé — et donc celui à ne pas
reproduire.

**Preuves et limites**

| | |
|---|---|
| Réfutation | 5 cases communes sur la graine 17, exécutée avant correction |
| Correction | commit `cd4c4dd` — `GameViewForClientTests.cs` |
| Mutation | `ViewForClient()` renvoyant la vue du joueur courant → 2 tests au rouge |
| Fil de revue | PR #1, commentaire `4013902228`, réponse et résolution |

Ce qui **reste non vérifié** :

- La mutation exécutée était déjà détectée par
  `ViewForClient_OnceTheBotIsToPlay_StillDescribesTheHuman`, via `ViewerName`.
  La fuite n'était donc pas totalement sans filet : c'est l'assertion portant
  explicitement sur la **flotte** qui était vide. La correction supprime une
  fausse garantie, elle ne comble pas un trou complet.
- Le test compare des ensembles de coordonnées. Il ne dirait rien d'une fuite qui
  passerait par un autre membre du DTO — cette garantie-là repose sur la forme du
  type `GameViewResponse`, pas sur ce test.

**Ce que cette revue enseigne pour la suite du projet**

Un relecteur automatique raisonne sur le texte du test, pas sur le modèle du
domaine. Il a lu `ownCells` et `botCells` comme deux sous-ensembles d'un même
espace — ce qu'un nom de variable suggère et ce que le code ne dit pas. Son
diagnostic, lui, ne portait que sur la logique ensembliste de la ligne : c'est
exactement le registre où il est fiable.

D'où la règle retenue pour les revues suivantes : **traiter séparément le
défaut signalé et le remède proposé.** Le premier mérite d'être vérifié, le
second d'être reconstruit à partir du domaine.

---

## Revue 3 — Le `ConcurrentDictionary` rendait-il vraiment le stockage sûr ?

**Proposition examinée**

L'ADR 0004, rédigé pendant le socle, conclut :

> **Le cycle de vie Singleton impose une implémentation thread-safe.** C'est la
> raison du `ConcurrentDictionary` plutôt qu'un `Dictionary` : deux requêtes HTTP
> concurrentes touchent la même instance.

Le même raisonnement figurait en commentaire dans `InMemoryGameRepository` et
dans `AGENTS.md` § 4. La revue de la PR #1 l'a contesté sur deux fichiers.

**Hypothèse à vérifier**

> Avec un `ConcurrentDictionary<Guid, Game>`, deux requêtes concurrentes visant
> la même partie ne peuvent pas corrompre son état.

**Expérience**

Deux contrôles, tous deux écrits avant d'implémenter quoi que ce soit :

1. Lancer 400 tirs sur la même partie en `Parallel.ForEach`, compter les appels
   acceptés, comparer à `game.Shots.Count`.
2. Faire projeter `ViewFor` par un fil pendant qu'un autre tire.

Résultat attendu, écrit avant exécution : si l'hypothèse est vraie, les deux
passent. Si elle est fausse, le premier montre un écart entre tirs acceptés et
tirs journalisés, et le second lève une exception d'énumération.

Erreur que ce contrôle peut détecter : toute confusion entre la sûreté de la
**table de stockage** et celle de l'**objet stocké**.

**Observation**

Sur le code du socle, sans verrou :

```
Échoué FireFromClient_CalledConcurrently_RecordsExactlyOneShotPerAcceptedCall
  System.InvalidOperationException : Operations that change non-concurrent
  collections must have exclusive access. A concurrent update was performed on
  this collection and corrupted its state.
```

L'hypothèse est fausse. Le `ConcurrentDictionary` protège `Find` et `Add` — la
table. Il ne protège pas le `Game` qu'il rend, qui reste un agrégat mutable
partagé : un `List<Shot>` et deux champs de tour.

Le chemin n'est pas théorique. `/games/{id}/shots` est en HTTP, `Fire` est en
gRPC-Web, et les deux atteignent le même Singleton.

**Décision et justification**

**Adaptée** — la remarque est retenue, sa localisation ne l'est pas.

Le verrou n'est pas allé dans le repository, comme la revue le suggérait, mais
dans `Game` : un `System.Threading.Lock` privé, **par partie**. Trois raisons.
Le repository ne voit pas les transitions — il rendrait un objet déjà
déverrouillé. Un verrou par partie laisse deux parties distinctes progresser en
parallèle. Et c'est le seul endroit que les deux transports traversent tous les
deux, donc le seul où le garde ne peut pas diverger.

L'option « rendre l'agrégat immuable », également citée par la revue, a été
écartée : elle annule l'ADR 0002 et réécrit le moteur, pour un socle
mono-processus.

Conséquence que la revue ne demandait pas : le choix de la cible du bot est
entré dans l'agrégat (`Game.PlayBotTurn(IBotStrategy)`). Tant qu'il vivait dans
l'endpoint, « vérifier que c'est au bot de jouer » et « tirer » restaient deux
opérations séparées — deux appels concurrents à `/bot-turn` auraient joué deux
tours. **Verrouiller `Fire` seul aurait donné l'illusion d'avoir corrigé le
défaut.**

**Preuves et limites**

| | |
|---|---|
| Correction | commit `cd4c4dd` — `BattleShip.Domain/Game.cs` |
| Tests | `GameConcurrencyTests`, 2 cas |
| Mutation | `lock (_gate)` → `if (true)` → corruption du journal |
| Décision tracée | ADR 0008 |
| ADR corrigé | ADR 0004, section « Conséquences » |

Ce qui **reste non vérifié** :

- Les deux tests **ne prouvent pas l'absence de course**. Ils en attrapent
  l'occurrence : ils échouent vite quand le verrou saute, ils ne passent jamais
  à tort. C'est une propriété plus faible que ce que leur nom laisse croire, et
  elle est assumée comme telle.
- `GameMapper.ToResponse` relit `game.Status` et `game.Winner` **après** la fin
  du verrou. La transition est atomique, le corps de réponse peut refléter un
  état légèrement postérieur au tir qu'il décrit.
- Le verrou ne couvre qu'un processus. Il deviendra insuffisant à la bascule
  vers SQLite (backlog item 5), qui demandera une transaction.

**Ce que cette revue enseigne pour la suite du projet**

L'ADR 0004 n'était pas faux, il était **imprécis au mauvais endroit**. « Le
Singleton impose une implémentation thread-safe » est vrai ; « c'est la raison du
`ConcurrentDictionary` » laisse croire que le choix du type de collection épuise
la question. Une phrase d'ADR qui répond à une question voisine de celle qu'elle
pose se relit comme une garantie.

Le contrôle qui manquait était trivial à écrire et n'existait pas, parce que la
suite de tests raisonnait en tours successifs — jamais en appels simultanés.

