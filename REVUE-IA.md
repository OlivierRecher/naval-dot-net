# REVUE-IA.md

Revues argumentées de propositions produites par l'IA. Trois minimum sont
attendues, dont au moins une adaptée ou rejetée : trois acceptations
n'établiraient rien.

Chaque revue nomme ce qui devait être vrai, l'expérience choisie pour le mettre
en défaut, ce qui a réellement été observé, et ce qui reste non vérifié.

Binôme : Olivier Recher (@OlivierRecher) · Ulysse (@Oulssyyy)

**État : 4 revues — 2 adaptées, 1 correctif rejeté, 1 conclusion invalidée.**

---

## Revue 1 — La vue servie au navigateur peut-elle divulguer la flotte du bot ?

**Proposition examinée**

L'ADR 0003, produit par l'IA pendant la séance de cadrage, résout le secret des
positions en supprimant tout paramètre d'identité : *« le serveur renvoie
toujours la vue du joueur dont c'est le tour »*. L'argument avancé était fort —
sans paramètre, il n'y a plus de contrôle à oublier, donc l'invariant devient
structurellement invulnérable.

Cette règle a été reprise telle quelle dans `AGENTS.md` § 4 et a orienté tout le
contrat d'API avant qu'une ligne de code ne soit écrite.

**Hypothèse à vérifier**

Pour que la proposition soit acceptable, il faut que :

> quel que soit l'état de la partie, la `GameView` transmise au navigateur ne
> contienne aucune information que le joueur humain n'a pas le droit de connaître.

**Expérience**

Scénario minimal : une partie en mode `Solo`, l'humain tire une fois, puis on
demande l'état de la partie.

Résultat attendu, écrit avant exécution : la vue décrit l'humain, `IsViewerTurn`
vaut `false` puisque c'est au bot de jouer, et `OwnFleet` contient la flotte de
l'humain.

Erreur que ce contrôle peut détecter : toute implémentation qui projette la vue
d'après le joueur courant sans distinguer humain et bot. Si la règle de l'ADR est
appliquée littéralement, la vue renvoyée après le tir est celle du **bot** — donc
`OwnFleet` contient la flotte du bot, et le navigateur la reçoit.

**Observation**

Le défaut a été trouvé **par inspection, pas par un test** : il est apparu en
concevant le DTO de réponse au tir, au moment de décider quelle vue y attacher.
C'est une distinction qui compte — aucun test existant ne l'aurait signalé,
puisqu'aucun ne portait sur ce cas.

Le test a été écrit ensuite, et la mutation exécutée pour établir qu'il détecte
bien le défaut :

```
Mutation : ViewForClient() => ViewFor(_current)   // la règle de l'ADR, littérale
Résultat : 1 échec / 50
  Échoué ViewForClient_OnceTheBotIsToPlay_StillDescribesTheHuman
Restauration : 50 / 50
```

La règle de l'ADR était donc **fausse en mode solo**, et elle produisait
exactement la fuite qu'elle prétendait rendre impossible.

**Décision et justification**

**Adaptée.** La décision de fond — aucun identifiant de joueur transmis par le
client — est conservée : elle reste la bonne, et elle supprime réellement une
classe d'erreurs. C'est sa formulation qui était incomplète.

La règle exacte retenue est : **le serveur ne sert jamais la vue d'un bot.**
`ViewForClient()` suit le joueur courant s'il est humain, et bascule sur
l'adversaire humain sinon. En mode `Local` les deux joueurs étant humains, le
comportement d'origine est inchangé.

L'ADR 0003 conserve sa décision initiale et gagne une section exposant l'angle
mort et sa correction. Réécrire l'original aurait effacé l'information la plus
utile : l'écart entre ce qui a été décidé sur le papier et ce que
l'implémentation a révélé.

**Preuves et limites**

| | |
|---|---|
| Correction | commit `a44e064` — `BattleShip.Domain/Game.cs`, `ViewForClient()` |
| Tests | `BattleShip.Tests/Domain/GameViewForClientTests.cs`, 4 cas |
| Frontière HTTP | `GetGame_AfterTheHumanFired_StillDescribesTheHuman` |
| ADR corrigé | commit `112a96f` |

Ce qui **reste non vérifié** :

- La garantie porte sur la frontière HTTP — aucun endpoint n'accepte d'identité
  de joueur — et **non** sur le type `Game`, dont `ViewFor(Player)` reste
  accessible à tout code serveur. Un futur endpoint pourrait l'appeler avec le
  mauvais joueur sans qu'aucun test actuel ne le détecte.
- Les tests couvrent les modes `Solo` et `Local` avec deux joueurs. Une partie
  opposant deux bots n'est pas couverte, parce qu'elle n'existe pas au périmètre.
- Aucun contrôle ne vérifie que la sérialisation JSON n'ajoute pas de champ :
  la garantie repose sur la forme du type `GameViewResponse`, qui ne porte aucun
  membre capable de transporter la flotte adverse.

**Ce que cette revue enseigne pour la suite du projet**

Une décision d'architecture formulée en langage naturel peut être convaincante,
cohérente avec toutes les contraintes du sujet, et néanmoins fausse sur un cas
que personne n'a encore instancié. Ici le mot fautif était « joueur » : le
glossaire le définissait comme humain **ou** bot, et la règle de l'ADR employait
le terme comme s'il ne désignait qu'un humain.

`CONTEXT.md` a été précisé en conséquence — voir l'entrée `ViewForClient`.

---

## Revue 2 — Un relecteur automatique peut-il prescrire un correctif faux à partir d'un diagnostic juste ?

**Proposition examinée**

Revue Copilot de la PR #1, commentaire sur
`BattleShip.Tests/Domain/GameViewForClientTests.cs:52` :

> This assertion is tautological: `botCells.Except(ownCells)` removes every cell
> that could intersect `ownCells`, so it passes even if the bot fleet is present
> in `ownCells`. The test therefore does not protect the confidentiality
> invariant; **assert directly that the two cell sets do not intersect.**

La remarque tient en deux affirmations distinctes, et c'est cette distinction qui
fait l'objet de la revue : un **diagnostic** — l'assertion ne teste rien — et une
**prescription** — la remplacer par une assertion de disjonction.

**Hypothèse à vérifier**

Le diagnostic est vérifiable par lecture :
`ownCells.Intersect(botCells.Except(ownCells))` est vide par construction, quel
que soit le contenu des ensembles. Il n'appelait pas d'expérience.

La prescription, elle, suppose quelque chose que le commentaire n'énonce pas :

> les cases occupées par la flotte de l'humain et celles occupées par la flotte
> du bot sont disjointes.

C'est cette hypothèse-là qui a été mise à l'épreuve.

**Expérience**

Écrire l'assertion exactement telle que proposée, l'exécuter sur le scénario du
test — partie solo, graine `Random(17)`, flotte standard sur grille 10×10 — et
afficher les cases communes.

Résultat attendu, écrit avant exécution : **si les deux flottes partagent au
moins une case, la prescription est fausse**, car elle ferait échouer le test sur
du code correct.

Erreur que ce contrôle peut détecter : une assertion qui confond « deux flottes »
et « deux grilles ».

**Observation**

```
own=17 bot=17 partagees=5 -> (3,4), (3,5), (3,8), (5,9), (6,9)
```

Les deux flottes partagent 5 cases sur 17. C'était prévisible sans exécuter :
chaque joueur possède **sa propre grille**, et la coordonnée (3,4) de la grille
de l'humain n'a aucun rapport avec la coordonnée (3,4) de celle du bot. Deux
flottes placées au hasard sur deux grilles se recouvrent naturellement.

La prescription aurait donc produit un test rouge sur un code juste — et, pire,
un test que le réflexe naturel aurait « corrigé » en changeant la graine jusqu'à
ce qu'il passe.

**Décision et justification**

**Diagnostic accepté, prescription rejetée.**

L'assertion tautologique est supprimée : elle prétendait protéger la
confidentialité de la flotte et ne protégeait rien. Mais l'invariant à tester
n'est pas « les deux flottes sont disjointes » — cet énoncé est faux dans le
domaine. Il est :

> la vue servie au navigateur décrit la flotte de l'humain, et pas celle du bot.

Traduit en :

```csharp
Assert.False(humanFleet.SetEquals(botFleet), "les deux flottes sont identiques : le test ne prouverait rien");
// ...
Assert.True(served.SetEquals(humanFleet));
Assert.False(served.SetEquals(botFleet));
```

La première ligne est un garde sur le test lui-même : si les deux flottes
étaient identiques, les deux assertions suivantes ne discrimineraient plus rien.
C'est le défaut de l'assertion d'origine, transposé — et donc celui à ne pas
reproduire.

**Preuves et limites**

| | |
|---|---|
| Réfutation | 5 cases communes sur la graine 17, exécutée avant correction |
| Correction | commit `cd4c4dd` — `GameViewForClientTests.cs` |
| Mutation | `ViewForClient()` renvoyant la vue du joueur courant → 2 tests au rouge |
| Fil de revue | PR #1, commentaire `4013902228`, réponse et résolution |

Ce qui **reste non vérifié** :

- La mutation exécutée était déjà détectée par
  `ViewForClient_OnceTheBotIsToPlay_StillDescribesTheHuman`, via `ViewerName`.
  La fuite n'était donc pas totalement sans filet : c'est l'assertion portant
  explicitement sur la **flotte** qui était vide. La correction supprime une
  fausse garantie, elle ne comble pas un trou complet.
- Le test compare des ensembles de coordonnées. Il ne dirait rien d'une fuite qui
  passerait par un autre membre du DTO — cette garantie-là repose sur la forme du
  type `GameViewResponse`, pas sur ce test.

**Ce que cette revue enseigne pour la suite du projet**

Un relecteur automatique raisonne sur le texte du test, pas sur le modèle du
domaine. Il a lu `ownCells` et `botCells` comme deux sous-ensembles d'un même
espace — ce qu'un nom de variable suggère et ce que le code ne dit pas. Son
diagnostic, lui, ne portait que sur la logique ensembliste de la ligne : c'est
exactement le registre où il est fiable.

D'où la règle retenue pour les revues suivantes : **traiter séparément le
défaut signalé et le remède proposé.** Le premier mérite d'être vérifié, le
second d'être reconstruit à partir du domaine.

---

## Revue 3 — Le `ConcurrentDictionary` rendait-il vraiment le stockage sûr ?

**Proposition examinée**

L'ADR 0004, rédigé pendant le socle, conclut :

> **Le cycle de vie Singleton impose une implémentation thread-safe.** C'est la
> raison du `ConcurrentDictionary` plutôt qu'un `Dictionary` : deux requêtes HTTP
> concurrentes touchent la même instance.

Le même raisonnement figurait en commentaire dans `InMemoryGameRepository` et
dans `AGENTS.md` § 4. La revue de la PR #1 l'a contesté sur deux fichiers.

**Hypothèse à vérifier**

> Avec un `ConcurrentDictionary<Guid, Game>`, deux requêtes concurrentes visant
> la même partie ne peuvent pas corrompre son état.

**Expérience**

Deux contrôles, tous deux écrits avant d'implémenter quoi que ce soit :

1. Lancer 400 tirs sur la même partie en `Parallel.ForEach`, compter les appels
   acceptés, comparer à `game.Shots.Count`.
2. Faire projeter `ViewFor` par un fil pendant qu'un autre tire.

Résultat attendu, écrit avant exécution : si l'hypothèse est vraie, les deux
passent. Si elle est fausse, le premier montre un écart entre tirs acceptés et
tirs journalisés, et le second lève une exception d'énumération.

Erreur que ce contrôle peut détecter : toute confusion entre la sûreté de la
**table de stockage** et celle de l'**objet stocké**.

**Observation**

Sur le code du socle, sans verrou :

```
Échoué FireFromClient_CalledConcurrently_RecordsExactlyOneShotPerAcceptedCall
  System.InvalidOperationException : Operations that change non-concurrent
  collections must have exclusive access. A concurrent update was performed on
  this collection and corrupted its state.
```

L'hypothèse est fausse. Le `ConcurrentDictionary` protège `Find` et `Add` — la
table. Il ne protège pas le `Game` qu'il rend, qui reste un agrégat mutable
partagé : un `List<Shot>` et deux champs de tour.

Le chemin n'est pas théorique. `/games/{id}/shots` est en HTTP, `Fire` est en
gRPC-Web, et les deux atteignent le même Singleton.

**Décision et justification**

**Adaptée** — la remarque est retenue, sa localisation ne l'est pas.

Le verrou n'est pas allé dans le repository, comme la revue le suggérait, mais
dans `Game` : un `System.Threading.Lock` privé, **par partie**. Trois raisons.
Le repository ne voit pas les transitions — il rendrait un objet déjà
déverrouillé. Un verrou par partie laisse deux parties distinctes progresser en
parallèle. Et c'est le seul endroit que les deux transports traversent tous les
deux, donc le seul où le garde ne peut pas diverger.

L'option « rendre l'agrégat immuable », également citée par la revue, a été
écartée : elle annule l'ADR 0002 et réécrit le moteur, pour un socle
mono-processus.

Conséquence que la revue ne demandait pas : le choix de la cible du bot est
entré dans l'agrégat (`Game.PlayBotTurn(IBotStrategy)`). Tant qu'il vivait dans
l'endpoint, « vérifier que c'est au bot de jouer » et « tirer » restaient deux
opérations séparées — deux appels concurrents à `/bot-turn` auraient joué deux
tours. **Verrouiller `Fire` seul aurait donné l'illusion d'avoir corrigé le
défaut.**

**Preuves et limites**

| | |
|---|---|
| Correction | commit `cd4c4dd` — `BattleShip.Domain/Game.cs` |
| Tests | `GameConcurrencyTests`, 2 cas |
| Mutation | `lock (_gate)` → `if (true)` → corruption du journal |
| Décision tracée | ADR 0008 |
| ADR corrigé | ADR 0004, section « Conséquences » |

Ce qui **reste non vérifié** :

- Les deux tests **ne prouvent pas l'absence de course**. Ils en attrapent
  l'occurrence : ils échouent vite quand le verrou saute, ils ne passent jamais
  à tort. C'est une propriété plus faible que ce que leur nom laisse croire, et
  elle est assumée comme telle.
- `GameMapper.ToResponse` relit `game.Status` et `game.Winner` **après** la fin
  du verrou. La transition est atomique, le corps de réponse peut refléter un
  état légèrement postérieur au tir qu'il décrit.
- Le verrou ne couvre qu'un processus. Il deviendra insuffisant à la bascule
  vers SQLite (backlog item 5), qui demandera une transaction.

**Ce que cette revue enseigne pour la suite du projet**

L'ADR 0004 n'était pas faux, il était **imprécis au mauvais endroit**. « Le
Singleton impose une implémentation thread-safe » est vrai ; « c'est la raison du
`ConcurrentDictionary` » laisse croire que le choix du type de collection épuise
la question. Une phrase d'ADR qui répond à une question voisine de celle qu'elle
pose se relit comme une garantie.

Le contrôle qui manquait était trivial à écrire et n'existait pas, parce que la
suite de tests raisonnait en tours successifs — jamais en appels simultanés.

---

## Revue 4 — Un écart de 1,3 tir mesuré sur 500 parties est-il un écart ?

**Proposition examinée**

Pendant l'item 2, l'IA a mesuré le coût de deux raffinements de l'algorithme
`HuntTarget` en les désactivant tour à tour, puis a conclu :

> Désactiver la résolution des navires coulés rend `HuntTarget` **meilleur**
> (64,6 tirs contre 65,8) et `HuntTargetParity` **pire** (60,3 contre 59,4).
> Mesuré sur 500 parties appariées.

⚠️ **Le nombre 64,6 apparaît deux fois dans ce dépôt pour deux choses
différentes.** Ci-dessus, c'est `HuntTarget` **sans** résolution, sur 500
parties. Dans l'ADR 0009 et dans `PROMPTS.md`, c'est `HuntTarget` **livré**, sur
100 parties. Les deux valeurs sont justes ; leur rapprochement est une
coïncidence d'arrondi. Table de référence complète plus bas.

Et l'explication avancée dans la foulée : *les navires se regroupent sur une
grille 10 × 10, donc continuer à sonder autour d'un impact reste du bon terrain.*

La proposition a deux parties, et c'est leur écart qui fait l'objet de la revue :
un **chiffre** présenté comme un fait, et une **explication** qui n'en couvre que
la moitié. L'explication prédit le même signe pour les deux difficultés — la
parité ne change pas la façon dont les navires sont placés. Or les signes
observés étaient opposés.

**Hypothèse à vérifier**

> L'écart de 1,3 tir observé entre la variante livrée et la variante sans
> résolution est un écart réel, et non une fluctuation de l'échantillon.

Le point aveugle était nommé : la mesure rapportait deux moyennes et **aucune
dispersion**. Le nombre de tirs pour vider une flotte standard varie fortement
d'une partie à l'autre — écart type de l'ordre de 10 tirs. Avec 500 parties,
l'erreur type d'une moyenne est déjà d'environ 0,45 tir ; celle d'une
**différence** dépend de la corrélation entre les deux variantes et n'avait pas
été calculée du tout.

**Expérience**

Reprendre les deux variantes en enregistrant le nombre de tirs **partie par
partie**, pas seulement la moyenne, sur les mêmes graines de placement. Calculer
la moyenne des différences appariées et son erreur type. Répéter à 1 000 puis à
4 000 parties.

Résultat attendu, écrit avant exécution : si les écarts sont réels, le rapport
différence / erreur type croît comme la racine de l'échantillon et les signes
restent stables. S'ils sont du bruit, le rapport stagne autour de 1 et une
estimation peut changer de valeur, voire de signe.

Erreur que ce contrôle peut détecter : une conclusion tirée d'un écart plus petit
que la précision de la mesure qui l'a produit.

**Observation**

| Échantillon | Raffinement désactivé | Difficulté | Différence appariée | Erreur type | Rapport |
|---|---|---|---|---|---|
| 1 000 | résolution des coulés | `HuntTarget` | −1,32 | 0,69 | −1,9 |
| 1 000 | résolution des coulés | `HuntTargetParity` | **+1,32** | 0,61 | 2,2 |
| 1 000 | préférence d'alignement | `HuntTarget` | +1,85 | 0,64 | 2,9 |
| 1 000 | préférence d'alignement | `HuntTargetParity` | +1,83 | 0,51 | 3,6 |
| 4 000 | résolution des coulés | `HuntTarget` | −1,31 | 0,35 | −3,7 |
| 4 000 | résolution des coulés | `HuntTargetParity` | **+0,55** | 0,31 | 1,8 |
| 4 000 | préférence d'alignement | `HuntTarget` | +1,50 | 0,33 | 4,6 |
| 4 000 | préférence d'alignement | `HuntTargetParity` | +2,13 | 0,25 | 8,6 |

Trois choses apparaissent.

1. **La préférence d'alignement est établie** aux deux difficultés, et elle se
   renforce avec l'échantillon : c'est la signature d'un effet réel.
2. **Le coût de la résolution sur `HuntTarget` est établi** : −1,31 tir, stable
   de 1 000 à 4 000 parties. Le raffinement livré rend bel et bien ce niveau
   moins efficace.
3. **Le bénéfice de la résolution sur `HuntTargetParity` n'existe pas.**
   L'estimation passe de +1,32 à +0,55 en quadruplant l'échantillon, et le
   rapport reste sous 2. Le chiffre initial était du bruit.

C'est exactement le point du contradicteur : l'affirmation « mesuré sur 500
parties appariées » donnait à une valeur instable l'autorité d'un fait.

**Table de référence — aucune valeur de ce projet ne se lit sans sa variante ni
son échantillon :**

| Variante | Parties | `Random` | `HuntTarget` | `HuntTargetParity` |
|---|---|---|---|---|
| **livrée** | 100 | 95,3 | **64,6** | 58,7 |
| livrée | 500 | — | 65,8 | 59,4 |
| livrée | 4 000 | — | 65,9 | 59,5 |
| sans résolution | 500 | — | **64,6** | 60,3 |
| sans résolution | 4 000 | — | 64,6 | 60,1 |
| sans alignement | 4 000 | — | 67,4 | 61,7 |

La variante livrée passe elle-même de 64,6 à 65,9 entre 100 et 4 000 parties :
la mesure a une précision d'environ ±1 tir à 100 parties, et les valeurs du
tableau de l'ADR ne sont pas significatives à la décimale.

Ce qui rend le **classement** robuste malgré cela, c'est l'ordre de grandeur des
écarts : 30 tirs entre `Random` et `HuntTarget`, 6 entre `HuntTarget` et
`HuntTargetParity`. Ces écarts-là sont très supérieurs à la précision de
l'échantillon — d'où des tests d'efficacité fiables sur le classement, et
incapables de trancher une comparaison de variantes.

**L'explication était fausse aussi, et une mesure la remplace.** Plutôt que
d'invoquer la répartition des navires, la part de tirs joués en chasse a été
comptée :

| Variante | `HuntTarget` | `HuntTargetParity` |
|---|---|---|
| avec résolution | 75 % de chasse | 71 % de chasse |
| sans résolution | 44 % | 39 % |

La résolution des coulés ne fait pas « gagner des tirs » : elle **rend le bot à
la chasse**. Sans elle, les cases touchées ne sortent jamais de la liste d'attente
et le bot reste en traque permanente. Le gain ou la perte dépend alors de ce vers
quoi il retourne — un tirage uniforme pour `HuntTarget`, qui est moins bon que
sonder autour d'un impact, un balayage en damier pour `HuntTargetParity`, qui est
meilleur. Un seul mécanisme, les deux signes.

**Décision et justification**

**Chiffre corrigé, explication remplacée, décision de conception maintenue.**

La résolution des navires coulés est conservée, et la justification change
complètement. Elle n'est **pas** gardée parce qu'elle ferait gagner des tirs :
elle en coûte 1,31 sur `HuntTarget` et son bénéfice ailleurs n'est pas établi.

Elle est gardée parce que le critère n'est pas le nombre de tirs, c'est ce que la
difficulté prétend être. Sans elle, `HuntTargetParity` ne joue plus que 39 % de
ses tirs en chasse : son damier — sa seule spécificité, ce qui la distingue de
`HuntTarget` — ne gouverne plus qu'une minorité de ses décisions. Une difficulté
dont le mécanisme distinctif est inerte les deux tiers du temps n'est pas la
difficulté annoncée.

Ce que la variante sans résolution décrit, en revanche, est un **autre
algorithme** : un bot qui exploite sans le savoir le regroupement des navires.
C'est une difficulté possible pour plus tard, pas une raison de laisser
celle-ci mal définie.

**Pourquoi ne pas garder la résolution uniquement là où elle se justifie ?**
L'objection est réelle : le seul coût établi — 1,31 tir — tombe sur
`HuntTarget`, et c'est précisément la difficulté où l'argument d'identité est le
plus faible. Sans résolution, elle chasse encore 44 % du temps et « chasse et
traque » reste descriptif. La garder seulement pour `HuntTargetParity` ferait
gagner ce tir.

C'est écarté pour une raison qui n'est pas l'efficacité non plus. `CONTEXT.md`
définit les deux difficultés comme partageant la même traque et **ne différant
que par le balayage**. Les faire diverger sur un second point rendrait l'écart
mesuré entre elles ininterprétable : il additionnerait deux changements, et on
ne saurait plus dire ce que le damier apporte. L'échelle de difficulté est ce
que la fonctionnalité livre ; un tir gagné ne paie pas sa lisibilité.

**Preuves et limites**

| | |
|---|---|
| Mesures appariées | 4 000 parties, mêmes graines de placement entre variantes |
| Part de chasse | 1 000 parties, compteurs posés sur les deux phases puis retirés |
| Décision tracée | ADR 0009, sections « Conséquences » et « Vérification » |
| Contradiction initiale | Session pair, objection de méthode sur l'absence d'erreur type |

Ce qui **reste non vérifié** :

- Le rapport 1,8 de `HuntTargetParity` sans résolution n'établit **ni** un effet
  **ni** son absence. Il établit seulement que 4 000 parties ne suffisent pas à
  le trancher. L'écart, s'il existe, est inférieur à l'ordre de grandeur mesuré.
- Les erreurs types supposent des parties indépendantes. Les graines sont
  distinctes mais issues du même générateur ; aucun contrôle n'a été fait sur
  l'indépendance réelle des placements produits.
- Ces mesures sont **hors suite de tests**. Elles ont été obtenues en modifiant
  le code puis en le rétablissant ; elles ne sont pas rejouées à chaque
  `dotnet test`, qui ne vérifie que le classement des trois difficultés avec des
  bornes larges. Une dégradation de 1,3 tir passerait inaperçue en CI.
- La part de chasse a été mesurée avec des compteurs ajoutés au domaine pour
  l'occasion, **absents du code livré**. Le chiffre n'est pas reproductible en
  l'état par un lecteur : il faut reposer les sondes.

**Ce que cette revue enseigne pour la suite du projet**

Une mesure produite par l'IA porte la même assurance qu'une affirmation produite
par l'IA, et le chiffre la fait paraître plus solide encore. « 64,6 contre
65,8 sur 500 parties » se lit comme un fait ; c'était une différence de 1,3 avec
une précision de 0,7, c'est-à-dire presque rien.

La règle retenue : **un écart entre deux mesures ne vaut que rapporté à la
précision de la mesure.** Une moyenne sans dispersion n'est pas un résultat, et
le réflexe d'augmenter l'échantillon quand le rapport stagne doit précéder la
conclusion, pas la suivre.

Deuxième enseignement, plus inattendu : **l'explication d'un résultat est une
proposition à vérifier au même titre que le résultat.** Celle avancée ici était
plausible, cohérente avec le domaine, et fausse — elle ne rendait compte que
d'un des deux signes observés. C'est ce défaut de couverture, pas le chiffre, qui
a mis sur la piste. Une explication qui n'explique que la moitié de ce qu'on
observe signale toujours quelque chose.
