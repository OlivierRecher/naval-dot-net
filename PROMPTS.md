# PROMPTS.md

Journal des échanges IA **décisifs** du projet — pas l'intégralité des
conversations. Une entrée par échange ayant pesé sur une décision, avec la
vérification qui l'étaye.

Binôme : Olivier Recher (@OlivierRecher) · Ulysse (@Oulssyyy)

---

## 2026-09-15 — Cadrage initial du projet

**Outil / modèle** : Claude Code (Opus 5)

**Contexte**
Démarrage du TP. Dépôt vide : un `.gitignore` .NET, un `README.md` réduit à son
titre, un seul commit. Aucune solution créée, aucune décision prise.

Le support de cours laisse presque tout ouvert — règles du jeu, organisation du
code, stockage, algorithme du bot, interface, extensions — et annonce que
l'ambition du périmètre est notée. Le risque identifié était de coder d'abord et
de découvrir les contraintes imposées en cours de route, ou de subir une
architecture choisie par défaut plutôt que décidée.

**Prompt**
Lire l'intégralité du support (64 diapos), en extraire le travail à faire, les
contraintes, les attentes et les rendus finaux, puis cadrer le projet avant
d'écrire la moindre ligne de code. Rédiger le cadrage dans `CLAUDE.md` et
`AGENTS.md`. Conduit sous forme de questionnement contradictoire plutôt que de
génération directe.

**Réponse résumée**
Extraction du texte des 64 diapos du `.pptx`, puis trois séries de questions
(21 au total), chacune accompagnée d'une recommandation argumentée. Chaque série
ne posait que les questions dont les prérequis étaient déjà tranchés.

Décisions prises par le binôme :

| Sujet | Décision |
|---|---|
| Langue | Code en anglais, livrables `.md` en français |
| Git | Branches + PR relue par l'autre membre |
| Périmètre | Multijoueur **local** (hot-seat) à la place du multijoueur en ligne |
| Règles | Grille 10×10, flotte classique, contact autorisé, le tour passe toujours |
| Projets | 5 projets : `Domain` séparé de `Models` |
| Modèle | Agrégat mutable + journal de tirs en ajout seul |
| État serveur | Serveur autoritaire, **aucune identité de joueur transmise par le client** |
| Transport | `Fire` en gRPC-Web, reste du contrat en HTTP/JSON |
| Validation | Filtre d'endpoint + intercepteur gRPC, au lieu de l'appel manuel montré par le support |
| Tests | TDD strict sur `Domain`, tests d'intégration après coup sur l'API |

**Décision** : acceptée, après adaptation sur trois points.

Le questionnement a effectivement changé des choix, ce qui n'aurait pas eu lieu
avec une génération directe :

1. **Multijoueur local au lieu d'en ligne.** La proposition initiale plaçait le
   multijoueur en ligne en priorité 3. Écarté : trois extensions finies valent
   mieux qu'une infrastructure temps réel bancale, et le support exige que
   chaque fonctionnalité livrée soit « intégrée, vérifiée et comprise ».
2. **Conflit détecté entre deux exigences du support.** Le hot-seat met deux
   joueurs devant un seul écran, alors que la diapo 36 impose de « garder
   secrètes les positions adverses non découvertes ». Résolu en supprimant le
   paramètre : le client ne transmet aucune identité, le serveur renvoie
   toujours la vue du joueur dont c'est le tour. L'invariant devient
   structurellement invérifiable par le client, au lieu d'être simplement
   contrôlé.
3. **Écart assumé au support sur la validation.** La diapo 34 montre un appel
   manuel à `ValidateAsync` dans chaque endpoint. Remplacé par un filtre
   générique : réponse uniforme et impossibilité d'oublier un endpoint. L'écart
   fera l'objet d'un ADR plutôt que d'être passé sous silence.

**Vérification**

| Contrôle | Résultat |
|---|---|
| `dotnet --version` | `10.0.400` — conforme à l'exigence .NET 10 LTS |
| Recherche du dossier « Ressources Bataille Navale » dans `~/Documents`, `~/Downloads`, `~/Desktop` | **Absent** — les gabarits ont été reconstitués depuis le texte des diapos 56 à 58 |
| Relecture des contraintes imposées (diapo 6) contre le cadrage produit | Les 10 contraintes figurent en checklist dans `AGENTS.md` § 2 |
| État du dépôt (`git log`, `git remote`) | 1 commit, remote `OlivierRecher/naval-dot-net` déjà configuré |

**Portée du contrôle — ce qui n'est PAS vérifié**
Le contrat d'API, le découpage en 5 projets et le choix de `Fire` en gRPC-Web
sont des décisions **cohérentes avec le support mais non exécutées**. Rien n'a
encore compilé. Elles seront confirmées ou révisées au premier jet de code, et
les ADR concernés devront être mis à jour si elles ne tiennent pas.

---

## 2026-09-15 — Traitement de la revue automatisée de la PR #1

**Outil / modèle** : Claude Code (Opus 5) · relecteur : GitHub Copilot code review

**Contexte**
La PR #1 livrait le socle : moteur en TDD, Minimal API, tir en gRPC-Web,
interface Blazor. Copilot y avait déposé 8 commentaires en ligne et un verdict
« Changes recommended ».

Le risque identifié n'était pas de rater une correction, mais l'inverse :
appliquer les huit suggestions telles quelles. Un relecteur automatique produit
des remarques au ton également assuré, qu'elles soient justes ou non — et
`CLAUDE.md` pose que « le ton assuré du modèle ne démontre ni la pertinence ni
la correction de la solution ». Accepter en bloc aurait été le même défaut de
méthode que refuser en bloc.

**Prompt**
Traiter les commentaires de la PR #1 un par un : pour chacun, établir d'abord
s'il décrit un défaut réel, puis si le correctif proposé est le bon — les deux
questions sont distinctes. Corriger ce qui doit l'être, justifier ce qui est
écarté par une expérience et non par un avis. Répondre sur chaque fil et le
résoudre.

**Réponse résumée**

| # | Remarque | Traitement |
|---|---|---|
| 1 | `POST /shots` jouable alors que c'est au bot | Retenue |
| 2 | `ConcurrentDictionary` ne protège pas le `Game` | Retenue, **corrigée ailleurs** |
| 3 | Même défaut côté gRPC | Retenue |
| 4 | Course sur la transition de tir | Retenue |
| 5 | Échec de `/bot-turn` avalé côté Blazor | Retenue |
| 6 | `lang="en"` sur une interface française | Retenue |
| 7 | Assertion de confidentialité tautologique | **Diagnostic retenu, correctif rejeté** |
| 8 | Commentaire de `GameView` périmé | Retenue |

Les remarques 1 à 4 décrivaient le même défaut vu de quatre fichiers : le
serveur n'était autoritaire ni sur la propriété du tour, ni sur l'atomicité de
la transition. Elles ont donc été traitées par une seule correction, dans
l'agrégat — seul point que les transports HTTP et gRPC-Web traversent tous deux.

**Décision** : 7 acceptées, 1 adaptée après réfutation du correctif proposé.

Deux points ont été corrigés autrement que suggéré :

1. **Le verrou n'est pas allé dans le repository** mais dans `Game`. Le
   repository ne voit pas les transitions ; il rendrait un objet déjà
   déverrouillé. Un verrou par partie laisse en outre deux parties distinctes
   progresser en parallèle.
2. **Le correctif du test de confidentialité était faux** — voir `REVUE-IA.md`,
   revue 2.

Effet de bord qu'aucune remarque ne demandait mais que la correction a rendu
nécessaire : le choix de la cible du bot est entré dans l'agrégat. Tant qu'il
vivait dans l'endpoint, « vérifier que c'est au bot de jouer » et « tirer »
restaient deux opérations séparées, donc deux appels concurrents à `/bot-turn`
pouvaient jouer deux tours. Verrouiller `Fire` seul n'aurait pas suffi.

**Vérification**

| Contrôle | Résultat |
|---|---|
| `dotnet build` puis `dotnet test` avec `CI=true` | 87 tests, 0 échec, 0 avertissement |
| Mutation — garde du tour client retiré | 3 tests au rouge (domaine, HTTP, gRPC) |
| Mutation — garde de `/bot-turn` retiré | 2 tests au rouge |
| Mutation — `lock (_gate)` → `if (true)` | 1 test au rouge : *« non-concurrent collections must have exclusive access »* |
| Mutation — `ViewForClient()` sert le joueur courant | 2 tests au rouge |
| Réfutation du correctif proposé en n° 7 | 5 coordonnées partagées par les deux flottes sur la graine du test |

**Portée du contrôle — ce qui n'est PAS vérifié**

- Le garde « le client ne joue pas le tour du bot » porte sur `FireFromClient`.
  `Game.Fire` reste public et sans garde : c'est le primitif dont les tests du
  domaine ont besoin pour piloter les deux camps. Un futur endpoint qui
  l'appellerait directement contournerait le contrôle sans qu'aucun test ne le
  signale.
- Les deux tests de concurrence **n'établissent pas l'absence de course** : ils
  en attrapent l'occurrence. Ils échouent vite quand le verrou saute, ils ne
  passent jamais à tort.
- La réponse HTTP est assemblée après la fin du verrou. La transition est
  atomique, le corps de réponse peut refléter un état légèrement postérieur.
- La correction côté Blazor (remarque 5) n'a **aucun test automatisé** : il
  faudrait un `HttpMessageHandler` de test, absent du projet. Vérifiée à la main.

**Constat de méthode**
La suite de tests enchaînait toujours `/shots` puis `/bot-turn`. Aucun test ne
visitait donc le chemin « deux tirs consécutifs », qui était précisément le
défaut. Le trou était dans le plan de test avant d'être dans le code : une suite
verte ne dit rien des scénarios qu'elle n'instancie pas.

**Commits** : `cd4c4dd` (code et tests), PR #1.

---

## 2026-09-15 — Niveaux de bot : trois difficultés, et la preuve qu'elles se classent

**Outil / modèle** : Claude Code (Opus 5)

**Contexte**
Item 2 du backlog. Le socle ne connaissait qu'un `RandomBot`, enregistré en
Singleton sous `IBotStrategy` et injecté tel quel dans `/games/{id}/bot-turn`.
Ce montage ne survit pas à trois difficultés : un Singleton unique signifie que
toutes les parties en cours partagent le même adversaire.

Le risque identifié n'était pas d'écrire trois algorithmes — c'est de la
littérature, la bataille navale est un problème résolu. Il était de livrer trois
classes différentes dont **rien n'établirait qu'elles sont d'inégale force**.
Une hiérarchie de difficulté qu'aucune mesure n'appuie est une étiquette, pas une
fonctionnalité, et le support sanctionne ce qui est livré sans être vérifié.

**Prompt**
Implémenter les trois difficultés `Random`, `HuntTarget` et `HuntTargetParity` en
TDD sur le domaine. Poser d'abord la question de mesure : par quel indicateur
vérifie-t-on qu'une difficulté est réellement supérieure à une autre, et quel
test échoue si elle cesse de l'être ? Décider ensuite où vit la difficulté et qui
la résout en stratégie. Vérifier par exécution, pas de mémoire, tout ce qui
touche au comportement des API .NET.

**Réponse résumée**

L'indicateur retenu est le **nombre de tirs nécessaires pour couler une flotte
complète**, mesuré par un banc `BotDrill` qui fait tirer une stratégie sur une
grille sans passer par `Game` — donc sans l'alternance des tours ni la course
entre les deux joueurs, qui brouilleraient la mesure.

| Difficulté | Tirs moyens (100 parties appariées) |
|---|---|
| `Random` | 95,3 |
| `HuntTarget` | 64,6 |
| `HuntTargetParity` | 58,7 |

Décisions structurantes, toutes tracées dans l'ADR 0009 :

| Sujet | Décision |
|---|---|
| Où vit la difficulté | Sur `Game`, fixée à la création — pas dans le dépôt, pas transmise par le client |
| Qui la résout | `IBotStrategyFactory`, Singleton ; légitime car les stratégies sont des fonctions pures de la `GameView` |
| Type dans le DTO | `string`, pas l'énumération — sinon la désérialisation échoue avant le filtre de validation (ADR 0006) |
| Validation | Comparaison à la liste des noms, **pas** `Enum.TryParse` |
| Catalogue des libellés | Dans `Models`, avec un test qui interdit la divergence avec l'énumération du domaine |

**Décision** : acceptée, après deux corrections en cours de route.

1. **Dérive de vocabulaire.** Le code a été écrit `BotLevel` alors que
   `CONTEXT.md` fixe `BotDifficulty`. `CLAUDE.md` demande explicitement de lire
   le glossaire *avant* d'écrire du code ; ça n'a pas été fait. Renommé partout,
   et `CONTEXT.md` a gagné les trois termes que l'implémentation a créés —
   chasse, traque, damier — plus deux lignes de « mots écartés ». Le filtre de
   balayage a été renommé `IsOnScanLattice` pour porter le nom du glossaire.
2. **Une conclusion plus large que la mesure.** Voir `REVUE-IA.md`, revue 4.

**Vérification**

| Contrôle | Résultat |
|---|---|
| `dotnet build` puis `dotnet test` | 130 tests, 0 échec (87 avant la feature) |
| Comportement de `Enum.TryParse` sur une valeur hors énumération | `dotnet run --file` : `TryParse("42")` → **True**, `IsDefined` → False ; `TryParse("0")` → Random |
| 8 mutations, chacune rétablie | Chacune met au moins un test au rouge — table détaillée dans l'ADR 0009 |
| Écarts entre variantes d'un même algorithme | 4 000 parties appariées, erreur type des différences — voir revue 4 |
| Partie complète contre chaque difficulté | `ASoloGame_AgainstEveryDifficulty_RunsToAWinner`, 3 cas |
| Le niveau choisi pilote réellement le tour du bot | Fabrique espionne substituée dans l'hôte de test |
| Parcours navigateur complet | Préflight CORS 204 → `POST /battleship.Battle/Fire` 200 en gRPC-Web, puis `/bot-turn` et `GET /games/{id}` en HTTP/JSON |
| Traque observée à l'écran | Le bot `Vétéran` coule un `Battleship` par 4 tirs verticalement consécutifs, puis tire en **J10** — il retourne chasser dès le navire résolu, et J10 est sur le damier : (9 + 9) % 2 = 0 |

**Portée du contrôle — ce qui n'est PAS vérifié**

- Le damier de `HuntTargetParity` ne peut rien manquer **tant que le plus petit
  navire occupe deux cases**. Aucun test ne protège cette hypothèse : elle
  tombera avec la flotte personnalisable (item 6), et le niveau deviendra alors
  incorrect, pas seulement moins bon.
- La résolution des navires coulés est **approchée** : le contact entre navires
  étant autorisé, deux navires alignés et mitoyens se confondent. Aucun test ne
  construit ce cas — il est décrit, pas couvert.
- Les bornes d'efficacité sont larges (±10 tirs). Elles détectent l'effondrement
  d'une difficulté, pas sa dégradation lente.
- Le sélecteur de difficulté côté Blazor n'a **aucun test automatisé**, comme le
  reste du front : vérifié à la main dans le navigateur.

**Commits** : branche `feat/difficultes-de-bot`, PR #4.
