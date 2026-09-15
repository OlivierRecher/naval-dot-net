# ADR 0013 : la flotte devient une donnée de la partie, et le damier suit le plus petit navire

## Statut et date
Accepté — 2026-09-15. Rédigé avec l'item 6 du backlog (personnalisation).

## Contexte
Deux ADR précédents ont laissé une dette explicite à cet item.

L'ADR 0009, sur les difficultés de bot :

> **Le damier suppose un navire de deux cases minimum.** Le jour où la flotte
> devient personnalisable, un navire d'une seule case rendrait ce niveau
> **incorrect**, pas seulement moins bon.

L'ADR 0010, sur le placement manuel :

> Le gabarit contrôlé est `FleetTemplate.Standard`, **en dur** dans
> `Game.PlaceFleetFromClient`. La partie connaît la taille de sa grille mais pas
> sa flotte ; l'item 6 devra la lui donner.

Ces deux dettes sont la même : la composition n'appartenait à personne. La taille
de grille, elle, était paramétrable depuis le socle.

## Décision 1 — la composition appartient à la partie

`Game` porte sa `Fleet`, comme il porte déjà son mode, sa difficulté et la taille
de ses grilles. Elle est fixée à la création, ne change plus, et sert :

- à placer les flottes aléatoirement ;
- à **contrôler** un placement manuel — c'est la dette de l'ADR 0010 ;
- à peupler `FleetToPlace` dans la vue ;
- à informer le bot — voir décision 3.

Elle est persistée avec la partie, en noms séparés par des virgules. Ce n'est
pas une donnée relationnelle : c'est un gabarit, relu d'un bloc, jamais
interrogé ligne à ligne. Le normaliser aurait fait une table de plus pour rien.

## Décision 2 — ce qu'une composition doit respecter

`FleetTemplateRules.Validate` est une **fonction pure** : elle ne place rien,
elle écarte ce qui ne peut pas tenir.

| Refus | Exact ou heuristique |
|---|---|
| Flotte vide | Exact |
| Plus de 15 navires | Choix de périmètre |
| Un navire plus long que le plus grand côté | Exact |
| Plus d'un tiers de la grille occupé | **Heuristique, mesurée** |

Le plafond de densité mérite d'être justifié, car il refuse des flottes qui
*pourraient* tenir. Le placement aléatoire procède par essais : au-delà d'une
certaine densité il échoue — et il échoue **aléatoirement**, donc le refus
dépendrait de la graine. Un joueur verrait la même composition acceptée puis
refusée.

Le plafond rend le refus déterministe. Sa valeur n'est pas choisie : un test
construit la flotte la plus dense encore acceptée sur la plus petite grille
autorisée, et vérifie que le placement réussit sur **200 graines**. S'il échouait
une seule fois, le plafond serait trop haut.

Conséquence assumée : une flotte au-dessus du plafond est refusée même si elle
aurait pu tenir. C'est un refus prévisible préféré à une acceptation aléatoire.

## Décision 3 — le pas du damier se déduit de la flotte

C'est la dette de l'ADR 0009, et elle se solde par une généralisation plutôt que
par un correctif.

Le raisonnement d'origine était : « un navire de deux cases croise forcément une
case sur deux ». Le raisonnement général est : **un navire de longueur L croise
forcément une maille de pas L, jamais moins.** Le pas suit donc le plus petit
navire de la flotte en jeu.

- Flotte classique, plus petit navire de 2 cases → pas 2, le damier d'origine.
- Une vedette d'une case → **pas 1**, c'est-à-dire la grille entière. Le niveau
  `HuntTargetParity` n'est alors plus qu'un `HuntTarget` : il ne peut pas faire
  mieux, et surtout il ne manque rien.
- Sans information de flotte → pas 1. Balayer trop coûte des tirs ; balayer trop
  peu manque un navire.

Le bot lit la composition dans la `GameView`, qui la publie. C'est une
information **publique par construction** : elle est annoncée à la création et
les deux joueurs partagent la même. Elle ne dit rien des positions, donc elle ne
touche pas l'invariant de l'ADR 0003.

## Décision 4 — le schéma se met à niveau, il ne se recrée pas

Ajoutée par la revue de la PR #8. La colonne `Fleet` est la **première évolution
de schéma du projet**, et elle falsifie une affirmation de l'ADR 0012 : « le
périmètre ne comporte aucune évolution de schéma à rejouer ». `EnsureCreated`
crée un schéma absent, il ne modifie jamais un schéma existant — une base créée
avant cet item échouait sur « table Games has no column named Fleet ».

`SchemaUpgrade.Apply` crée le schéma s'il manque **puis** ajoute les colonnes
apparues depuis, en interrogeant `pragma_table_info`. C'est idempotent, et c'est
volontairement minimal : une liste de colonnes et un `ALTER TABLE`.

Ce n'est pas une solution durable, et l'ADR le dit plutôt que de le taire : **au
prochain changement de schéma qui ne soit pas un simple ajout de colonne — un
renommage, une contrainte, une table scindée — il faudra passer aux migrations
EF.** Le coût évité ici est celui d'un outillage supplémentaire pour une seule
colonne ; il ne se représentera pas.

Une partie écrite avant la colonne se relit avec la flotte classique : `Fleet`
vide signifie « flotte par défaut », ce qui est exactement ce qu'elle était.

## Conséquences
- `ShipKind` gagne `PatrolBoat`, une case. Il n'a d'autre raison d'être que de
  rendre le cas d'une case **atteignable** : sans lui, la limite de l'ADR 0009
  serait restée théorique et la généralisation invérifiable.
- `BattleShip.Models` gagne `ShipCatalog`, avec les libellés français et les
  longueurs. Un test interdit qu'il diverge de l'énumération **et des longueurs**
  du domaine — le catalogue de l'ADR 0009 ne vérifiait que les noms.
- Le navigateur applique les mêmes bornes que le serveur pour dire tout de suite
  ce qui serait refusé. C'est un **confort** ; la garantie reste le refus serveur,
  et un test vérifie que celui-ci passe bien par la validation et non par l'échec
  du placeur.
- **Limite assumée** : les longueurs restent attachées aux types. On choisit
  *combien* de navires de *quels types*, pas des longueurs arbitraires. Inventer
  un type par longueur aurait fait un enum sans fin.
- **Limite assumée** : la mise à niveau de schéma ne sait qu'**ajouter des
  colonnes**. Toute autre évolution demandera des migrations.
- **Limite assumée** : le plafond d'un tiers est vérifié sur la plus petite
  grille (8 × 8). Il est plus conservateur sur les grandes, où le placement
  aléatoire réussit à densité supérieure.

## Vérification et réexamen

`dotnet build` puis `dotnet test` : **246 tests, 0 échec** (221 avant l'item).

| Contrôle | Résultat |
|---|---|
| Damier avec la flotte classique | Une case sur deux, 100 graines |
| Damier avec un navire d'une case | Couvre toute la grille — la limite de l'ADR 0009 est soldée |
| Damier avec un plus petit navire de 3, 4, 5 | Pas 3, 4, 5 |
| Damier pendant la traque | Ignoré, quelle que soit la flotte |
| Plafond de densité | Flotte la plus dense acceptée, placée sur 200 graines sans un échec |
| Placement manuel | Contrôle la composition **de la partie**, pas la flotte classique |
| Flotte personnalisée après redémarrage | Retrouvée à l'identique |
| Navigateur | Flotte réduite à un croiseur, un torpilleur et une vedette, partie lancée et jouée |

Six mutations, toutes détectées. Deux ne l'étaient d'abord **que** par les tests
de domaine : une flotte impossible finit aussi en 400 quand le placement
aléatoire épuise ses essais, si bien que le statut seul ne distinguait pas le
refus déterministe du refus par hasard. Le test d'API vérifie désormais la
**forme** de la réponse — un `ValidationProblem` et non un `Problem` nu.

Trois mutations de plus après la revue de la PR #8, toutes détectées : mise à
niveau de schéma retirée, pas du damier forcé à 3, plafond de densité relevé.

À réexaminer si les longueurs devaient devenir libres, ou si le placement
aléatoire était remplacé par un algorithme déterministe : le plafond de densité
n'aurait alors plus de raison d'être.

## Références
- ADR 0003 (serveur autoritaire), ADR 0009 (difficultés), ADR 0010 (placement manuel), ADR 0012 (persistance)
- `AGENTS.md` § 3, § 10 item 6 · `CONTEXT.md`
- `REVUE-IA.md`, revue 9
