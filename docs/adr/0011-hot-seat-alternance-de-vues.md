# ADR 0011 : le hot-seat est une alternance de vues, et la passation protège l'écran

## Statut et date
Accepté — 2026-09-15. Rédigé avec l'item 4 du backlog (multijoueur local).

## Contexte
L'ADR 0007 a écarté le multijoueur **en ligne** au profit du hot-seat : deux
humains, un appareil, chacun son tour. L'ADR 0003 pose l'invariant qui rend la
chose délicate : le serveur ne sert jamais qu'une `GameView`, celle d'un seul
joueur, et le client ne transmet aucune identité.

Ces deux décisions se combinent bien — trop bien, presque : l'agrégat savait
déjà jouer une partie à deux humains avant que cet item ne commence. Six tests
de domaine écrits au début de l'item passent **sans une ligne de production**.

La question qui reste n'est donc pas « comment faire alterner deux joueurs »
mais : **qu'est-ce qui empêche le joueur qui vient de tirer de voir la flotte de
l'autre ?** Car dès le tir résolu, le serveur sert la vue du suivant — et cette
vue contient sa flotte.

## Décision

### 1. L'alternance est déjà celle du serveur
Aucune notion nouvelle dans le domaine. `FireLocked` échange `_current` et
`_waiting` comme en `Solo` ; `ViewForClient()` suit le joueur courant, qui n'est
jamais un bot en `Local`. Un seul concept de tour, comme le voulait le cadrage.

Les deux seules différences visibles du serveur :
- `POST /games` construit un second joueur humain au lieu d'un bot ;
- `POST /bot-turn` répond `409` — il n'y a pas de bot à faire jouer.

### 2. La passation est une protection d'écran, et rien d'autre
`GameSession` retient le nom du joueur attendu (`HandoverTo`). Tant qu'il n'est
pas nul, l'interface **n'affiche rien de la vue** : ni grille, ni flotte, ni
compteur. Le joueur confirme, et seulement alors l'écran se peuple.

**Ce que cette protection ne fait pas, et il faut le dire clairement :** la vue
du joueur suivant est **déjà dans le navigateur** quand l'écran de passation
s'affiche. Elle y est arrivée en réponse au tir. Quiconque ouvre les outils de
développement la lit.

Ce n'est pas un défaut qu'on pourrait corriger : sur un appareil partagé, les
données des deux joueurs passent nécessairement par le même navigateur. Faire
confirmer la passation au serveur n'ajouterait rien, puisque c'est le même client
non fiable qui confirmerait. La passation protège d'un regard par-dessus
l'épaule, pas d'un adversaire déterminé.

C'est exactement ce que `CONTEXT.md` annonçait depuis le cadrage — « c'est une
protection d'affichage ; la protection réelle est que le serveur ne transmet
jamais autre chose qu'une `GameView` » — et l'implémentation ne change pas cette
lecture, elle la confirme.

### 3. `opponentName` n'est exigé qu'en `Local`
Règle de validation **conditionnelle** :

```csharp
RuleFor(request => request.OpponentName)
    .NotEmpty()
    .MaximumLength(40)
    .When(request => string.Equals(request.Mode, nameof(GameMode.Local), StringComparison.OrdinalIgnoreCase));
```

En `Solo`, l'adversaire est un bot que le serveur nomme lui-même : exiger un nom
reviendrait à réclamer une donnée sans objet, ce que l'ADR 0009 a déjà refusé
pour la difficulté du constructeur de `Game`.

**Asymétrie assumée** : `botDifficulty`, lui, reste exigé dans les deux modes.
Il a un défaut qui veut dire quelque chose — « si cette partie avait un bot » —
là où `opponentName` n'en a aucun. Une partie `Local` porte donc une difficulté
que rien n'utilise, et un test le constate explicitement plutôt que de le taire.

## Conséquences
- Le domaine n'acquiert **aucune règle nouvelle**. Tout l'item est dans l'API et
  le front.
- `CanRetryBotTurn` exclut le hot-seat : sans bot, il n'y a pas de tour à
  relancer.
- Le nom du tireur doit être **capturé avant le tir**. Une fois le tir résolu, la
  vue décrit déjà l'autre joueur : un message composé après coup s'adresserait au
  mauvais lecteur. Le message dit « Olivier manque », pas « Vous manquez ».
- L'écran de création masque la difficulté du bot en `Local`, et le bandeau n'y
  affiche pas son badge : afficher un adversaire qui n'existe pas est un mensonge
  d'interface.
- **Limite assumée** : la vue du joueur suivant est dans le navigateur avant la
  passation. Voir ci-dessus.
- **Limite assumée** : rien n'empêche un joueur de confirmer la passation à la
  place de l'autre. Le bouton est une convention entre deux personnes assises à
  la même table, pas un contrôle.

## Vérification et réexamen

`dotnet build` puis `dotnet test` : **192 tests, 0 échec** (175 avant l'item).

Six tests de domaine écrits **avant** toute modification de production passent
immédiatement : c'est le contrôle qui établit que l'agrégat savait déjà jouer à
deux humains, plutôt que de l'affirmer.

Le test qui porte l'invariant : `ASequenceOfShots_NeverServesTwoFleetsAtOnce`
joue douze tirs et vérifie qu'aucune réponse ne porte plus d'une flotte, et que
la flotte servie à un joueur donné ne change jamais. Il est doublé d'un garde —
si les deux flottes étaient identiques, il ne discriminerait rien.

Quatre mutations, chacune rétablie :

| Règle cassée | Tests au rouge |
|---|---|
| Mode de jeu non validé | 3 / 11 |
| `opponentName` non exigé en `Local` | 2 / 11 |
| Le second joueur est toujours un bot | 3 / 11 |
| Le mode demandé est ignoré | 1 / 11 |

**Vérification navigateur** : partie hot-seat créée, tir joué, écran de passation
affiché sans rien révéler, confirmation, vue du second joueur avec **sa** flotte.
Elle a révélé trois défauts d'affichage qu'aucun test ne couvrait, et un défaut
de rendu — voir `REVUE-IA.md`, revue 7.

À réexaminer à l'item 5 (persistance), où une partie hot-seat reprise après
redémarrage devra retrouver le bon joueur courant.

## Références
- ADR 0003 (serveur autoritaire), ADR 0007 (local plutôt qu'en ligne), ADR 0009 (donnée sans objet), ADR 0010 (placement)
- `AGENTS.md` § 4, § 5, § 10 item 4 · `CONTEXT.md`, entrées `Passation` et `Tireur`
- `REVUE-IA.md`, revue 7
