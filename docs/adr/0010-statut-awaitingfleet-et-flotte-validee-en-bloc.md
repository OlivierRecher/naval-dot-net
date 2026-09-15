# ADR 0010 : `AwaitingFleet` se déduit des grilles, et la flotte est validée en bloc

## Statut et date
Accepté — 2026-09-15. Rédigé avec l'item 3 du backlog (placement manuel).

## Contexte
Jusqu'ici, `POST /games` posait la flotte des deux joueurs au hasard et rendait
une partie immédiatement jouable. Le placement manuel introduit une phase qui
n'existait pas : **la partie existe, mais on ne peut pas y tirer**.

`CONTEXT.md` l'avait anticipée depuis le cadrage — « un troisième statut
"flottes non encore placées" n'apparaîtra qu'avec le placement manuel » — sans
décider ni comment il est porté, ni comment la flotte est soumise.

Deux questions distinctes, tranchées ici.

## Question 1 — comment la partie sait-elle qu'elle attend une flotte ?

**(a) Un drapeau porté par `Game`,** posé à la construction et abaissé au
placement. Écarté : le drapeau et les grilles peuvent se contredire. Une partie
« prête » dont la grille est vide est un état que rien n'empêche, et que la
première requête de tir découvrirait par une exception.

**(b) Le statut se déduit des grilles.** Une grille humaine sans navire **est**
l'attente ; il n'y a rien d'autre à tenir à jour.

**Décision : (b).**

```csharp
Status = first.Board.Ships.Count is 0 || second.Board.Ships.Count is 0
    ? GameStatus.AwaitingFleet
    : GameStatus.InProgress;
```

Conséquence non recherchée : le mode `Local` de l'item 4, où deux humains posent
chacun leur flotte, est **partiellement** servi. Chaque `PUT` remplit la
prochaine grille vide et la partie démarre quand il n'en reste plus.

> **Correction apportée par la revue de la PR #5.** Cet ADR affirmait d'abord que
> le mode `Local` était servi « sans aucune ligne à ajouter ». C'était faux :
> `ViewForClient()` servait toujours le joueur courant, donc après le placement
> du premier joueur le second recevait un écran de placement **sans rien à
> poser** et sans moyen d'avancer. Le défaut était inatteignable — l'API ne crée
> que des parties `Solo` — mais l'affirmation, elle, était publiée. Corrigé :
> pendant `AwaitingFleet`, la vue servie décrit le joueur **dont on attend la
> flotte**, pas le joueur courant.

Ce qui reste à l'item 4 : que l'API sache créer une partie `Local`, et l'écran de
passation entre les deux joueurs.

### Le client ne pose jamais la flotte d'un bot
`BoardAwaitingFleet()` écarte explicitement les joueurs `IsBot`. Le serveur
remplit toujours la grille du bot lui-même, donc ce garde est **inatteignable
par l'API** — et c'est précisément ce qui le rend dangereux : il pouvait être
supprimé sans qu'aucun test ne bronche. Voir « Vérification ».

## Question 2 — comment la flotte est-elle soumise ?

**(a) Un navire par requête.** Cinq allers-retours, et surtout cinq états
intermédiaires persistés : une flotte abandonnée en cours de route laisse la
grille à moitié remplie, dans un état qu'aucune règle du jeu ne décrit.

**(b) La flotte entière en une requête, validée en bloc avant qu'aucune case ne
soit posée.**

**Décision : (b).** `PUT /games/{id}/fleet` reçoit les cinq navires.
`FleetPlacementRules.Validate` est une **fonction pure** — conforme à
`AGENTS.md` § 4, « les vérifications de règles sont des fonctions pures,
testables sans instancier une partie » — qui contrôle dans l'ordre :

1. la **composition** : exactement un navire de chaque type du gabarit ;
2. chaque **placement**, cumulativement, contre ceux déjà acceptés — débordement
   puis chevauchement.

Le point 2 se fait sur une liste qui grossit, pas sur la grille : rien n'est
écrit tant que la flotte entière n'est pas validée.

`PUT` et non `POST` parce que l'opération est idempotente dans son intention —
elle établit la flotte d'une partie — et qu'une seconde tentative est refusée
par `409`, pas dupliquée.

## Conséquences
- `GameStatus` gagne `AwaitingFleet`, placé **en premier** : c'est l'état le plus
  précoce, et le statut circule en texte, donc aucune valeur numérique n'est
  exposée.
- `FireRules.Validate` refuse `AwaitingFleet` par un `FireRejection.FleetNotPlaced`
  → `409` en HTTP, `FailedPrecondition` en gRPC-Web. Les deux transports passent
  par la même règle, ils ne peuvent pas diverger.
- La `GameView` publie `FleetToPlace` : le type et la longueur de chaque navire
  restant, **sans aucune position**. Le front n'embarque donc aucune règle de
  jeu, et la flotte personnalisable (item 6) ne demandera aucun changement côté
  client.
- `EnumNames<T>` généralise le contrat « la valeur voyage sous forme de nom »
  posé par l'ADR 0009. Il sert désormais quatre énumérations : `BotDifficulty`,
  `FleetPlacement`, `ShipKind`, `Orientation`. `BotDifficulties` disparaît.
- **Limite assumée** : le gabarit contrôlé est `FleetTemplate.Standard`, en dur
  dans `Game.PlaceFleetFromClient`. La partie connaît la taille de sa grille mais
  pas sa flotte ; l'item 6 devra la lui donner.
- **Limite assumée** : le navigateur refuse localement débordement et
  chevauchement pour éviter un aller-retour par navire. C'est un **confort
  d'interface** : la garantie est le contrôle serveur, et aucun test automatisé
  ne couvre le contrôle local.
- La soumission de flotte est sérialisée par le verrou de l'agrégat (ADR 0008),
  et un test l'atteste désormais. Sans verrou, la mesure montre de 2 à 64
  soumissions acceptées et jusqu'à **18 navires** sur une grille qui n'en admet
  que cinq — deux soumissions entrelacées posent chacune la leur.

## Vérification et réexamen

`dotnet build` puis `dotnet test` : **172 tests, 0 échec** (152 après le domaine,
134 avant l'item 3).

Neuf mutations exécutées puis rétablies. Le premier passage en a laissé
**trois survivantes**, qui ont changé le code :

| Mutation | Premier passage | Traitement |
|---|---|---|
| Composition jamais vérifiée | 3 / 8 au rouge | — |
| Chevauchement ignoré | 1 / 8 | — |
| Statut forcé à `InProgress` | 4 / 11 | — |
| Flotte posée sans validation | 1 / 11 | — |
| `FireFromClient` ne refuse plus la flotte manquante | **survivante** | Garde **supprimé** : `FireRules.Validate` refusait déjà. C'était du code mort. |
| `PlaceFleetFromClient` ne vérifie plus le statut | **survivante** | Condition **simplifiée** : l'absence de grille en attente suffit. |
| La grille d'un bot peut recevoir la flotte du client | **survivante** | **Test ajouté.** Aucun test ne distinguait le garde de son absence. |
| Type de navire non validé | 2 / 19 | — |
| Placement de flotte non validé à la création | 3 / 19 | — |

La troisième survivante est la plus instructive :
`PlaceFleetFromClient_NeverTouchesTheBotBoard` **passait dans les deux cas**,
parce que la grille du bot n'est jamais vide. Le test affirmait protéger un
invariant de l'ADR 0003 et ne protégeait rien. Le test ajouté construit l'état
dégénéré — bot sans flotte, humain avec — qui est le seul à discriminer.

Deux mutations supplémentaires, ajoutées après la revue de la PR #5 :

| Mutation | Test au rouge |
|---|---|
| `ViewForClient` ignore le joueur en attente | `ViewForClient_WhenTheFirstOfTwoHumansHasPlaced_DescribesTheSecond` |
| `PlaceFleetFromClient` sans verrou | `PlaceFleetFromClient_CalledConcurrently_AcceptsExactlyOneFleet` |

La seconde n'a mordu qu'après correction **du test** : dans sa première écriture,
il passait sans verrou. Voir `REVUE-IA.md`, revue 6.

**Vérification navigateur** : partie créée en placement manuel, cinq navires
posés, flotte validée, partie jouée. Elle a révélé un défaut qu'aucun des 172
tests ne pouvait voir — voir `REVUE-IA.md`, revue 5.

À réexaminer à l'item 4 (`Local`, deux flottes à poser) et à l'item 6 (gabarit
de flotte configurable, qui doit sortir du code en dur).

## Références
- ADR 0003 (serveur autoritaire), ADR 0006 (validation par filtre), ADR 0009 (nom plutôt qu'énumération)
- `AGENTS.md` § 4, § 5, § 10 item 3 · `CONTEXT.md`
- `REVUE-IA.md`, revues 5 et 6
