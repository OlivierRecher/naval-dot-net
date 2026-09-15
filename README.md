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
| **SDK .NET 10** | Version `10.0.400`, épinglée par [`global.json`](./global.json). `dotnet --version` doit répondre `10.0.400` ou une révision supérieure de la même bande. |
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

C'est tout : saisir un nom, choisir une taille de grille, et jouer.

### Si le port de l'API change

Le front lit l'adresse de l'API dans
[`BattleShip.App/wwwroot/appsettings.json`](./BattleShip.App/wwwroot/appsettings.json) :

```json
{ "ApiBaseAddress": "https://localhost:7027" }
```

Changer ce port impose d'ajouter l'origine correspondante à la politique CORS de
l'API — section `Cors:AllowedOrigins` de sa configuration. Sans cela le
navigateur bloque les appels sans que le serveur ne journalise quoi que ce soit.

---

## Lancer les tests

```bash
dotnet test
```

**87 tests**, tous verts, répartis en tests métier (`BattleShip.Tests/Domain/`)
et tests d'intégration sur l'API (`BattleShip.Tests/Api/`).

En intégration continue, la variable `CI=true` rend les avertissements
bloquants. Pour reproduire la CI en local :

```bash
CI=true dotnet build && CI=true dotnet test
```

---

## Fonctionnalités

- **Partie complète contre un bot**, de la création à la victoire, avec relance.
- **Placement aléatoire** de la flotte classique — porte-avions 5, cuirassé 4,
  croiseur 3, sous-marin 3, torpilleur 2 — sur une grille de 8×8 à 20×20.
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
POST   /games                 → 201 GameView
GET    /games/{id}            → 200 GameView | 404
POST   /games/{id}/shots      → 200 ShotOutcome | 400 | 404 | 409
POST   /games/{id}/bot-turn   → 200 ShotOutcome | 404 | 409
```

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
| 2 | Niveaux de bot — `Random`, `HuntTarget`, `HuntTargetParity` | En cours |
| 3 | Placement manuel de la flotte | À venir |
| 4 | Multijoueur local (hot-seat) | À venir |
| 5 | Persistance, historique, statistiques | À venir |
| 6 | Personnalisation — grille, composition de flotte | À venir |

**Le multijoueur en ligne est hors périmètre**, décision assumée et tracée dans
l'[ADR 0007](./docs/adr/0007-multijoueur-local-plutot-quen-ligne.md) : trois
extensions finies valent mieux qu'une infrastructure temps réel bancale, que le
binôme ne saurait ni terminer ni défendre.

---

## Limites connues

- **Les parties sont perdues au redémarrage du serveur.** Le stockage est en
  mémoire derrière `IGameRepository` ; la bascule vers une base est l'item 5 du
  backlog. Voir l'[ADR 0004](./docs/adr/0004-igamerepository-en-memoire.md).
- **Un seul processus.** L'agrégat `Game` est protégé par un verrou par partie,
  qui ne couvre pas plusieurs instances de l'API. Voir
  l'[ADR 0008](./docs/adr/0008-verrou-par-partie-sur-l-agregat.md).
- **L'erreur gRPC-Web attendue n'est pas déclenchable depuis l'interface** :
  l'écran désactive les cases déjà visées, donc le `FailedPrecondition` ne se
  démontre que par les tests d'intégration
  (`BattleGrpcServiceTests`) ou par un appel direct.
- **Le tour du bot est un appel distinct du tir du joueur.** Si cet appel
  échoue, l'interface le signale et propose de le relancer, mais la partie reste
  en attente jusque-là.
- **Aucun test automatisé côté Blazor.** Le comportement du client est vérifié à
  la main ; un double de `HttpMessageHandler` serait nécessaire pour le couvrir.

---

## Documentation

| Fichier | Contenu |
|---|---|
| [`AGENTS.md`](./AGENTS.md) | Source unique de vérité : contraintes, architecture, contrat, conventions, backlog |
| [`CONTEXT.md`](./CONTEXT.md) | Vocabulaire du domaine |
| [`docs/adr/`](./docs/adr/) | Décisions structurantes, avec options écartées et vérifications |
| [`PROMPTS.md`](./PROMPTS.md) | Journal des échanges IA décisifs |
| [`REVUE-IA.md`](./REVUE-IA.md) | Revues argumentées de propositions IA, dont une rejetée |
