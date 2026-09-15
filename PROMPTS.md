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
