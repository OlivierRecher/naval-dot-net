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
