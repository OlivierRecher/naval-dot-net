# ADR 0009 : la difficulté est une donnée de la partie, résolue par une fabrique

## Statut et date
Accepté — 2026-09-15. Rédigé avec l'item 2 du backlog (niveaux de bot).

## Contexte
Le socle n'offrait qu'un bot, `RandomBot`, enregistré en **Singleton** sous
`IBotStrategy` et injecté directement dans l'endpoint `/games/{id}/bot-turn` :

```csharp
builder.Services.AddSingleton<IBotStrategy>(_ => new RandomBot(Random.Shared));
```

Ce montage ne survit pas à l'arrivée de trois difficultés. Un Singleton unique
signifie « toutes les parties en cours partagent le même adversaire » : le
conteneur n'a aucun moyen de savoir laquelle des parties il sert.

Il faut donc décider **où vit la difficulté** et **qui résout le nom en code**.

Contrainte héritée : `IBotStrategy.ChooseTarget` ne reçoit qu'une `GameView`,
c'est-à-dire exactement ce qu'un humain voit. Aucune difficulté ne peut être
obtenue en donnant plus d'information au bot — seulement en exploitant mieux
celle qu'il a. C'est ce qui rend les trois niveaux comparables.

## Options envisagées

**(a) Le client transmet la difficulté à chaque tour de bot.** L'endpoint
resterait sans état. Écarté sans hésiter : `AGENTS.md` § 4 pose que le serveur
est autoritaire et que le client n'est jamais cru. Un client pourrait demander
`HuntTargetParity` à la création pour l'affichage, puis `Random` à chaque tour.
La difficulté deviendrait décorative.

**(b) Le dépôt stocke la stratégie à côté de la partie.** `IGameRepository`
rendrait un couple `(Game, IBotStrategy)`. Écarté : cela fait porter au stockage
une notion qui n'en relève pas, et complique la bascule vers SQLite (ADR 0004) —
on ne persiste pas un objet de comportement, on persiste un nom.

**(c) La partie porte sa difficulté ; une fabrique la résout en stratégie.**
`Game` gagne une propriété `BotDifficulty` fixée à la construction. Une
`IBotStrategyFactory` en Singleton traduit ce nom en `IBotStrategy` au moment du
tour de bot.

## Décision
Option **(c)**.

```csharp
builder.Services.AddSingleton<IBotStrategyFactory>(_ => new BotStrategyFactory(Random.Shared));
// ...
var outcome = game.PlayBotTurn(strategies.For(game.BotDifficulty));
```

Trois points la justifient.

1. **La difficulté est un attribut de l'affrontement**, au même titre que
   `GameMode` ou `BoardSize`. Elle est choisie une fois, ne change plus, et
   devra être persistée telle quelle le jour de SQLite.
2. **La fabrique est le seul endroit où le nom rencontre le code.** `Game` ne
   connaît aucune implémentation de `IBotStrategy` ; il en reçoit une. La règle
   « `Domain` ne référence rien » (`AGENTS.md` § 4) reste tenue, la fabrique
   vivant elle aussi dans `Domain`.
3. **Le Singleton reste légitime** parce que les stratégies sont des fonctions
   pures de la `GameView` : aucune ne retient d'état entre deux tours. C'est une
   conséquence directe de la contrainte héritée, et c'est ce qui autorise une
   seule fabrique pour toutes les parties simultanées.

### Le nom voyage en texte, pas en énumération
`CreateGameRequest.BotDifficulty` est un `string`, pas l'énumération.

Une énumération dans le DTO ferait échouer la **désérialisation** sur tout nom
inconnu, avant que le filtre de validation (ADR 0006) ait pu s'exécuter : le
client recevrait une erreur de format au lieu du `ValidationProblem` uniforme que
l'ADR 0006 promet. Avec un `string`, toute **chaîne** JSON se lie et c'est
FluentValidation qui refuse.

Le `string` déplace la frontière, il ne la supprime pas — vérifié par sondes sur
`POST /games` :

| `botDifficulty` | Statut | Forme |
|---|---|---|
| `"Expert"`, `""`, `null`, champ absent | 400 | `ValidationProblem` — le filtre a parlé |
| `2`, `true`, `["HuntTarget"]` | 400 | `BadHttpRequestException` — la **liaison** a refusé, en amont du filtre |

Le statut reste 400 dans les deux cas, donc le client n'a rien à distinguer ; la
forme du corps, elle, diffère. Le comportement est épinglé par
`CreateGame_WithANonTextualDifficulty_IsRefusedByTheBinderNotTheValidator`.

La validation compare le nom à `BotDifficulties.Names`. Elle **n'utilise pas**
`Enum.TryParse`, vérifié par exécution :

```
TryParse("42") = True  -> 42     IsDefined=False
TryParse("0")  = True  -> Random IsDefined=True
```

`Enum.TryParse` accepte la valeur numérique sous-jacente, y compris hors
énumération ; `Enum.IsDefined` rattrape `"42"` mais pas `"0"`. Le contrat exposé
est la liste des noms, pas la représentation entière.

## Conséquences
- `Game` gagne un paramètre optionnel `BotDifficulty botDifficulty =
  BotDifficulty.Random`. Optionnel **parce qu'une partie `Local` n'oppose aucun
  bot** : exiger un niveau y reviendrait à inventer une donnée sans objet.
- La difficulté est publiée dans `GameView` puis dans `GameViewResponse` : le
  joueur voit à l'écran ce qu'il affronte. Aucune information secrète n'y
  transite.
- Le catalogue des libellés (`BotDifficultyCatalog`) vit dans `BattleShip.Models`
  et non dans `Domain` : c'est le front qui les affiche, et `App` ne référence
  jamais `Domain`. Un test interdit qu'il diverge de l'énumération.
- **Limite assumée — le damier suppose un navire de deux cases minimum.** Le
  balayage d'une case sur deux de `HuntTargetParity` ne peut rien manquer tant
  que le plus petit navire occupe deux cases adjacentes. Le jour où la flotte
  devient personnalisable (item 6), un navire d'une seule case rendrait ce niveau
  incorrect, pas seulement moins bon.
- **Limite assumée — la résolution des navires coulés est approchée, et se
  trompe dans une partie sur deux.** Le bot déduit qu'une case touchée appartient
  à un navire coulé si elle est alignée avec une case `Sunk`, sans case intacte
  entre les deux. Le contact entre navires étant autorisé (`AGENTS.md` § 3), deux
  navires alignés et mitoyens se confondent. Ce n'est pas un cas de bord : mesuré
  sur 2 000 parties en comparant le calcul du bot à la vérité terrain, **50,9 %
  des parties** contiennent au moins une case d'un navire encore à flot classée à
  tort comme coulée. Le bot retourne alors chasser trop tôt ; il perd de
  l'efficacité — c'est déjà compté dans les 1,31 tir ci-dessous — et il ne tire
  jamais un coup interdit.
- **Compromis délibéré — la résolution coûte des tirs et on la garde quand
  même.** Mesurée, elle dégrade `HuntTarget` de 1,31 tir. Elle est conservée
  parce que le critère n'est pas le nombre de tirs mais ce que la difficulté
  prétend faire : sans elle, la part de tirs joués en chasse tombe de 71 % à
  39 % pour `HuntTargetParity`, donc son damier — sa seule spécificité — ne
  gouverne plus qu'une minorité de ses décisions. Ces deux mesures ont été
  obtenues **hors suite de tests**, en modifiant le code puis en le rétablissant
  et avec des compteurs depuis retirés : elles ne sont pas rejouées par
  `dotnet test` et demandent de reposer les sondes pour être reproduites.
  La garder pour le seul `HuntTargetParity` économiserait ce tir ; c'est écarté
  parce que `CONTEXT.md` définit les deux difficultés comme ne différant **que**
  par le balayage. Les faire diverger sur un second point rendrait l'écart mesuré
  entre elles ininterprétable : il additionnerait deux changements, et on ne
  saurait plus dire ce que le damier apporte. Voir `REVUE-IA.md`, revue 4.

## Vérification et réexamen

Banc de mesure `BotDrill` : une stratégie tire sur une grille jusqu'à couler la
flotte, sans passer par `Game`. Moyennes sur 100 parties appariées (mêmes graines
de placement pour les trois difficultés) — c'est le chiffre que les tests
vérifient à chaque exécution.

| Difficulté — **code livré**, 100 parties | Tirs moyens |
|---|---|
| `Random` | 95,3 |
| `HuntTarget` | 64,6 |
| `HuntTargetParity` | 58,7 |

Ces valeurs ont une précision d'environ ±1 tir et ne sont pas significatives à
la décimale : la même variante donne 65,9 sur 4 000 parties. Elles ne valent que
pour le **classement**, dont les écarts — 30 tirs puis 6 — sont très supérieurs
à cette précision. `REVUE-IA.md` revue 4 porte la table complète par variante et
par échantillon ; une valeur de 64,6 y désigne une **autre** variante.

Les trois se classent, ce qui est la seule chose qui donne un sens au mot
« difficulté ». Les bornes des tests sont larges et arrondies vers l'extérieur :
elles interdisent qu'un niveau dérive au point de se confondre avec un autre,
elles ne pincent pas une valeur.

Les écarts **entre variantes d'un même algorithme** sont d'un autre ordre de
grandeur — environ 1 à 2 tirs, pour un écart-type par partie proche de 10. Ils
ont été établis hors suite de tests, sur 4 000 parties appariées, avec l'erreur
type des différences. Voir `REVUE-IA.md`, revue 4 : à 500 parties, deux de ces
écarts étaient indiscernables du bruit et l'un d'eux a changé de conclusion en
augmentant l'échantillon.

Contrôles de mutation exécutés, chacun rétabli ensuite :

| Règle cassée | Tests au rouge |
|---|---|
| Résolution des navires coulés désactivée | 2 / 7 |
| Toute case touchée réputée coulée | 3 / 7 domaine, 4 / 5 comparaison |
| Préférence d'alignement retirée | 1 / 7 |
| Damier appliqué aussi à la traque | 1 / 3 |
| Damier retiré du niveau `Parity` | 1 / 3 domaine, 1 / 5 comparaison |
| L'endpoint ignore `game.BotDifficulty` | 1 / 6 |
| Validation par `Enum.TryParse` | 1 / 18 |
| Catalogue partagé désynchronisé | 1 / 6 |

À réexaminer :

- **Item 6 (flotte personnalisable)**, qui peut invalider le damier — un navire
  d'une seule case rendrait `HuntTargetParity` incorrect.
- **Item 6 encore**, pour un piège de signature : `IBotStrategy.ChooseTarget(view,
  size)` reçoit dans `view.Size` la grille **du bot** et dans `size` celle de
  **l'adversaire**. Elles sont identiques tant que la taille n'est pas
  paramétrable. `HuntTargetBot` n'utilise que `size`, ce qui est correct ; rien
  n'empêche une future stratégie de lire `view.Size` par erreur.
- **Item 5 (SQLite)**, où la difficulté devra être persistée.
- **Toute stratégie qui retiendrait un état entre deux tours** : la fabrique en
  construit une neuve à chaque appel, donc une file de cibles mémorisée serait
  silencieusement réinitialisée. Le Singleton n'est légitime que tant que les
  stratégies sont des fonctions pures de la `GameView`, et aucun test ne protège
  cette propriété.

## Références
- ADR 0003 (serveur autoritaire), ADR 0004 (`IGameRepository`), ADR 0006 (validation par filtre)
- `AGENTS.md` § 4, § 10 item 2 · `CONTEXT.md`, section « Comportement des bots »
- `REVUE-IA.md`, revue 4
