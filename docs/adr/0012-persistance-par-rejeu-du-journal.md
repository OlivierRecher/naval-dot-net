# ADR 0012 : persister le journal, rejouer le reste

## Statut et date
Accepté — 2026-09-15. Rédigé avec l'item 5 du backlog (persistance, historique, statistiques).

## Contexte
L'ADR 0004 a posé `IGameRepository` **avant** d'en avoir besoin, en annonçant :

> La bascule vers SQLite consistera à écrire une seconde implémentation et à
> changer une ligne d'enregistrement. **Aucun endpoint modifié.**

Cet item est le premier à mettre cette promesse à l'épreuve. Elle tient sur un
point et se rompt sur un autre — voir « Ce que l'ADR 0004 avait mal vu ».

Reste la question de fond : **que persiste-t-on d'une partie ?** `Game` est un
agrégat mutable avec deux grilles, des navires, leurs impacts, deux champs de
tour, un statut, un vainqueur et un verrou.

## Décision 1 — on ne persiste que ce qui n'est pas dérivable

L'ADR 0002 annonçait, à propos du journal en ajout seul : « il fournit rejeu,
historique et statistiques presque gratuitement ». Cet item encaisse la
promesse : **la persistance est le rejeu**.

Ce qui est écrit :

| Table | Contenu |
|---|---|
| `Games` | mode, difficulté, taille de grille, dates, vainqueur |
| `Players` | nom, humain ou bot, **siège** (0 ouvre la partie) |
| `Ships` | type, origine, orientation — les **placements**, rien d'autre |
| `Shots` | rang, tireur, case, résultat |

Ce qui n'est **pas** écrit, parce que le rejeu le recalcule : les cases touchées,
les navires coulés, les tirs reçus, le joueur courant, le statut de la partie.
`Game.Restore` repose les flottes puis rejoue le journal dans l'ordre.

Trois raisons.

1. **Un état dérivé stocké peut diverger de ce dont il dérive.** En ne le
   stockant pas, la question ne se pose plus.
2. Le schéma est minimal : quatre tables, aucun champ calculé à maintenir.
3. Si le rejeu ne reproduisait pas exactement l'état, ce serait que le domaine
   n'est pas déterministe — un défaut qu'on préfère découvrir.

**Exception assumée** : le **résultat** de chaque tir est stocké alors qu'il est
dérivable. Le rejeu ne le lit jamais ; il n'existe que pour rendre les
statistiques interrogeables en SQL sans rejouer des centaines de journaux. Un
test interdit qu'il diverge de ce que le rejeu recalcule.

## Décision 1 bis — la persistance lit l'agrégat sous son verrou

Ajoutée par la revue de la PR #7. Écrire une partie demande de lire son statut,
ses deux joueurs, leurs grilles et son journal. Les lire **une par une** revient
à les lire à des instants différents : l'échange de tour est une affectation de
tuple, donc non atomique, et `Board.Ships` est une liste vivante. Une écriture
concurrente d'un tir pouvait donc mélanger deux instants, ou lever une exception
d'énumération.

`Game.Snapshot()` rend une photographie cohérente, prise sous le verrou, et la
persistance ne travaille plus que là-dessus. Les sièges qu'elle publie sont
l'ordre d'**ouverture**, jamais l'ordre du tour courant : c'est cet ordre-là que
le rejeu attend.

## Décision 2 — SQLite détient le journal, la mémoire détient la partie

Reconstruire la partie à chaque lecture rendrait **deux instances** à deux
requêtes concurrentes, et le verrou par partie de l'ADR 0008 ne sérialiserait
plus rien : chacune jouerait sur la sienne.

Un cache Singleton conserve donc les parties vivantes. C'est
`GameCache.Remember` — un `GetOrAdd` — qui garantit l'unicité : il publie une
instance, ou rend celle qui a gagné la course. Le raccourci de `Find` n'est
qu'une optimisation : retiré, le comportement est identique, seulement plus
coûteux. Cette distinction n'était **pas** celle que la première rédaction de cet
ADR annonçait ; c'est une mutation qui l'a établie.

## Décision 3 — le dépôt reste synchrone

EF Core offre `SaveChanges` autant que `SaveChangesAsync`. Rendre
`IGameRepository` asynchrone aurait obligé chaque endpoint à devenir `async`,
ce qui contredisait frontalement la promesse de l'ADR 0004.

Le choix est donc **délibéré et coûteux** : les entrées/sorties SQLite bloquent
un fil du pool. À l'échelle du périmètre — un processus, une base locale — c'est
sans conséquence mesurable. À une échelle réelle, ce serait le premier point à
reprendre, et cela **changerait les endpoints**.

## Ce que l'ADR 0004 avait mal vu

L'interface `Add` / `Find` était la bonne **forme**. Il lui manquait un
**point de validation**.

Un dépôt en mémoire détient la référence de l'agrégat : quand un endpoint appelle
`game.Fire(...)`, le dépôt « voit » le changement sans qu'on lui dise rien. Un
dépôt persistant, non. Il faut lui signaler que la partie a changé.

`IGameRepository` gagne donc `Save(Game)`, et **quatre appelants changent** :
`/shots`, `/bot-turn`, `/fleet` et le service gRPC. L'implémentation en mémoire
en fait une méthode vide — et c'est précisément cette vacuité qui avait masqué le
besoin pendant quatre items.

Voir `REVUE-IA.md`, revue 8.

## Conséquences
- `Player` accepte un identifiant à la relecture : le journal désigne ses tireurs
  par cet identifiant, il doit survivre au redémarrage.
- `IGameHistory` est **séparée** de `IGameRepository` : l'historique interroge des
  colonnes, il ne reconstruit aucune partie. Les mêler ferait rejouer des
  centaines de journaux pour afficher un tableau.
- Les statistiques ne stockent **aucun taux**. La précision se déduit des
  compteurs ; un total et sa moyenne ne peuvent pas diverger s'il n'y en a qu'un
  des deux.
- Pas de migrations : le schéma est créé au démarrage s'il manque
  (`EnsureCreated`). Le périmètre ne comporte aucune évolution de schéma à
  rejouer, et une migration vide serait un rituel sans objet.

  > **Falsifié par l'item suivant.** L'item 6 a ajouté une colonne `Fleet`, et
  > `EnsureCreated` ne modifie jamais un schéma existant : une base créée avant
  > cet item échouait sur « table Games has no column named Fleet ». « Aucune
  > évolution de schéma à rejouer » était vrai au moment où c'était écrit et
  > faux un item plus tard. Voir ADR 0013 et `REVUE-IA.md` revue 10.
- Une partie **terminée quitte le cache**. Elle ne change plus, son verrou ne
  protège plus rien, et la garder ferait croître la mémoire sans borne sur un
  serveur qui vit longtemps. SQLite suffit à la relire. Ajouté par la revue de la
  PR #7.
- Le statut `AwaitingFleet` de l'historique se **déduit des flottes** — un humain
  sans navire — et non d'une colonne de plus. La première version publiait
  `InProgress` pour toute partie non terminée, donc un statut faux tant que les
  flottes n'étaient pas posées. Ajouté par la revue de la PR #7.
- **Limite assumée** : le cache et le verrou ne couvrent **qu'un processus**. Deux
  instances de l'API sur la même base joueraient chacune sur sa propre copie.
  L'ADR 0008 l'annonçait ; cet item ne le corrige pas, il le confirme.
- **Limite assumée** : `Save` réécrit la ligne de partie et ajoute les tirs
  manquants, sans transaction explicite ni verrou optimiste. Un second processus
  écrivant la même partie produirait un conflit de clé, pas une fusion.
- **Piège vérifié par exécution** : SQLite refuse `ORDER BY` sur un
  `DateTimeOffset`. Les dates sont stockées en **ticks UTC** par un
  `ValueConverter` ; l'ordre chronologique devient l'ordre numérique, et la
  résolution de 100 ns rend deux parties ex æquo impossibles en pratique.

## Vérification et réexamen

`dotnet build` puis `dotnet test` : **217 tests, 0 échec** (195 avant l'item).

Les tests d'API tournent désormais sur un **vrai SQLite en mémoire**, un par
classe : le schéma, les contraintes et le SQL des statistiques sont réellement
exercés.

| Contrôle | Résultat |
|---|---|
| Une partie en cours survit à un redémarrage | Dépôt et cache neufs sur la même base : mêmes tirs, même vue |
| Une partie mutée **sans** `Save` perd ses tirs | C'est ce test qui donne son sens au point de validation |
| Validation après **chaque** tir | Les rangs vont de 0 à n−1, sans trou ni doublon |
| Résultats stockés contre résultats rejoués | Identiques sur 25 tours |
| Lecture concurrente sur cache froid, 64 fils | Une seule instance publiée |
| Vérification navigateur | Partie jouée, **API redémarrée**, partie retrouvée intacte ; page d'historique et statistiques |

Sept mutations, dont **trois survivantes au premier passage** — chacune a révélé
un test qui ne protégeait rien, jamais un défaut du code :

| Mutation | 1ᵉʳ passage | Traitement |
|---|---|---|
| L'endpoint de tir ne valide plus | 1 / 82 | — |
| `Restore` ne rejoue pas le journal | 3 / 5 + 3 / 7 | — |
| Les tirs sont réécrits depuis zéro | **survivante** | Test ajouté : valider après chaque tir |
| La flotte posée plus tard n'est pas écrite | 1 / 7 | — |
| Le raccourci du cache est retiré | **survivante** | **Mutation équivalente** : l'ADR disait faux, corrigé |
| `Remember` écrase au lieu de publier | **survivante** | Test ajouté : lecture concurrente sur cache froid |
| Les statistiques oublient les navires coulés | **survivante** | Test tautologique remplacé par un témoin indépendant |
| L'historique ne trie plus | 1 / 7 | — |

Quatre mutations de plus après la revue de la PR #7, toutes détectées :
`Snapshot` sans verrou, sièges suivant le tour courant, partie terminée gardée en
cache, historique ignorant l'attente de flotte. Les deux premières ont d'abord
survécu — l'une parce que le test ne mettait pas assez de pression, l'autre parce
qu'aucun test ne distinguait l'ordre d'ouverture de l'ordre du tour.

À réexaminer si l'API devait tourner en plusieurs instances : le cache, le verrou
et l'absence de verrou optimiste tomberaient ensemble.

## Références
- ADR 0002 (journal en ajout seul), ADR 0004 (`IGameRepository`), ADR 0008 (verrou par partie)
- `AGENTS.md` § 4, § 10 item 5 · `CONTEXT.md`
- `REVUE-IA.md`, revue 8
