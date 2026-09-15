# Bataille navale — C# / ASP.NET

Une bataille navale jouable dans le navigateur, du front Blazor WebAssembly
jusqu'au moteur de jeu côté serveur.

**Binôme** : Olivier Recher ([@OlivierRecher](https://github.com/OlivierRecher)) ·
Ulysse ([@Oulssyyy](https://github.com/Oulssyyy))

TP noté du cours C# / ASP.NET — Christophe MOMMER, HTS Learning.

---

## Prérequis

| | |
|---|---|
| **SDK .NET 10** | [`global.json`](./global.json) demande `10.0.400` avec `rollForward: latestFeature` : tout SDK .NET 10 de version **égale ou supérieure** convient, y compris une bande de fonctionnalités plus récente. `dotnet --version` doit répondre `10.x`. |
| **Certificat HTTPS de développement** | Obligatoire : sans lui le navigateur refuse la page. Voir ci-dessous. |
| **Un navigateur** | L'interface est du WebAssembly ; tout navigateur récent convient. |

### Approuver le certificat de développement

```bash
dotnet dev-certs https --trust
```

macOS demande le mot de passe de session et ouvre une fenêtre de confirmation.

Sans cette étape, le certificat existe mais n'est **pas approuvé** : Chrome
affiche une page d'erreur à la place de l'application, sans message explicite
côté serveur. Pour vérifier :

```bash
dotnet dev-certs https --check --trust
```

---

## Lancer le projet

L'API et le front sont **deux applications distinctes** : il faut deux
terminaux, et l'API doit démarrer en premier.

**Terminal 1 — l'API**

```bash
dotnet run --project BattleShip.API --launch-profile https
```

Elle écoute sur `https://localhost:7027`. En développement, la description
OpenAPI est servie sur `https://localhost:7027/openapi/v1.json`.

**Terminal 2 — le front**

```bash
dotnet run --project BattleShip.App --launch-profile https
```

Il écoute sur `https://localhost:7142`, et le navigateur s'ouvre tout seul.

C'est tout. L'écran de création demande :

- **l'adversaire** — un bot, ou un second joueur sur le même appareil ;
- **la difficulté du bot** en solo — Novice, Chasseur ou Vétéran ;
- **le placement de la flotte** — aléatoire, ou manuel navire par navire ;
- la taille de la grille, de 8 × 8 à 20 × 20.

### Si vous ne voulez pas toucher à votre trousseau

Le certificat de développement n'est nécessaire que pour le lancement nominal.
Tout fonctionne aussi en HTTP simple, gRPC-Web compris — vérifié :

```bash
dotnet run --project BattleShip.API --launch-profile http   # http://localhost:5166
dotnet run --project BattleShip.App --launch-profile http   # http://localhost:5019
```

en pointant au préalable `BattleShip.App/wwwroot/appsettings.json` sur
`http://localhost:5166`. L'origine `http://localhost:5019` est déjà autorisée par
la politique CORS de l'API.

### Si un port change

Deux réglages distincts, à ne pas confondre — le CORS valide l'origine du
**front**, pas le port de l'API.

| Ce qui change | Ce qu'il faut modifier |
|---|---|
| Le port de l'**API** | `ApiBaseAddress` dans [`BattleShip.App/wwwroot/appsettings.json`](./BattleShip.App/wwwroot/appsettings.json) |
| Le port ou le schéma du **front** | `Cors:AllowedOrigins` dans la configuration de l'API |

Se tromper de côté donne le même symptôme : le navigateur bloque les appels sans
que le serveur ne journalise quoi que ce soit.

---

## Lancer les tests

```bash
dotnet test
```

Tous verts, répartis en tests métier (`BattleShip.Tests/Domain/`) et tests
d'intégration sur l'API (`BattleShip.Tests/Api/`), ces derniers sur un vrai
SQLite en mémoire. Le compte exact figure dans la sortie de `dotnet test` ; il
n'est pas recopié ici, où il se périmerait à chaque item.

Ce que ces tests ne couvrent **pas** : le front Blazor, dont aucun composant
n'est instancié. C'est une décision, pas un oubli — voir « Limites connues ».

Le classement des difficultés de bot, lui, **est** dans la suite
(`BotDifficultyComparisonTests`, 100 parties par niveau). Ce qui vit hors suite,
ce sont les campagnes de 4 000 parties qui ont servi à comparer des variantes
d'un même algorithme — voir `REVUE-IA.md`, revue 4.

En intégration continue, la variable `CI=true` rend les avertissements
bloquants. Pour reproduire la CI en local :

```bash
CI=true dotnet build && CI=true dotnet test
```

---

## Fonctionnalités

- **Partie complète contre un bot**, de la création à la victoire, avec relance.
- **Trois difficultés de bot**, choisies à la création et jamais transmises par le
  client ensuite. Elles se classent, et c'est mesuré : sur 100 parties appariées,
  le code livré coule une flotte en **95,3** tirs en moyenne au niveau `Random`,
  **64,6** en `HuntTarget`, **58,7** en `HuntTargetParity`. Ces valeurs ont une
  précision d'environ ±1 tir ; elles portent le classement, pas la décimale.
- **Placement de la flotte au choix** : aléatoire par le serveur, ou **manuel**,
  navire par navire.
- **Composition de flotte personnalisable** : combien de navires de quels types,
  parmi porte-avions 5, cuirassé 4, croiseur 3, sous-marin 3, torpilleur 2 et
  vedette 1 — sur une grille de 8×8 à 20×20. La flotte classique reste le défaut.
  Le bot `Vétéran` adapte son balayage : son damier suit la longueur du plus petit
  navire, donc il couvre toute la grille dès qu'une vedette est en jeu.
- **Multijoueur local (hot-seat)** : deux joueurs sur le même appareil, avec un
  écran de passation qui masque tout entre deux tours.
- **Historique et statistiques**, page `/historique` : les parties passées, leur
  issue et le nombre de tirs, plus les compteurs globaux et la précision.
- **Le serveur est autoritaire.** Le client ne transmet jamais d'identité de
  joueur ; c'est le serveur qui décide quelle vue il accepte de publier. Les
  positions adverses non découvertes ne quittent jamais le serveur.
- **Tir en gRPC-Web**, le reste du contrat en HTTP/JSON.
- **Validation FluentValidation** sur toutes les entrées serveur, par filtre
  d'endpoint côté HTTP et par intercepteur côté gRPC.

### Les cinq projets

```
BattleShip.Domain   moteur de jeu pur — zéro dépendance
BattleShip.Models   DTO et contrats de frontière — zéro dépendance
BattleShip.API      Minimal API  → Domain, Models
BattleShip.App      Blazor WASM  → Models
BattleShip.Tests    xUnit        → Domain, API
```

`App` ne référence jamais `Domain` : le navigateur n'embarque pas le moteur.

### Contrat d'API

```
POST   /games                 → 201 GameView        (mode, botDifficulty, fleetPlacement)
GET    /games/{id}            → 200 GameView | 404
POST   /games/{id}/shots      → 200 ShotOutcome | 400 | 404 | 409
POST   /games/{id}/bot-turn   → 200 ShotOutcome | 404 | 409
PUT    /games/{id}/fleet      → 200 GameView | 400 | 404 | 409
GET    /games?limit=n         → 200 GameSummary[]   (historique)
GET    /stats                 → 200 Statistics
```

`POST /games` accepte un champ `fleet` : un nom de type par navire, répétitions
comprises. Absent, la flotte classique s'applique. Une composition vide, de plus
de quinze navires, contenant un navire plus long que la grille, ou occupant plus
du tiers des cases, est refusée par la validation.

Les valeurs nommées — mode, difficulté, placement, type de navire, orientation —
voyagent en **texte**, jamais en entier : un nom inconnu doit produire le 400
uniforme de FluentValidation, pas une erreur de désérialisation en amont du
filtre.

Plus l'opération gRPC `battleship.Battle/Fire`, que le front utilise réellement
pour tirer. [`api.http`](./api.http) contient des requêtes prêtes à jouer, y
compris les cas d'erreur.

---

## Arbitrages du backlog

L'ordre des fonctionnalités est imposé et séquentiel : une feature est livrée,
testée et mergée avant d'attaquer la suivante.

| # | Feature | État |
|---|---|---|
| 1 | Socle — moteur, API, front, validation, gRPC | **Livré** |
| 2 | Difficultés de bot — `Random`, `HuntTarget`, `HuntTargetParity` | **Livré** |
| 3 | Placement manuel de la flotte | **Livré** |
| 4 | Multijoueur local (hot-seat) | **Livré** |
| 5 | Persistance, historique, statistiques | **Livré** |
| 6 | Personnalisation — grille, composition de flotte | **Livré** |

**Le multijoueur en ligne est hors périmètre**, décision assumée et tracée dans
l'[ADR 0007](./docs/adr/0007-multijoueur-local-plutot-quen-ligne.md) : trois
extensions finies valent mieux qu'une infrastructure temps réel bancale, que le
binôme ne saurait ni terminer ni défendre.

---

## Limites connues

- **Les parties survivent au redémarrage**, depuis l'item 5 : SQLite conserve les
  placements et le journal, tout le reste est rejoué. Voir
  l'[ADR 0012](./docs/adr/0012-persistance-par-rejeu-du-journal.md).
- **Un seul processus.** Le cache des parties vivantes et le verrou de l'agrégat
  ne couvrent qu'une instance de l'API. Deux instances sur la même base
  joueraient chacune sur sa copie.
- **Le dépôt est synchrone**, donc les entrées/sorties SQLite bloquent un fil du
  pool. Sans conséquence à cette échelle ; premier point à reprendre autrement.
  Voir l'[ADR 0008](./docs/adr/0008-verrou-par-partie-sur-l-agregat.md) et
  l'[ADR 0012](./docs/adr/0012-persistance-par-rejeu-du-journal.md).
- **L'erreur gRPC-Web attendue n'est pas déclenchable depuis l'interface** :
  l'écran désactive les cases déjà visées, donc le `FailedPrecondition` ne se
  démontre que par les tests d'intégration
  (`BattleGrpcServiceTests`) ou par un appel direct.
- **Le tour du bot est un appel distinct du tir du joueur.** Si cet appel
  échoue, l'interface le signale et propose de le relancer, mais la partie reste
  en attente jusque-là.
- **Aucun test automatisé côté Blazor.** Le comportement du client est vérifié à
  la main dans le navigateur ; un double de `HttpMessageHandler` et `bUnit`
  seraient nécessaires pour le couvrir. Cette zone a produit trois des défauts
  les plus instructifs du projet, tous invisibles à une suite verte — voir
  `REVUE-IA.md`, revues 5 et 7.
- **L'écran de passation du hot-seat protège l'écran, pas les données.** La vue
  du joueur suivant est déjà dans le navigateur quand la passation s'affiche :
  elle y est arrivée en réponse au tir. Sur un appareil partagé, les données des
  deux joueurs passent nécessairement par le même navigateur. Voir
  l'[ADR 0011](./docs/adr/0011-hot-seat-alternance-de-vues.md).
- **Les longueurs restent attachées aux types de navires.** On choisit combien de
  navires de quels types, pas des longueurs libres.
- **Une flotte occupant plus du tiers de la grille est refusée**, même si elle
  aurait pu tenir. Le placement aléatoire procède par essais et échoue de façon
  *aléatoire* au-delà d'une certaine densité : le plafond échange une acceptation
  imprévisible contre un refus prévisible. Sa valeur est mesurée, pas choisie.
  Voir l'[ADR 0013](./docs/adr/0013-flotte-personnalisable-et-pas-du-damier.md).
- **Le classement des difficultés n'a été mesuré que sur la flotte classique.**
  Rien n'établit que le bot `Vétéran` reste meilleur que le `Chasseur` sur une
  flotte de vedettes — son damier y couvre toute la grille, donc les deux niveaux
  se confondent, et l'interface ne le dit pas.

---

## Documentation

| Fichier | Contenu |
|---|---|
| [`AGENTS.md`](./AGENTS.md) | Source unique de vérité : contraintes, architecture, contrat, conventions, backlog |
| [`CONTEXT.md`](./CONTEXT.md) | Vocabulaire du domaine |
| [`docs/adr/`](./docs/adr/) | Décisions structurantes, avec options écartées et vérifications |
| [`PROMPTS.md`](./PROMPTS.md) | Journal des échanges IA décisifs |
| [`REVUE-IA.md`](./REVUE-IA.md) | Revues argumentées de propositions IA, dont une rejetée |
