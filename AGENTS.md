# AGENTS.md — Bataille navale C# / ASP.NET

Source unique de vérité du projet. `CLAUDE.md` pointe ici. Le vocabulaire du
domaine est dans [CONTEXT.md](./CONTEXT.md) — le lire avant d'écrire du code.

---

## 1. Le projet

TP noté du cours C# / ASP.NET (Christophe MOMMER, HTS Learning). Une bataille
navale jouable, du navigateur jusqu'au serveur.

- **Binôme** : Olivier Recher (@OlivierRecher) · Ulysse (@Oulssyyy)
- **Dépôt** : `github.com/OlivierRecher/naval-dot-net`
- **Note** : 50 % projet (binôme) + 50 % QCM individuel (20 questions, dernière heure du jour 5)
- **Remise** : lien du dépôt + **hash du commit** à contact@hts-learning.com, **avant** le début du QCM. Seul ce commit est évalué.

Ce qui est évalué sur le projet : fonctionnement, qualité du code, **maîtrise de
l'IA**, tests et collaboration, **ambition des extensions**.

> Le support sanctionne explicitement le périmètre minimal : « Choisir de s'en
> tenir au minimum engage votre responsabilité et sera apprécié comme tel. »
> L'ambition n'est pas un bonus, c'est un critère.

---

## 2. Contraintes imposées — non négociables

Aucune de ces lignes ne se discute ni ne se contourne.

- [ ] **.NET 10** (LTS). SDK épinglé par `global.json`.
- [ ] **API ASP.NET Core en Minimal API** — pas de contrôleurs MVC.
- [ ] **Front Blazor WebAssembly**.
- [ ] **Bibliothèque de modèles partagée** entre l'API et le front.
- [ ] **Partie complète jouable contre un bot**, de la création à la victoire.
- [ ] **FluentValidation sur toutes les entrées serveur** — HTTP *et* gRPC.
- [ ] **gRPC-Web fonctionnel sur au moins un échange**, avec une **erreur attendue démontrable**.
- [ ] **Tests métier ET tests d'intégration**.
- [ ] **Livrables IA** : `PROMPTS.md`, `docs/adr/`, `REVUE-IA.md` (≥ 3 revues).
- [ ] **Historique Git exploitable**, avec changements relus par le binôme.

---

## 3. Règles du jeu — nos décisions

Libres selon le support, donc à défendre. Toutes paramétrables dès le départ.

- Grille **10 × 10**.
- Flotte : `Carrier` 5 · `Battleship` 4 · `Cruiser` 3 · `Submarine` 3 · `Destroyer` 2.
- Placement **horizontal ou vertical** uniquement. Chevauchement interdit,
  **contact autorisé** (deux navires peuvent se toucher).
- **Le tour passe toujours**, touché ou non.
- **Une case déjà visée est une tentative refusée** : elle ne modifie pas la
  partie et **ne consomme pas le tour**.
- Fin de partie quand une flotte est entièrement coulée. Aucun tir n'est accepté ensuite.

---

## 4. Architecture

### Projets

```
BattleShip.Domain   moteur de jeu pur — ZÉRO dépendance (ni HTTP, ni JSON, ni gRPC, ni EF)
BattleShip.Models   DTOs et contrats de frontière — ZÉRO dépendance
BattleShip.API      Minimal API  → Domain, Models
BattleShip.App      Blazor WASM  → Models
BattleShip.Tests    xUnit        → Domain, API   (dossiers Domain/ et Api/)
```

**Règles de dépendance, à ne jamais violer :**

- `Domain` ne référence rien. Si un `using` de transport y apparaît, la conception est cassée.
- `App` ne référence **jamais** `Domain` — le navigateur ne doit pas embarquer le moteur.
- `Models` ne contient que des DTOs : aucune règle de jeu.

### État serveur

- Le serveur est **autoritaire**. Toutes les règles sont vérifiées côté serveur ; le client n'est jamais cru.
- `Game` est un **agrégat mutable** doublé d'un **journal de tirs en ajout seul**
  (`Shots`). Le journal fournit rejeu, historique et statistiques presque gratuitement.
- Les vérifications de règles sont des **fonctions pures**, testables sans instancier une partie.
- Stockage : **SQLite via EF Core** derrière `IGameRepository`, enregistré en
  **Scoped** avec son `DbContext`. Seuls les placements et le journal sont
  écrits ; tout le reste est **rejoué**. Un cache Singleton conserve les parties
  vivantes, sans quoi le verrou de l'agrégat ne sérialiserait plus rien.
  Voir ADR 0012.
- `IGameRepository` porte un **point de validation** `Save(Game)`. L'ADR 0004
  n'en avait pas prévu : un dépôt en mémoire n'en a pas besoin, un dépôt
  persistant ne peut pas s'en passer.
- Le dictionnaire concurrent protège la **table**, pas la partie qu'elle contient.
  L'agrégat `Game` porte son propre verrou et sérialise ses transitions et ses
  projections. Voir ADR 0008.

### Secret des positions — invariant structurel

**Le client ne transmet jamais d'identité de joueur.** Le serveur renvoie
toujours la `GameView` du **viewer** qu'il choisit lui-même, et rien d'autre :
le joueur dont c'est le tour, sauf si ce joueur est un bot — un bot n'a pas de
client, lui servir sa vue publierait sa flotte. Voir ADR 0003 et `CONTEXT.md`.

Symétriquement, le client ne **joue** jamais le tour d'un bot : `FireFromClient`
le refuse, `PlayBotTurn` est le seul chemin par lequel le serveur fait tirer un
bot.

C'est délibéré : l'invariant devient impossible à violer plutôt que « vérifié ».
L'écran de passation du mode `Local` est un confort d'affichage, pas la
protection. Toute proposition réintroduisant un `playerId` fourni par le client
est à rejeter.

---

## 5. Contrat d'API

```
POST   /games                 → 201 GameView   (mode, botDifficulty, fleetPlacement)
GET    /games/{id}            → 200 GameView | 404
POST   /games/{id}/shots      → 200 ShotOutcome | 400 | 404 | 409
POST   /games/{id}/bot-turn   → 200 ShotOutcome | 404 | 409
PUT    /games/{id}/fleet      → 200 GameView | 400 | 404 | 409   (placement manuel)
GET    /games?limit=n         → 200 GameSummary[]                (historique)
GET    /stats                 → 200 Statistics
```

- `POST /shots` résout **uniquement le coup du joueur courant**. La riposte du
  bot est un appel distinct : un seul concept de tour, code identique en `Solo`
  et en `Local`.
- **400** : entrée malformée ou hors grille (produit par FluentValidation).
- **409** : conflit avec l'état courant — case déjà visée, pas son tour, partie terminée.
- **404** : partie inconnue.
- `PUT /fleet` reçoit la **flotte entière**, jamais un navire à la fois : une
  flotte refusée ne doit pas laisser la grille à moitié remplie. Voir ADR 0010.
- `POST /games` reçoit le **mode** (`Solo` ou `Local`). `opponentName` n'est
  exigé qu'en `Local` — en `Solo` l'adversaire est un bot que le serveur nomme
  lui-même. Voir ADR 0011.
- `POST /games` reçoit la **difficulté du bot** sous forme de **nom** —
  `Random`, `HuntTarget` ou `HuntTargetParity`. Un `string` et non
  l'énumération : un nom inconnu doit produire un 400 de FluentValidation, pas
  une erreur de désérialisation en amont du filtre. La garantie porte sur les
  **chaînes** JSON ; un nombre ou un tableau reste refusé par la liaison, avec
  un 400 de forme différente. Voir ADR 0009.

### gRPC-Web — opération `Fire`

`Fire` est l'opération la plus fréquente : c'est la seule pour laquelle un
contrat binaire se justifie. **Le front tire réellement via gRPC-Web** — c'est
ce qui prouve que l'échange est fonctionnel.

Erreurs à démontrer : `InvalidArgument` (validation) · `NotFound` (partie
inconnue) · `FailedPrecondition` (case déjà visée, partie terminée).

L'endpoint HTTP `POST /shots` est conservé, testé et documenté dans `api.http` :
il coûte cinq lignes et donne à l'ADR un point de comparaison réel entre les
deux transports.

### Validation

Un **`IEndpointFilter` générique** côté HTTP et un **`Interceptor`** côté gRPC,
pas d'appel manuel à `ValidateAsync` répété dans chaque endpoint. Cet écart au
support est **délibéré et doit faire l'objet d'un ADR** : réponse
`ValidationProblem` uniforme, impossible d'oublier un endpoint.

---

## 6. Front Blazor

- Un service **`GameSession` scoped** détient la vue courante et notifie les
  composants via un événement `OnChange`.
- **Tous** les appels réseau passent par ce service. Aucun composant n'appelle
  `HttpClient` ni le client gRPC directement.
- Les composants sont du rendu pur.
- Habillage : **Bootstrap** (déjà livré par le template) + CSS Grid maison pour
  la grille. Pas de bibliothèque de composants.
- Chaque appel a trois états visibles : chargement, succès, échec. Un incident
  réseau ne doit jamais rendre l'interface inutilisable.

---

## 7. Conventions de code

- **Code en anglais** : types, membres, fichiers, `.proto`, noms de tests.
- **Livrables `.md` en français** : README, ADR, PROMPTS, REVUE-IA, ce fichier.
- **Messages de commit en français**, à l'impératif.
- Noms de tests : `Method_Scenario_ExpectedResult`
  (ex. `Fire_OnAlreadyTargetedCell_DoesNotConsumeTurn`).
- `Nullable` et `ImplicitUsings` activés via `Directory.Build.props`.
- `TreatWarningsAsErrors` **en CI uniquement** — jamais en local, pour ne pas
  bloquer une exploration.
- PascalCase pour les membres publics, camelCase pour paramètres et variables locales.
- Privilégier `record` pour les DTOs, constructeurs primaires pour l'injection.

---

## 8. Tests

- **TDD strict sur `BattleShip.Domain`** : le test d'abord, toujours.
- **Tests d'intégration après coup** sur l'API, via `WebApplicationFactory`.
- Un seul projet de tests, un seul `dotnet test`.

**La règle qui compte** : pour chaque règle métier, un test qui **échoue si on
casse la règle**. Le support est explicite — les tests doivent « détecter des
règles violées, pas seulement un succès nominal ».

Avant de déclarer un test utile : casser volontairement la règle, vérifier que
le test passe au rouge, rétablir. Cette manipulation est la matière première de
`REVUE-IA.md`.

Couverture minimale attendue sur le domaine : placement sans chevauchement ni
débordement, résolution des tirs, détection de navire coulé, fin de partie et
vainqueur, refus d'une case déjà visée sans consommation du tour, refus de tir
après la fin, non-divulgation des positions dans une `GameView`.

---

## 9. Git

- Branche `main` **protégée**, CI verte obligatoire.
- Une branche par feature : `feat/`, `fix/`, `docs/`, `test/`, `chore/`.
- **Une PR par feature, relue par l'autre membre.** La PR est la preuve
  matérielle de relecture exigée par la grille.
- `Co-authored-by:` quand vous travaillez ensemble.
- Commits atomiques : le message dit ce que le changement fait, pas quel fichier a bougé.

CI GitHub Actions sur chaque PR : `restore → build → test`.

---

## 10. Backlog — ordre imposé

Une feature est livrée, testée et mergée **avant** d'attaquer la suivante. Le
support est clair : « chaque fonctionnalité livrée doit être intégrée, vérifiée
et comprise par le binôme. »

1. **Socle** — moteur + API + front + validation + gRPC, partie complète contre un bot aléatoire
2. ~~**Niveaux de bot** — `Random`, `HuntTarget`, `HuntTargetParity`~~ — livré, ADR 0009
3. ~~**Placement manuel de la flotte** — le placement aléatoire reste offert~~ — livré, ADR 0010
4. ~~**Multijoueur local (hot-seat)** — écran de passation ; le secret reste garanti côté serveur~~ — livré, ADR 0011
5. ~~**Persistance + historique + statistiques** — SQLite / EF Core derrière `IGameRepository`~~ — livré, ADR 0012
6. **Personnalisation** — taille de grille, composition de flotte
7. *(stretch)* Déploiement

Le multijoueur **en ligne est hors périmètre**, décision assumée : trois
features finies valent mieux qu'une infra temps réel bancale.

Pas de planning par jour. On avance feature par feature, dans cet ordre.

---

## 11. Traçabilité IA — protocole

Ces livrables pèsent lourd (« Maîtrise de l'IA » est un critère à part entière)
et sont **irrattrapables après coup**. Ils s'écrivent **dans le même commit que
le code concerné**.

**Entrées courtes.** Le support le dit : « inutile de recopier toutes les
conversations », seulement les échanges décisifs.

### `PROMPTS.md`
Une entrée par échange **décisif**. Date · outil · contexte · prompt · réponse
résumée · décision (acceptée / adaptée / rejetée) · vérification · lien vers le commit.

### `docs/adr/`
Un ADR par choix structurant. Contexte · options envisagées · décision ·
conséquences · vérification et réexamen.

ADR déjà identifiés par le cadrage :
- `0001` Séparation `Domain` / `Models` (5 projets au lieu des 4 imposés)
- `0002` Agrégat mutable + journal de tirs en ajout seul
- `0003` Serveur autoritaire, aucune identité de joueur transmise par le client
- `0004` `IGameRepository` en mémoire, abstrait avant d'être nécessaire
- `0005` `Fire` en gRPC-Web, reste du contrat en HTTP/JSON
- `0006` Validation par filtre et intercepteur plutôt qu'appel manuel (écart au support)
- `0007` Multijoueur local plutôt qu'en ligne
- `0008` Verrou par partie porté par l'agrégat `Game`
- `0009` La difficulté du bot est une donnée de la partie, résolue par une fabrique
- `0010` Le statut `AwaitingFleet` se déduit des grilles, et la flotte est validée en bloc
- `0011` Le hot-seat est une alternance de vues, et la passation protège l'écran
- `0012` Persister le journal, rejouer le reste

### `REVUE-IA.md`
**Trois revues minimum.** Proposition · hypothèse à vérifier · expérience
(résultat attendu **écrit avant** exécution) · observation · décision · preuves et limites.

Contrainte : **au moins une proposition rejetée ou corrigée**. Trois
acceptations ne prouvent rien. Chaque revue doit répondre à « quelle erreur ce
contrôle aurait-il détectée ? ».

---

## 12. Définition de « terminé »

Une feature n'est terminée que si **toutes** ces lignes sont vraies :

- [ ] `dotnet build` et `dotnet test` passent
- [ ] Les nouveaux tests échouent quand on casse la règle qu'ils protègent
- [ ] Les entrées serveur correspondantes sont validées par FluentValidation
- [ ] Le comportement est jouable dans le navigateur, pas seulement testé
- [ ] `PROMPTS.md` / ADR mis à jour si une décision a été prise
- [ ] PR relue et mergée par l'autre membre
- [ ] Les **deux** membres savent l'expliquer

---

## 13. Livrables et checklist de remise

Fichiers attendus à la racine : `README.md`, `PROMPTS.md`, `REVUE-IA.md`,
`docs/adr/`, plus `CONTEXT.md` et ce fichier.

`README.md` doit contenir : les deux noms, les prérequis, **comment lancer**,
les fonctionnalités, les arbitrages du backlog et les limites connues.

Checklist finale (diapo 63) :

- [ ] Le README seul suffit à lancer le projet
- [ ] Une partie complète se joue, fin comprise, et on peut en relancer une
- [ ] Un échange gRPC-Web **et son erreur attendue** sont démontrables
- [ ] Entrées HTTP et gRPC validées ; règles vérifiées côté serveur
- [ ] Les tests détectent des règles violées
- [ ] `PROMPTS.md`, ADR et 3 revues IA à jour et reliés à des preuves
- [ ] Commits relus, historique lisible
- [ ] Chaque membre explique le fonctionnement, le périmètre et les limites
- [ ] Le code généré est identifié et son rôle compris

**Test de sortie** : faire lancer le projet par un autre binôme en suivant
**uniquement** le README.

---

## 14. Pièges .NET 10 connus

Tirés du support — à ne pas redécouvrir en perdant une heure.

- `dotnet new webapi` génère une **Minimal API** par défaut ; la solution est au format **`.slnx`**.
- Le template **n'embarque plus Swagger UI**. Utiliser `AddOpenApi()` avant `builder.Build()` et `MapOpenApi()` en développement.
- `global.json` évite qu'un autre SDK majeur installé soit sélectionné.
- HTTPS local : `dotnet dev-certs https --trust` avant de s'énerver sur le navigateur.
- API et front sur des **ports différents = origines différentes** → CORS à configurer, et les ports relevés doivent correspondre au `BaseAddress` du client.
- `Grpc.Tools` est un outil de build : `PrivateAssets="all"`.
- Côté `.csproj` : `GrpcServices="Server"` dans l'API, `"Client"` dans l'App.
- Vérifier une syntaxe sans projet : `dotnet run --file essai.cs`, fichier placé **hors** d'un dossier contenant un `.csproj`.
- `dotnet new <modèle> --help` liste les options réelles — à consulter avant de croire une IA sur une option.

---

## 15. Règles pour l'agent

- **Lire [CONTEXT.md](./CONTEXT.md) avant d'écrire du code.** Employer les termes du glossaire, jamais les mots écartés.
- Ne jamais violer les règles de dépendance de la § 4.
- Ne jamais proposer d'identité de joueur transmise par le client (§ 4).
- Ne jamais introduire de dépendance dans `BattleShip.Domain`.
- Écrire le test avant le code dans `Domain`.
- Après toute décision structurante : proposer l'entrée `PROMPTS.md` ou l'ADR
  correspondant, **courte**, dans le même commit.
- Vérifier une API dans la documentation .NET 10 ou par exécution avant de
  l'affirmer. Le support insiste : « le ton assuré du modèle ne démontre ni la
  pertinence ni la correction de la solution ».
- Ne pas déborder du périmètre : les features se font dans l'ordre de la § 10.
