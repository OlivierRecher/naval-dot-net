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

| Difficulté — **code livré**, 100 parties appariées | Tirs moyens |
|---|---|
| `Random` | 95,3 |
| `HuntTarget` | 64,6 |
| `HuntTargetParity` | 58,7 |

Précision d'environ ±1 tir : ces valeurs portent le classement, pas la décimale.
Dans `REVUE-IA.md` revue 4, un 64,6 désigne une **autre** variante mesurée sur un
**autre** échantillon — la table de référence y lève l'ambiguïté.

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

---

## 2026-09-15 — Traitement de la revue de la PR #4

**Outil / modèle** : Claude Code (Opus 5) · relecteurs : GitHub Copilot code review + une relecture adverse déléguée

**Contexte**
La PR #4 livrait les trois difficultés. Deux relectures indépendantes ont été
conduites sans se voir : Copilot sur le diff, et un agent adverse mandaté pour
trouver des défauts réels dans `HuntTargetBot` — bornes, cases rejouées,
diagonales, concurrence — avec interdiction de modifier le dépôt.

L'intérêt de l'indépendance est mesurable : **les deux ont trouvé le même défaut
principal**, ce qu'aucune des deux seule n'aurait établi, et **chacune en a
trouvé un que l'autre a manqué**.

**Prompt**
Traiter chaque remarque en séparant le défaut signalé du remède proposé. Ne rien
écrire dans un livrable noté sans l'avoir vérifié soi-même, y compris les mesures
rapportées par l'agent — une remarque produite par une IA n'a pas plus d'autorité
qu'une autre production d'IA.

**Réponse résumée**

| # | Source | Remarque | Traitement |
|---|---|---|---|
| 1 | Copilot ×4 **et** adverse | La PR ajoute `Level`/`Niveau` aux mots écartés de `CONTEXT.md` **et** les emploie ~20 fois, dont le message du 400 envoyé au client | Retenue |
| 2 | Copilot | `ANewGame_WithoutAnExplicitDifficulty_PlaysAtRandom` passe `BotDifficulty.Random` explicitement : il ne teste pas le défaut qu'il nomme | Retenue |
| 3 | Adverse | L'ADR affirme « la liaison réussit toujours » ; faux pour une valeur JSON non textuelle | Retenue, **vérifiée avant correction** |
| 4 | Adverse | L'endpoint jetait le booléen de `TryParse` : une difficulté non reconnue serait devenue `Random` en silence | Retenue, **corrigée autrement que suggéré** |
| 5 | Adverse | Le garde `Math.Max(…, 0)` de `Between` est une branche morte | Retenue |
| 6 | Adverse | La « limite assumée » de la résolution est le cas majoritaire, pas un cas de bord | Retenue, **vérifiée indépendamment** |
| 7 | Adverse | `Play.razor` utilise `First` là où `LabelOf` utilise `FirstOrDefault` | Retenue |
| 8 | Adverse | `damaged` inclut les cases `Sunk` alors que c'est redondant | **Écartée** |

**Décision** : 7 retenues, 1 écartée, 1 corrigée autrement que proposé.

Deux points méritent d'être détaillés.

1. **Le correctif proposé au n° 4 aurait contredit l'ADR 0006.** L'agent
   proposait un `if (!TryParse(...)) return Problem(400)` dans l'endpoint. Or
   l'ADR 0006 pose que la validation vit dans un filtre générique, « pas d'appel
   manuel répété dans chaque endpoint » : ajouter un contrôle manuel aurait
   défait la décision qu'il trace. Le diagnostic était juste — la dégradation
   silencieuse est le pire mode de défaillance — mais le remède devait venir
   d'ailleurs. Retenu à la place : `BotDifficulties.Parse`, qui **lève**. Si le
   filtre sautait, l'appel échoue bruyamment au lieu de servir un bot dégradé, et
   aucune validation n'est dupliquée.
2. **Le n° 8 est écarté.** L'argument est correct : une chaîne de cases touchées
   reliant une case à un navire coulé n'a jamais besoin de traverser une **autre**
   case coulée, celle-ci serait un point d'arrivée plus proche et valide. Mais
   `damaged` signifie « touchée ou coulée » ; restreindre à `Hit` optimiserait la
   couverture de mutation au prix de la lisibilité du prédicat. Une expression
   redondante dont la redondance est **démontrable** n'est pas un défaut.

**Vérification**

| Contrôle | Résultat |
|---|---|
| `dotnet build` puis `dotnet test` | 134 tests, 0 échec (130 avant la revue) |
| Affirmation n° 3, refaite par sondes HTTP | `"Expert"`, `""`, `null` → `ValidationProblem` ; `2`, `true`, `["HuntTarget"]` → `BadHttpRequestException`, en amont du filtre. **L'agent avait raison** |
| Affirmation n° 6, refaite avec une sonde posée sur le calcul du bot | **1018 / 2000 = 50,9 %** des parties contiennent au moins une case d'un navire à flot classée coulée — chiffre identique à celui rapporté |
| Nouveaux tests épinglant ces deux comportements | `CreateGame_WithANonTextualDifficulty_IsRefusedByTheBinderNotTheValidator`, `CreateGame_WithADifficultyInAnotherCase_StoresTheCanonicalName` |
| Vocabulaire après renommage | `grep -n "level\|Level\|Niveau"` sur les cinq projets : aucune occurrence |

**Portée du contrôle — ce qui n'est PAS vérifié**

- Le chemin `BotDifficulties.Parse` lève **uniquement si le filtre de validation
  disparaît**. Aucun test ne l'instancie : c'est une défense en profondeur, pas
  un comportement couvert.
- La mesure des 50,9 % a été obtenue avec une sonde **ajoutée puis retirée** du
  domaine. Elle n'est pas rejouée par `dotnet test` et demande de reposer la
  sonde pour être reproduite.
- L'agent adverse rapporte 11 mutations sur 13 détectées par la suite, les deux
  survivantes étant des branches mortes. Ce chiffre **n'a pas été refait** : il
  est cité comme une observation de l'agent, pas comme un résultat vérifié.

**Constat de méthode**
Deux relecteurs indépendants sur le même diff ne coûtent presque rien et ne se
recouvrent pas : Copilot lit le diff et compare le code à ce que la PR déclare —
c'est ainsi qu'il a vu que le glossaire ajouté était violé par le code ajouté, et
que le test du défaut ne testait pas le défaut. L'agent adverse exécute, mesure
et mute — c'est ainsi qu'il a trouvé une affirmation d'ADR contredite par une
sonde HTTP. Aucun des deux n'aurait produit les trouvailles de l'autre.

**Commits** : branche `feat/difficultes-de-bot`, PR #4.

---

## 2026-09-15 — Placement manuel de la flotte

**Outil / modèle** : Claude Code (Opus 5)

**Contexte**
Item 3 du backlog. Le placement manuel introduit une phase qui n'existait pas :
la partie existe, mais on ne peut pas y tirer. `CONTEXT.md` avait anticipé ce
troisième statut depuis le cadrage sans décider comment il serait porté.

Le risque identifié était l'**état intermédiaire**. Une flotte se pose en cinq
navires ; si le serveur accepte les navires un par un, une flotte abandonnée en
cours de route laisse une grille à moitié remplie, dans un état qu'aucune règle
du jeu ne décrit et qu'aucun test n'aurait de raison d'instancier.

**Prompt**
Livrer le placement manuel en TDD sur le domaine, le placement aléatoire restant
offert. Trancher explicitement deux questions avant de coder : comment la partie
sait qu'elle attend une flotte, et à quelle granularité la flotte est soumise.
Pour chaque règle ajoutée, exécuter la mutation qui la casse — et si une mutation
survit, ne pas ajuster le test après coup mais se demander d'abord si c'est le
code ou le test qui est de trop.

**Réponse résumée**

| Question | Décision |
|---|---|
| Comment la partie sait qu'elle attend | Le statut se **déduit des grilles** : une grille humaine vide *est* l'attente. Pas de drapeau, qui pourrait contredire les grilles |
| Granularité de la soumission | La **flotte entière** en une requête `PUT`, validée en bloc avant qu'aucune case ne soit posée |
| Qui dicte la composition | Le serveur : la `GameView` publie `FleetToPlace` — types et longueurs, **aucune position** |
| Contrat des valeurs nommées | `EnumNames<T>` généralise l'ADR 0009 et sert quatre énumérations ; `BotDifficulties` disparaît |

**Décision** : acceptée. Deux effets non recherchés méritent d'être notés.

1. **Le mode `Local` de l'item 4 est déjà servi.** `BoardAwaitingFleet()` rend la
   prochaine grille humaine vide ; avec deux humains, chaque `PUT` en remplit
   une et la partie démarre quand il n'en reste plus. Aucune ligne à ajouter.
2. **Trois mutations ont survécu au premier passage**, et elles ont changé le
   code plutôt que les tests. Voir ci-dessous.

**Vérification**

| Contrôle | Résultat |
|---|---|
| `dotnet build` puis `dotnet test` | 172 tests, 0 échec (134 avant l'item 3) |
| 9 mutations, chacune rétablie | 6 détectées d'emblée, **3 survivantes** — table complète dans l'ADR 0010 |
| Survivante 1 — garde `AwaitingFleet` de `FireFromClient` | `FireRules.Validate` refusait déjà : **code mort, supprimé** |
| Survivante 2 — contrôle de statut dans `PlaceFleetFromClient` | Redondant avec l'absence de grille en attente : **condition simplifiée** |
| Survivante 3 — garde « jamais la grille d'un bot » | Aucun test ne le distinguait de son absence : **test ajouté** |
| Mutations rejouées après correction | Les 9 sont détectées |
| Parcours navigateur | Partie manuelle créée, 5 navires posés, flotte validée, partie jouée jusqu'au tir et à la riposte |

**Portée du contrôle — ce qui n'est PAS vérifié**

- Le gabarit contrôlé est `FleetTemplate.Standard`, **en dur** dans
  `Game.PlaceFleetFromClient`. La partie connaît la taille de sa grille mais pas
  sa flotte ; l'item 6 devra la lui donner.
- Le contrôle local du navigateur — débordement et chevauchement — n'a **aucun
  test automatisé**. C'est un confort d'interface ; la garantie est le contrôle
  serveur, qui lui est testé.
- Aucun test ne couvre une flotte soumise **pendant** qu'une autre requête la
  soumet. Le verrou de l'agrégat sérialise les deux, mais rien ne l'atteste ici,
  contrairement aux tirs concurrents de l'item 1.

**Constat de méthode**
« Le test passe » et « le test protège » sont deux choses différentes, et la
seule façon connue de les distinguer est de casser la règle. Sur neuf mutations,
trois ont révélé du code ou des tests qui ne servaient à rien — soit un tiers.
Aucun de ces trois défauts n'était visible à la relecture : les deux gardes morts
paraissaient prudents, et le test tautologique portait le nom exact de
l'invariant qu'il ne protégeait pas.

**Commits** : branche `feat/placement-manuel`, PR #5.

---

## 2026-09-15 — Traitement de la revue de la PR #5

**Outil / modèle** : Claude Code (Opus 5) · relecteur : GitHub Copilot code review

**Contexte**
La PR #5 livrait le placement manuel. Copilot a déposé **deux** commentaires, et
tous deux portaient sur des choses que la PR **affirmait** plutôt que sur du code
manifestement faux — c'est ce qui rend cette revue instructive.

**Prompt**
Traiter chaque remarque en séparant le défaut signalé du remède. Vérifier
soi-même les affirmations avant de corriger. Pour tout test ajouté en réponse,
exécuter la mutation correspondante avant de le déclarer utile.

**Réponse résumée**

| # | Remarque | Traitement |
|---|---|---|
| 1 | En `Local`, `ViewForClient()` sert toujours le joueur courant : après le premier placement, le second joueur reçoit un écran sans rien à poser | Retenue — **l'ADR affirmait le contraire** |
| 2 | Aucun test ne lance deux `PlaceFleetFromClient` concurrents, alors que les tirs en ont | Retenue — **le premier correctif ne corrigeait rien** |

**Décision** : 2 retenues, dont 1 dont le correctif a dû être refait.

1. **La remarque n° 1 visait une phrase, pas une ligne de code.** L'état décrit
   est inatteignable aujourd'hui : `POST /games` ne crée que des parties `Solo`.
   Mais l'ADR 0010 et le corps de la PR affirmaient que « le mode `Local` est
   déjà servi, aucune ligne à ajouter ». C'était faux, et le relecteur l'a établi
   en lisant le code plutôt que l'affirmation. Corrigé aux deux endroits :
   `ViewForClient()` sert désormais, pendant `AwaitingFleet`, le joueur **dont on
   attend la flotte** ; l'ADR dit ce qui reste à faire à l'item 4.
2. **La remarque n° 2 a produit un test qui ne testait rien.** Voir
   `REVUE-IA.md`, revue 6.

**Vérification**

| Contrôle | Résultat |
|---|---|
| `dotnet build` puis `dotnet test` | 175 tests, 0 échec (172 avant la revue) |
| Mutation — `ViewForClient` ignore le joueur en attente | 1 test au rouge |
| Mutation — `PlaceFleetFromClient` sans verrou, **1ʳᵉ** écriture du test | **0 au rouge** — le test ne protégeait rien |
| Mutation — même mutation, test corrigé | 1 au rouge, le test nommé |
| Mesure de la course, 10 essais × 64 fils | avec verrou : 1 acceptation, 5 navires. Sans : 2 à 64 acceptations, jusqu'à **18** navires |

**Portée du contrôle — ce qui n'est PAS vérifié**

- Le mode `Local` n'est **pas livré**. L'agrégat sait le construire et la
  projection est correcte, mais aucun endpoint ne crée une partie `Local` et il
  n'existe pas d'écran de passation. C'est l'item 4.
- Le test de concurrence repose sur un `Thread.Sleep(20)` empirique. Il rend la
  course reproductible sur cette machine ; aucune propriété ne le fonde.
- Les tests de concurrence des tirs, écrits à l'item 1, n'ont **pas** été
  resoumis à ce contrôle.

**Constat de méthode**
Les deux remarques portaient sur l'écart entre ce que le projet **dit** et ce
qu'il **fait** : une affirmation d'ADR contredite par le code, et un test absent
là où la PR affirmait par ailleurs que le verrou protégeait la transition. Un
relecteur automatique lit le diff sans croire le texte qui l'accompagne — c'est
précisément là qu'il est le plus utile, et c'est un angle qu'une relecture par
l'auteur n'a pas.

**Commits** : branche `feat/placement-manuel`, PR #5.

---

## 2026-09-15 — Multijoueur local (hot-seat)

**Outil / modèle** : Claude Code (Opus 5)

**Contexte**
Item 4 du backlog. L'ADR 0007 avait écarté le multijoueur en ligne au profit du
hot-seat ; l'ADR 0003 pose que le serveur ne sert qu'une `GameView` à la fois.
Ces deux décisions se combinent si bien que la difficulté de l'item n'est pas
l'alternance — l'agrégat la faisait déjà — mais ceci : **dès le tir résolu, le
serveur sert la vue du joueur suivant, donc sa flotte.** Sur un appareil
partagé, celui qui vient de tirer regarde encore l'écran.

**Prompt**
Livrer le hot-seat. Commencer par écrire les tests de domaine **avant** toute
modification de production, pour établir par exécution ce que l'agrégat sait déjà
faire — plutôt que de l'affirmer, comme l'ADR 0010 l'avait fait à tort. Puis
traiter la seule vraie question : qu'est-ce qui empêche un joueur de voir la
flotte de l'autre, et qu'est-ce que cette protection ne protège pas.

**Réponse résumée**

| Sujet | Décision |
|---|---|
| Alternance | Aucune notion nouvelle : `FireLocked` échange les joueurs comme en `Solo` |
| Protection | Un écran de passation ; tant qu'il est affiché, l'interface ne rend **rien** de la vue |
| Portée de la protection | Elle protège d'un regard, **pas** d'un adversaire : la vue est déjà dans le navigateur |
| `opponentName` | Exigé **uniquement** en `Local`, par une règle conditionnelle `.When()` |
| `botDifficulty` | Exigé dans les deux modes — il a un défaut qui veut dire quelque chose, `opponentName` non |

**Décision** : acceptée. Le point qui compte est le troisième.

La passation ne peut pas être une protection réelle, et l'ADR le dit sans
détour : la vue du joueur suivant arrive dans le navigateur en réponse au tir,
avant que l'écran de passation ne s'affiche. Faire confirmer la passation au
serveur n'y changerait rien — c'est le même client non fiable qui confirmerait.
Sur un appareil partagé, les données des deux joueurs passent nécessairement par
le même navigateur.

**Vérification**

| Contrôle | Résultat |
|---|---|
| 6 tests de domaine écrits **avant** toute production | Verts sans modification — l'agrégat savait déjà jouer à deux humains |
| `dotnet build` puis `dotnet test` | 192 tests, 0 échec (175 avant l'item) |
| Invariant du hot-seat | `ASequenceOfShots_NeverServesTwoFleetsAtOnce` : 12 tirs, une seule flotte par réponse, jamais changeante — doublé d'un garde contre deux flottes identiques |
| 4 mutations, chacune rétablie | Chacune met au moins un test au rouge |
| Parcours navigateur | Création, tir, passation muette, confirmation, tour du second joueur avec sa propre flotte |

**Portée du contrôle — ce qui n'est PAS vérifié**

- Le parcours navigateur a demandé **quatre essais** : trois défauts d'interface
  qu'aucun des 192 tests ne couvrait. Voir `REVUE-IA.md`, revue 7.
- Le **placement manuel en hot-seat** — deux joueurs posant chacun leur flotte —
  est couvert côté serveur mais **n'a pas été joué à la main**. C'est la
  combinaison la moins éprouvée de la livraison.
- Aucune partie hot-seat n'a été menée jusqu'à la victoire dans le navigateur.
- Rien n'empêche un joueur de confirmer la passation à la place de l'autre.

**Constat de méthode**
Écrire les tests de domaine avant la production a produit un résultat qu'on
n'attendait pas : ils sont tous passés. C'est une information — l'item 3 avait
correctement généralisé — mais elle ne dit rien du travail restant, et l'avoir
lue comme un avancement a coûté trois allers-retours au navigateur.

**Commits** : branche `feat/hot-seat`, PR #6.

---

## 2026-09-15 — Traitement de la revue de la PR #6

**Outil / modèle** : Claude Code (Opus 5) · relecteur : GitHub Copilot code review

**Contexte**
Un seul commentaire, sur `GameSession.FireAsync` : si le rafraîchissement échoue
après un tir accepté, la passation n'est jamais armée et l'interface revient sur
la vue périmée du tireur.

**Prompt**
Vérifier la conséquence annoncée avant de corriger — un relecteur qui décrit
correctement un défaut peut en décrire incorrectement l'effet.

**Réponse résumée**

**Le diagnostic est juste, la conséquence annoncée est fausse, et la réalité est
pire.** Copilot écrit que « la partie reste bloquée sur `NotTheClientTurn` ».
Ce refus n'existe pas en hot-seat : il ne se déclenche que si le joueur courant
est un **bot**, et une partie `Local` n'en a aucun.

Ce qui se passe réellement : la vue périmée du tireur dit encore « à vous »,
donc `CanFire` redevient vrai. S'il retire, `Game.FireFromClient` accepte — le
serveur ne sait pas qui est au clavier, c'est précisément la décision de l'ADR
0003. **Le premier joueur joue le tour du second sans que rien ne le signale.**

La passation n'est donc pas un confort d'affichage de plus : c'est le **seul
garde du tour** en hot-seat. Elle ne peut pas dépendre d'un appel qui peut
échouer.

Correction : la passation est armée **dès le tir accepté**, avant le
rafraîchissement. Pour cela la `GameView` publie le **nom de l'adversaire** —
un nom, jamais une position, et que le joueur connaît déjà. Confirmer est refusé
tant que la vue suivante n'est pas arrivée : confirmer sur une vue périmée
afficherait la flotte du joueur *précédent*. L'écran propose de réessayer.

**Décision** : diagnostic retenu, conséquence corrigée, correctif étendu.

**Vérification**

| Contrôle | Résultat |
|---|---|
| `dotnet build` puis `dotnet test` | 195 tests, 0 échec (192 avant la revue) |
| Mutation — la vue nomme le viewer au lieu de l'adversaire | 2 tests domaine + 1 test API au rouge |
| Parcours navigateur après correction | Création, tir, passation, confirmation — inchangé |

**Portée du contrôle — ce qui n'est PAS vérifié**

- **Le chemin d'échec qui motive la correction n'a pas été déclenché.** Il
  faudrait couper le réseau entre deux appels consécutifs du navigateur. La
  correction est établie par lecture, pas par expérience.
- Aucun test ne couvre `GameSession` : c'est la zone que `AGENTS.md` § 8 laisse
  hors périmètre. Un `HttpMessageHandler` de test la rendrait accessible, et
  c'est exactement ce qu'il faudrait pour éprouver ce chemin.

**Constat de méthode**
Une remarque peut être juste sur le défaut et fausse sur ses conséquences.
Recopier la conséquence annoncée aurait produit un correctif correct et une
justification erronée — donc une ligne d'ADR indéfendable à l'oral. Le défaut
méritait d'être vérifié dans le domaine, pas seulement dans le fichier signalé.

**Commits** : branche `feat/hot-seat`, PR #6.

---

## 2026-09-15 — Persistance SQLite, historique et statistiques

**Outil / modèle** : Claude Code (Opus 5)

**Contexte**
Item 5 du backlog. Le premier à mettre à l'épreuve une promesse écrite au socle :
l'ADR 0004 annonçait que la bascule vers SQLite « ne toucherait aucun endpoint ».

Le risque identifié n'était pas d'échouer à écrire du EF Core, mais de persister
**trop** : `Game` porte deux grilles, des impacts, des navires coulés, un tour,
un statut, un vainqueur. Tout mapper aurait produit un schéma large dont chaque
colonne dérivée peut diverger de ce dont elle dérive.

**Prompt**
Livrer la persistance derrière `IGameRepository`, plus l'historique et les
statistiques. Décider d'abord **ce qui mérite d'être écrit**, en tenant compte de
ce que l'ADR 0002 annonçait à propos du journal en ajout seul. Puis compter
précisément ce que la bascule change, pour vérifier ou réfuter l'ADR 0004.

**Réponse résumée**

| Sujet | Décision |
|---|---|
| Ce qui est écrit | Les **placements** et le **journal ordonné**. Rien de dérivable |
| Ce qui est rejoué | Impacts, navires coulés, tirs reçus, tour courant, statut, vainqueur |
| Exception | Le **résultat** de chaque tir, dérivable mais stocké — pour les statistiques en SQL. Un test interdit qu'il diverge |
| Instances | Un cache Singleton publie une partie et une seule, sinon le verrou de l'ADR 0008 ne sérialise plus rien |
| Asynchronisme | `IGameRepository` reste **synchrone** : le rendre `async` aurait rendu chaque endpoint `async` |
| Historique | `IGameHistory`, séparée : elle interroge des colonnes, elle ne rejoue rien |

**Décision** : acceptée. La promesse de l'ADR 0004 est **partiellement réfutée** —
voir `REVUE-IA.md`, revue 8.

**Vérification**

| Contrôle | Résultat |
|---|---|
| `dotnet build` puis `dotnet test` | 217 tests, 0 échec (195 avant l'item) |
| Tests d'API | Sur un **vrai SQLite en mémoire**, un par classe : schéma, contraintes et SQL réellement exercés |
| Survie au redémarrage | Dépôt et cache neufs sur la même base : mêmes tirs, même vue, même flotte |
| Mutation sans `Save` | Le tir existe en mémoire, pas en base — c'est ce test qui donne son sens au point de validation |
| `ORDER BY` sur `DateTimeOffset` | **Refusé par SQLite** — découvert par exécution, pas par lecture. Dates stockées en ticks UTC |
| 7 mutations | **3 survivantes au premier passage**, toutes corrigées par des tests, aucune par du code |
| Parcours navigateur | Partie jouée, **API redémarrée**, partie retrouvée intacte ; page d'historique et statistiques |

**Portée du contrôle — ce qui n'est PAS vérifié**

- Le cache et le verrou ne couvrent **qu'un processus**. Deux instances de l'API
  sur la même base joueraient chacune sur sa copie. L'ADR 0008 l'annonçait ; cet
  item le confirme sans le corriger.
- `Save` n'a **ni transaction explicite ni verrou optimiste**.
- Le dépôt synchrone bloque un fil du pool sur chaque entrée/sortie. Sans
  conséquence mesurable ici, premier point à reprendre à une échelle réelle.
- Aucune migration : le schéma est créé au démarrage. Une évolution de schéma sur
  une base existante n'est donc pas couverte.

**Constat de méthode**
Trois des sept mutations ont survécu, et **aucune ne révélait un défaut du
code** : toutes trois révélaient un test qui ne protégeait rien. L'une d'elles
— les statistiques ignorant les navires coulés — passait parce que le test
recalculait l'attendu à partir de la réponse du serveur. C'est exactement le
défaut de la revue 2, reproduit quatre items plus loin, par la même personne qui
l'avait écrite.

**Commits** : branche `feat/persistance`, PR #7.

---

## 2026-09-15 — Traitement de la revue de la PR #7

**Outil / modèle** : Claude Code (Opus 5) · relecteur : GitHub Copilot code review

**Contexte**
Cinq commentaires sur la persistance. Contrairement aux revues précédentes,
aucun ne portait sur un écart entre le code et ce que la PR annonçait : tous
portaient sur du code.

**Réponse résumée**

| # | Remarque | Traitement |
|---|---|---|
| 1 | `Save` lit l'agrégat **hors de son verrou** | Retenue — la plus sérieuse |
| 2 | L'historique annonce `InProgress` pour une partie qui attend sa flotte | Retenue |
| 3 | Le cache ne relâche jamais les parties terminées | Retenue |
| 4 | La page d'historique appelle `HttpClient` directement, contre `AGENTS.md` § 6 | Retenue |
| 5 | Faute d'orthographe dans un commentaire | Retenue |

**Décision** : 5 retenues sur 5.

La première méritait sa place en tête. `Save` lisait `Status`, `CurrentPlayer`,
`Opponent` et énumérait `Board.Ships` sans prendre le verrou de `Game`. Or
l'échange de tour est une affectation de tuple — non atomique — et `Board.Ships`
rend la liste vivante. Une écriture concurrente d'un tir pouvait donc persister un
état mélangeant deux instants, ou lever une exception d'énumération.
`Game.Snapshot()` rend désormais une photographie prise sous le verrou.

La quatrième est un rappel à une règle que le projet s'était donnée et que sa
propre page violait : `AGENTS.md` § 6 impose que tous les appels réseau passent
par `GameSession`. La page d'historique avait son propre `HttpClient` et sa propre
gestion d'erreur — un second chemin réseau, avec un second endroit où « chargement,
succès, échec » devait être tenu à jour.

**Vérification**

| Contrôle | Résultat |
|---|---|
| `dotnet build` puis `dotnet test` | 221 tests, 0 échec (217 avant la revue) |
| Mutation — `Snapshot` sans verrou | **A d'abord survécu** : 200 tours ne suffisaient pas. À 20 000, le test attrape l'exception d'énumération |
| Mutation — sièges suivant le tour courant | **A d'abord survécu** : aucun test ne distinguait l'ordre d'ouverture de l'ordre du tour. Test ajouté |
| Mutation — partie terminée gardée en cache | 1 test au rouge |
| Mutation — historique ignorant l'attente de flotte | 1 test au rouge |
| Appels réseau hors `GameSession` | `grep` sur la page : aucun |

**Portée du contrôle — ce qui n'est PAS vérifié**

- Le test de cohérence sous concurrence **attrape l'occurrence** d'une lecture
  déchirée, il n'établit pas son absence. Il a fallu 20 000 tours pour la
  produire de façon fiable sur cette machine ; rien ne garantit qu'une machine
  plus lente la produirait.
- L'éviction du cache à la fin d'une partie n'est pas éprouvée **sous
  concurrence** : une lecture simultanée à l'éviction rechargerait la partie
  depuis SQLite, ce qui est correct, mais aucun test ne l'instancie.

**Constat de méthode**
Deux des quatre mutations posées après la revue ont survécu au premier passage,
pour deux raisons différentes : l'une parce que le test ne mettait pas assez de
pression, l'autre parce qu'aucun test ne distinguait deux notions que le code
distingue. La seconde est la plus instructive — le correctif introduisait une
distinction juste, et rien ne la protégeait.

**Commits** : branche `feat/persistance`, PR #7.

---

## 2026-09-15 — Personnalisation : taille de grille et composition de flotte

**Outil / modèle** : Claude Code (Opus 5)

**Contexte**
Item 6, dernier du backlog. Deux ADR précédents lui avaient laissé une dette
explicite : l'ADR 0010 notait que le gabarit de flotte était en dur dans
`Game.PlaceFleetFromClient`, et l'ADR 0009 annonçait que le damier du bot
`Vétéran` deviendrait **incorrect** — pas seulement moins bon — le jour où un
navire d'une seule case existerait.

**Prompt**
Livrer la personnalisation de la flotte, la taille de grille étant déjà
paramétrable depuis le socle. Commencer par la dette de l'ADR 0009 : rendre le
cas d'un navire d'une case atteignable, vérifier que la limite annoncée est
réelle, puis corriger. Trancher ensuite ce qu'une composition doit respecter
pour être acceptée — et justifier chaque borne plutôt que de la choisir.

**Réponse résumée**

| Sujet | Décision |
|---|---|
| Où vit la composition | Sur `Game`, comme le mode et la difficulté. Fixée à la création, persistée avec la partie |
| Ce qu'elle doit respecter | Non vide, ≤ 15 navires, aucun navire plus long que la grille, ≤ 1/3 de la grille occupé |
| Le plafond de densité | **Mesuré, pas choisi** : la flotte la plus dense acceptée se place sur 200 graines sans un échec |
| Le damier du bot | Le pas suit le **plus petit navire** de la flotte — généralisation de la règle d'origine |
| Longueurs | Restent attachées aux types : on choisit combien de navires de quels types |

**Décision** : acceptée.

Le point qui compte est le damier. La formulation d'origine — « un navire de deux
cases croise une case sur deux » — n'était que l'instance L = 2 de « un navire de
longueur L croise une maille de pas L ». La correction n'est donc pas un cas
particulier ajouté mais une règle générale écrite ; la flotte classique retrouve
le damier d'origine sans qu'on l'y force. Voir `REVUE-IA.md`, revue 9.

Le plafond de densité mérite aussi d'être défendu : il refuse des flottes qui
*pourraient* tenir. La raison est que le placement aléatoire échoue de façon
**aléatoire** au-delà d'une certaine densité — le même joueur verrait la même
composition acceptée puis refusée. Le plafond échange une acceptation aléatoire
contre un refus prévisible, et sa valeur est établie par exécution.

**Vérification**

| Contrôle | Résultat |
|---|---|
| `dotnet build` puis `dotnet test` | 246 tests, 0 échec (221 avant l'item) |
| Damier, flotte classique | Une case sur deux, 100 graines |
| Damier, navire d'une case | Couvre toute la grille — la limite de l'ADR 0009 est soldée |
| Damier, plus petit navire de 3, 4, 5 | Pas 3, 4, 5 |
| Plafond de densité | Flotte la plus dense acceptée, placée sur 200 graines sans un échec |
| Placement manuel | Contrôle la composition **de la partie** — dette de l'ADR 0010 |
| Flotte personnalisée après redémarrage | Retrouvée à l'identique |
| Parcours navigateur | Flotte réduite à un croiseur, un torpilleur et une vedette ; garde de densité déclenché à 37 % ; partie jouée |

**Portée du contrôle — ce qui n'est PAS vérifié**

- Le **coût** du damier n'a pas été remesuré par composition. On sait que le pas
  est correct pour chaque flotte ; on ne sait pas si `HuntTargetParity` reste
  meilleur que `HuntTarget` hors flotte classique. Les tests de classement de
  l'item 2 ne portent que sur la flotte standard.
- Le plafond d'un tiers est établi sur la plus petite grille autorisée. Il est
  plus conservateur sur les grandes.
- Rien n'empêche une flotte de ne contenir que des vedettes, ce qui réduit le
  niveau `Vétéran` au niveau `Chasseur` sans que l'interface le dise.

**Constat de méthode**
Deux des six mutations de cet item n'étaient d'abord détectées **que** par les
tests de domaine. Côté API, une flotte impossible finit aussi en 400 — par échec
du placement aléatoire, pas par la validation. Le statut seul ne distinguait donc
pas le refus déterministe du refus par hasard, alors que c'est précisément la
différence que la règle introduit. Le test d'API vérifie désormais la **forme** de
la réponse.

**Commits** : branche `feat/personnalisation`, PR #8.

---

## 2026-09-15 — Traitement de la revue de la PR #8

**Outil / modèle** : Claude Code (Opus 5) · relecteur : GitHub Copilot code review

**Contexte**
Huit commentaires sur le dernier item. Trois portaient sur des **tests qui ne
testaient pas ce qu'ils annonçaient**, ce qui est devenu le motif récurrent de ce
projet.

**Réponse résumée**

| # | Remarque | Traitement |
|---|---|---|
| 1 | `EnsureCreated` ne modifie pas un schéma existant : la colonne `Fleet` casse toute base antérieure | Retenue — la plus sérieuse |
| 2 | Le test de densité ne construit pas la flotte la plus dense acceptée | Retenue |
| 3 | Le test du damier à une case n'exclut pas un pas de 3 | Retenue |
| 4 | Le README ne suit pas l'item 6 | Retenue |
| 5 | Les compteurs de flotte n'ont pas de nom accessible | Retenue |
| 6 | La classe de test s'appelle encore `ScanLattice`, le domaine dit `ScanStride` | Retenue |
| 7 | Le glossaire dit qu'un navire occupe « plusieurs cases », or la vedette en occupe une | Retenue |

**Décision** : 8 retenues sur 8.

La première **falsifie une affirmation de l'ADR 0012** — « le périmètre ne
comporte aucune évolution de schéma à rejouer » — un item après qu'elle a été
écrite. Voir `REVUE-IA.md`, revue 10.

La septième est la plus révélatrice du fonctionnement de ce dépôt : en ajoutant
la vedette, l'item a rendu **fausse une phrase du glossaire** qu'il n'avait pas
touchée. Aucun test ne pouvait le voir ; un relecteur qui lit le diff contre la
documentation, si.

**Vérification**

| Contrôle | Résultat |
|---|---|
| `dotnet build` puis `dotnet test` | 248 tests, 0 échec (246 avant la revue) |
| Défaut n° 1 reproduit | `ALTER TABLE Games DROP COLUMN Fleet` puis écriture → « table Games has no column named Fleet » |
| Mutation — mise à niveau de schéma retirée | 1 test au rouge |
| Mutation — pas du damier forcé à 3 | 2 tests au rouge — ce que l'ancien test ne détectait pas |
| Mutation — plafond de densité relevé | 1 test au rouge |
| Compatibilité amont | Une partie écrite sans composition se relit avec la flotte classique |

**Portée du contrôle — ce qui n'est PAS vérifié**

- La mise à niveau ne sait qu'**ajouter des colonnes**. Seule l'ADR 0013 dit que
  toute autre évolution demandera des migrations ; rien dans le code ne l'empêche.
- Le test reconstitue l'ancien schéma en **supprimant** une colonne d'une base
  neuve, ce qui n'est pas exactement une base produite par la version précédente.
- L'accessibilité n'est vérifiée que par la présence des `aria-label` : aucun
  lecteur d'écran n'a été essayé.

**Constat de méthode**
Trois des huit remarques portaient sur des tests, et aucune sur un défaut du code
testé. Le motif est constant depuis la revue 2 : ce qui échappe le plus
facilement, ce n'est pas le code, c'est **l'écart entre ce qu'un test affirme
protéger et ce qu'il protège réellement**. Un test porte un nom, une
documentation, une intention — et rien de tout cela n'est exécuté.

**Commits** : branche `feat/personnalisation`, PR #8.

---

## 2026-09-15 — Rendre l'erreur gRPC-Web démontrable depuis l'interface

**Outil / modèle** : Claude Code (Opus 5)

**Contexte**
Le backlog était terminé. En reprenant la checklist de remise d'`AGENTS.md` § 13,
une **contrainte imposée** restait ouverte — § 2, « gRPC-Web fonctionnel sur au
moins un échange, avec une **erreur attendue démontrable** » — et le README
l'admettait lui-même :

> L'erreur gRPC-Web attendue n'est pas déclenchable depuis l'interface : l'écran
> désactive les cases déjà visées, donc le `FailedPrecondition` ne se démontre que
> par les tests d'intégration ou par un appel direct.

Une limite honnêtement écrite, mais une ligne de checklist non cochée. « Ça se
voit dans les tests » est une réponse faible devant un correcteur qui demande une
démonstration.

**Prompt**
Fermer cette contrainte. L'erreur doit être déclenchable depuis le navigateur,
sans affaiblir la protection qui la rendait inatteignable — les cases déjà visées
restent désactivées.

**Réponse résumée**
Un bouton « Démontrer l'erreur gRPC-Web », sous les grilles, rejoue
volontairement le premier tir du journal. Il **contourne l'interface, pas le
serveur** : le clic normal reste impossible, et c'est bien le serveur qui refuse.

L'écran affiche le statut et le message reçus, puis relit l'état et vérifie que
le tour n'a **pas** été consommé — la règle d'`AGENTS.md` § 3 vue depuis le
transport binaire.

**Décision** : acceptée.

**Vérification**

| Contrôle | Résultat |
|---|---|
| `CI=true dotnet build` puis `dotnet test` | 248 tests, 0 échec, 0 avertissement |
| Navigateur | « gRPC-Web a refusé le tir en A3 — statut **FailedPrecondition** : « Cette case a déjà été visée ; le tour n'est pas consommé. » Le tour n'a pas été consommé : toujours 1 tir(s) au journal. » |

**Portée du contrôle — ce qui n'est PAS vérifié**

- La commande n'a **aucun test automatisé**, comme tout le front. Le refus
  serveur qu'elle déclenche, lui, est couvert par `BattleGrpcServiceTests`.
- Elle ne démontre qu'une des trois erreurs annoncées par l'ADR 0005.
  `InvalidArgument` et `NotFound` restent couverts par les tests et `api.http`,
  sans affordance dans l'interface.
- Le message affiché vient du serveur. Si celui-ci changeait de formulation,
  l'écran le refléterait sans que rien ne le signale.

**Constat de méthode**
Cette contrainte n'a pas été trouvée en relisant le code mais en relisant la
**checklist de remise**, une fois le backlog fini. Une limite écrite dans le
README avait tenu lieu de solution : elle décrivait honnêtement un manque, et
cette honnêteté avait suffi à le rendre confortable.

**Commits** : branche `feat/demonstration-grpc`, PR #9.
