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
| Statut de partie | `GameStatus` | `InProgress` ou `Finished`. Un troisième statut « flottes non encore placées » n'apparaîtra qu'avec le placement manuel (backlog item 3) : tant qu'il n'existe pas dans le code, il n'existe pas ici. |
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
| Damier | `ScanLattice` | Le sous-ensemble de cases balayées par `HuntTargetParity` pendant la **chasse** — une case sur deux. Il ne s'applique jamais à la traque, dont les cibles sont toutes de la parité opposée à la case touchée. |

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
| Type de navire | `ShipKind` | `Carrier` (5), `Battleship` (4), `Cruiser` (3), `Submarine` (3), `Destroyer` (2). |
| Orientation | `Orientation` | `Horizontal` ou `Vertical`. Aucune diagonale. |
| Placement | `Placement` | La pose d'une flotte sur une grille. `Random` (serveur) ou `Manual` (joueur). |

## Tirs

| Français | Identifiant | Définition |
|---|---|---|
| Tir | `Shot` | Une tentative **acceptée** : elle est enregistrée au journal et consomme le tour. |
| Résultat de tir | `ShotResult` | `Miss`, `Hit` ou `Sunk`. |
| Tentative refusée | — | Une tentative rejetée par les règles (case déjà visée, partie terminée, pas son tour). Elle **ne devient pas un `Shot`** : rien n'est enregistré et le tour ne passe pas. |
| Journal | `Shots` | La suite ordonnée des tirs d'une partie, en ajout seul. Source de l'historique, du rejeu et des statistiques. |

## Visibilité

| Français | Identifiant | Définition |
|---|---|---|
| Vue de partie | `GameView` | Ce qu'un joueur donné — le **viewer** — a le droit de voir, et rien de plus : sa propre flotte, les tirs reçus, et le résultat de ses propres tirs sur la grille adverse. Le viewer n'est pas toujours le joueur courant : voir `ViewForClient`. |
| Vue servie au client | `ViewForClient` | La `GameView` que le serveur accepte d'envoyer au navigateur. Elle suit le joueur courant **sauf quand celui-ci est un bot** : un bot n'a pas de client, et lui servir sa vue reviendrait à publier sa flotte. |
| Passation | `Handover` | En mode `Local`, l'écran qui masque tout pendant le changement de joueur. C'est une **protection d'affichage** ; la protection réelle est que le serveur ne transmet jamais autre chose qu'une `GameView`. |

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
