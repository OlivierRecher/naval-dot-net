# ADR 0008 : protéger l'agrégat `Game` par un verrou par partie

## Statut et date
Accepté — 2026-09-15. Rédigé après la revue automatisée de la PR #1.

## Contexte
L'ADR 0004 conclut que « le cycle de vie Singleton impose une implémentation
thread-safe » et désigne le `ConcurrentDictionary` comme la réponse.

La revue de la PR #1 a montré que cette phrase mélange deux choses. Le
dictionnaire concurrent protège la **table de stockage** — trouver et ajouter une
partie. Il ne protège pas l'**objet stocké** : `Game` reste un agrégat mutable
partagé, avec un `List<Shot>` et deux champs de tour.

Deux requêtes visant la même partie peuvent donc :

- franchir toutes les deux `FireRules.Validate` avant que l'une n'appelle
  `Board.Receive`, et enregistrer deux tirs pour un seul tour ;
- inverser `_current` et `_waiting` deux fois, ce qui annule l'alternance ;
- énumérer `_shots` dans `ViewFor` pendant qu'un autre fil y ajoute.

Le chemin est réel et non théorique : `/games/{id}/shots` est en HTTP et `Fire`
est en gRPC-Web (ADR 0005). Les deux transports partagent le même Singleton.

## Options envisagées

**(a) Ne rien faire.** Le client Blazor sérialise ses propres appels, donc le
jeu tel qu'il est joué ne déclenche pas la course. Mais l'API est publique et le
serveur est déclaré autoritaire (`AGENTS.md` § 4) : se reposer sur la politesse
du client contredit exactement cette ligne.

**(b) Rendre `Game` immuable.** Chaque tir produit une nouvelle instance
remplacée atomiquement dans le dictionnaire. Cohérent, mais annule l'ADR 0002
(agrégat mutable + journal en ajout seul) et réécrit le moteur entier.

**(c) Un verrou par partie, porté par l'agrégat.** `Game` détient un
`System.Threading.Lock` privé et sérialise ses propres transitions et
projections.

## Décision
Option **(c)**.

Le verrou vit dans `Game`, pas dans les endpoints : c'est le seul endroit que
les deux transports traversent. Un garde placé dans `GameEndpoints` aurait dû
être dupliqué dans `BattleGrpcService`, avec la dérive que cela suppose.

Conséquence de conception : le choix de la cible du bot est entré dans
l'agrégat (`Game.PlayBotTurn(IBotStrategy)`). Tant qu'il vivait dans l'endpoint,
« vérifier que c'est au bot de jouer » et « tirer » étaient deux opérations
séparées, donc deux appels concurrents à `/bot-turn` pouvaient jouer deux tours.

`Shots` renvoie désormais une copie : sans cela, l'appelant énumère la liste
vivante hors du verrou.

## Conséquences
- `Domain` ne gagne aucune dépendance : `System.Threading.Lock` est dans la BCL.
  La règle « zéro dépendance » d'`AGENTS.md` § 4 reste tenue.
- Le verrou est **par partie**, pas global : deux parties différentes ne se
  bloquent pas.
- Les sections critiques ne contiennent ni E/S ni `await`.
- **Limite assumée** : la réponse HTTP est assemblée *après* la fin du verrou
  (`GameMapper.ToResponse` relit `game.Status` et `game.Winner`). Sous
  concurrence réelle, le corps de réponse peut donc refléter un état légèrement
  postérieur au tir qu'il décrit. La transition, elle, reste atomique. Rendre la
  requête entière atomique supposerait de faire remonter le verrou jusqu'au
  mapping, ce qui exposerait un détail d'implémentation hors du domaine.
- Le jour de la bascule vers SQLite (ADR 0004, item 5 du backlog), ce verrou
  devient insuffisant : il ne couvre qu'un processus. Il faudra une transaction.

## Vérification et réexamen
- `FireFromClient_CalledConcurrently_RecordsExactlyOneShotPerAcceptedCall` :
  400 tirs lancés en `Parallel.ForEach`, le journal doit contenir exactement
  autant d'entrées que d'appels acceptés.
- `ViewFor_WhileAnotherThreadFires_NeverObservesAJournalBeingMutated` : un fil
  projette pendant qu'un autre tire.
- Contrôle de mutation exécuté : en remplaçant `lock (_gate)` par `if (true)`,
  le premier test échoue avec « Operations that change non-concurrent
  collections must have exclusive access ».
- Ces deux tests ne peuvent pas *prouver* l'absence de course : ils en attrapent
  l'occurrence. Ils échouent vite quand le verrou saute, ils ne passent jamais à
  tort.

À réexaminer si la persistance sort du processus, ou si l'ADR 0002 était
révisé au profit d'un agrégat immuable.

## Références
- ADR 0002 (agrégat mutable), ADR 0004 (Singleton), ADR 0005 (gRPC-Web)
- `AGENTS.md` § 4
- Revue automatisée de la PR #1
