# Vocabulaire du domaine

Glossaire du projet. Le code est en **anglais**, les livrables en **français** : ce
fichier fixe la correspondance entre les deux. Un seul mot par concept, des deux
côtés.

Ce fichier ne contient **aucun détail d'implémentation**. Il ne dit pas comment les
choses sont représentées ni stockées — uniquement ce que les mots désignent.

## Partie et joueurs

| Français | Identifiant | Définition |
|---|---|---|
| Partie | `Game` | Un affrontement entre exactement deux joueurs, de la création au vainqueur. Identifiée par un `GameId`. |
| Statut de partie | `GameStatus` | `AwaitingFleet`, `InProgress` ou `Finished`. `AwaitingFleet` signifie qu'au moins un humain n'a pas encore posé sa flotte : aucun tir n'y est accepté. Il se déduit des grilles, pas d'un drapeau — une grille humaine vide **est** l'attente. |
| Mode de jeu | `GameMode` | `Solo` (un humain contre un bot) ou `Local` (deux humains sur le même navigateur, en alternance). |
| Joueur | `Player` | L'un des deux participants. Un joueur est humain ou bot ; les règles ne font aucune différence entre les deux. |
| Bot | `Bot` | Joueur piloté par le serveur. **Pas** « IA », « ordinateur » ni « adversaire » — ces mots sont ambigus dans un projet où l'IA désigne aussi l'outil de développement. |
| Difficulté | `BotDifficulty` | `Random`, `HuntTarget` ou `HuntTargetParity`. Fixée à la création de la partie, elle ne change plus. Une partie `Local` en porte une par construction, sans objet : elle n'oppose aucun bot. |
| Stratégie | `BotStrategy` | Le comportement qui choisit la case visée par un bot. Une difficulté nomme une stratégie ; la fabrique `IBotStrategyFactory` est le seul endroit où le nom rencontre le code. |
| Joueur courant | `CurrentPlayer` | Le joueur à qui c'est le tour. Seul lui peut tirer. |
| Tour | `Turn` | Le droit de tirer une fois. Il passe après chaque tir accepté, touché ou non. |
| Vainqueur | `Winner` | Le joueur dont l'adversaire a perdu toute sa flotte. N'existe que si le statut est `Finished`. |

### Comportement des bots

Ces trois mots ne décrivent pas trois difficultés mais deux phases et un filtre.
`HuntTarget` et `HuntTargetParity` partagent la traque et ne diffèrent que par le
balayage.

| Français | Identifiant | Définition |
|---|---|---|
| Chasse | `Hunt` | La phase où aucun navire touché n'est en cours de traque : le bot choisit une case parmi celles qu'il n'a pas encore visées. |
| Traque | `Track` | La phase où au moins une case touchée appartient à un navire encore à flot : le bot ne vise plus que le voisinage de ces cases. |
| Case en attente | `Pending` | Une case touchée dont le navire n'est pas connu comme coulé. C'est ce qui déclenche la traque ; quand il n'en reste aucune, le bot retourne chasser. |
| Damier | `ScanStride` | Le pas du balayage de `HuntTargetParity` pendant la **chasse**. Il vaut la longueur du **plus petit navire** de la flotte : un navire de longueur L croise forcément une maille de pas L. Deux pour la flotte classique, un dès qu'un navire n'occupe qu'une case. Il ne s'applique jamais à la traque. |

## Plateau

| Français | Identifiant | Définition |
|---|---|---|
| Grille | `Board` | La zone d'un joueur : sa flotte, et les tirs reçus. Chaque partie en compte deux, une par joueur. |
| Case | `Cell` | Une position unique d'une grille. |
| Coordonnées | `Coordinates` | L'adresse d'une case. Stockée en base 0 ; affichée `A1` à `J10`. |
| Taille de grille | `BoardSize` | Dimensions de la grille. 10 × 10 par défaut, paramétrable. |

## Flotte

| Français | Identifiant | Définition |
|---|---|---|
| Flotte | `Fleet` | L'ensemble des navires d'un joueur. |
| Navire | `Ship` | Une pièce occupant plusieurs cases contiguës en ligne droite. |
| Type de navire | `ShipKind` | `Carrier` (5), `Battleship` (4), `Cruiser` (3), `Submarine` (3), `Destroyer` (2), `PatrolBoat` (1). La longueur est attachée au type : on choisit combien de navires de quels types, pas des longueurs libres. |
| Composition | `Fleet` | Les types de navires en jeu, répétitions comprises. Identique pour les deux joueurs, fixée à la création. Publique : elle est annoncée, et elle ne dit rien des positions. |
| Orientation | `Orientation` | `Horizontal` ou `Vertical`. Aucune diagonale. |
| Placement | `FleetPlacement` | Qui pose la flotte de l'humain : `Random` (le serveur) ou `Manual` (le joueur, navire par navire). Choisi à la création, il ne change plus. |
| Flotte à poser | `FleetToPlace` | La composition que le serveur réclame au joueur : un type et une longueur par navire, **aucune position**. C'est le serveur qui dicte la flotte ; le front n'a aucune règle de jeu à connaître. |
| Grille en attente | `BoardAwaitingFleet` | La grille du prochain humain à servir. Un bot n'y figure jamais : le serveur pose sa flotte lui-même, donc le client ne peut pas la poser à sa place. |

## Tirs

| Français | Identifiant | Définition |
|---|---|---|
| Tir | `Shot` | Une tentative **acceptée** : elle est enregistrée au journal et consomme le tour. |
| Résultat de tir | `ShotResult` | `Miss`, `Hit` ou `Sunk`. |
| Tentative refusée | — | Une tentative rejetée par les règles (case déjà visée, partie terminée, pas son tour). Elle **ne devient pas un `Shot`** : rien n'est enregistré et le tour ne passe pas. |
| Journal | `Shots` | La suite ordonnée des tirs d'une partie, en ajout seul. Source de l'historique, du rejeu et des statistiques — et, depuis l'item 5, **la seule chose persistée** avec les placements. |
| Rejeu | `Restore` | La reconstruction d'une partie à partir des placements et du journal. Tout le reste — impacts, navires coulés, tour courant, statut, vainqueur — est recalculé, jamais stocké. |
| Validation | `Save` | Le moment où le dépôt est informé qu'une partie a changé. Sans objet en mémoire, indispensable dès que le stockage est ailleurs que dans la référence. |
| Historique | `IGameHistory` | La lecture seule des parties passées : des colonnes, jamais un rejeu. Distincte du dépôt, qui lui reconstruit. |

## Visibilité

| Français | Identifiant | Définition |
|---|---|---|
| Vue de partie | `GameView` | Ce qu'un joueur donné — le **viewer** — a le droit de voir, et rien de plus : sa propre flotte, les tirs reçus, et le résultat de ses propres tirs sur la grille adverse. Le viewer n'est pas toujours le joueur courant : voir `ViewForClient`. |
| Vue servie au client | `ViewForClient` | La `GameView` que le serveur accepte d'envoyer au navigateur. Elle suit le joueur courant **sauf quand celui-ci est un bot** : un bot n'a pas de client, et lui servir sa vue reviendrait à publier sa flotte. |
| Passation | `Handover` | En mode `Local`, l'écran qui masque tout pendant le changement de joueur. C'est une **protection d'affichage** ; la protection réelle est que le serveur ne transmet jamais autre chose qu'une `GameView`. Le nom du joueur attendu est porté par `HandoverTo` ; tant qu'il n'est pas nul, l'interface n'affiche rien de la vue. |
| Tireur | `Shooter` | Le joueur qui vient de tirer. En hot-seat il n'est plus le viewer au moment où le message s'affiche : le message doit le **nommer**, pas dire « vous ». |

## Mots écartés

Ces termes ne doivent apparaître ni dans le code, ni dans les livrables.

| À ne pas employer | Employer | Pourquoi |
|---|---|---|
| `Grid` | `Board` | Un seul mot pour la grille d'un joueur. |
| `Square`, `Tile` | `Cell` | Un seul mot pour la case. |
| `Destroyed`, `Killed` | `Sunk` | Un seul mot pour un navire coulé. |
| `Attack`, `Strike` | `Shot` | Un seul mot pour le tir. |
| `Move` | `Shot` | Un tir accepté est le seul coup qui existe ; une tentative refusée n'en est pas un. |
| `Computer`, `AI` | `Bot` | « IA » désigne l'outil de développement dans ce projet ; le confondre avec l'adversaire rend les livrables illisibles. |
| `Player1`, `Player2` | `CurrentPlayer` / `Opponent` selon le point de vue | Les numéros de joueur ne sont pas une notion du domaine. |
| `BotLevel`, `Level`, `Niveau` | `BotDifficulty`, « difficulté » | Un seul mot pour le réglage de l'adversaire. `AGENTS.md` § 10 intitule le backlog « niveaux de bot » : c'est le nom de l'étape, pas celui de la notion. |
| `Easy`, `Medium`, `Hard` | `Random`, `HuntTarget`, `HuntTargetParity` | Les difficultés sont nommées par l'algorithme qu'elles désignent, pas par une échelle subjective. Les étiquettes « Novice », « Chasseur », « Vétéran » n'existent qu'à l'écran. |
