# REVUE-IA.md

Revues argumentées de propositions produites par l'IA. Trois minimum sont
attendues, dont au moins une adaptée ou rejetée : trois acceptations
n'établiraient rien.

Chaque revue nomme ce qui devait être vrai, l'expérience choisie pour le mettre
en défaut, ce qui a réellement été observé, et ce qui reste non vérifié.

Binôme : Olivier Recher (@OlivierRecher) · Ulysse (@Oulssyyy)

**État : 7 revues — 2 adaptées, 1 correctif rejeté, 1 conclusion invalidée, 2 défauts invisibles aux tests, 1 test qui ne testait pas.**

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
---

## Revue 5 — Une suite de tests verte peut-elle garantir un écran utilisable ?

**Proposition examinée**

L'écran de placement manuel produit pour l'item 3, dans `Play.razor`. La grille
est dessinée par deux boucles imbriquées, et chaque case est un bouton qui pose
le navire courant :

```razor
@for (var row = 0; row < Session.View.Rows; row++)
{
    for (var column = 0; column < Session.View.Columns; column++)
    {
        var cell = new CoordinatesDto(column, row);

        <button type="button"
                class="cell @(_takenCells.Contains(cell) ? "cell-ship" : "cell-empty")"
                title="@Label(cell)"
                @onclick="() => PlaceAt(column, row)">
        </button>
    }
}
```

Au moment de l'écrire, la proposition était accompagnée d'un argument de
couverture : le domaine est en TDD, la frontière HTTP a ses tests d'intégration,
les neuf mutations sont détectées, **172 tests sont verts**. Le placement manuel
était donc annoncé comme livré.

**Hypothèse à vérifier**

> Une fonctionnalité dont le domaine et la frontière HTTP sont testés, et dont
> le composant compile, est utilisable dans le navigateur.

C'est l'hypothèse implicite de toute annonce de livraison fondée sur une suite
verte. `AGENTS.md` § 12 ne la partage pas : « le comportement est **jouable dans
le navigateur**, pas seulement testé » y est une ligne distincte de
« `dotnet build` et `dotnet test` passent ».

**Expérience**

Lancer l'API et le front, créer une partie en placement manuel, et **cliquer
réellement** cinq cases de la grille.

Résultat attendu, écrit avant exécution : le compteur passe de « 0 / 5 navires
placés » à « 5 / 5 », les cases occupées changent de couleur, et le bouton
« Valider la flotte » s'active.

Erreur que ce contrôle peut détecter : tout ce qui sépare un composant qui
compile d'un composant qui répond — un gestionnaire jamais appelé, une liaison
inversée, un état qui ne se rafraîchit pas.

**Observation**

Après cinq clics : **« 0 / 5 navires placés »**. Aucune case ne change. Aucune
erreur, aucune exception, aucun appel réseau — rien ne signale quoi que ce soit.

La cause est dans le lambda. `column` et `row` sont les variables d'une boucle
`for` : en C#, une boucle `for` n'a **qu'une seule** variable pour toutes ses
itérations, et le lambda la capture par référence. Au moment où l'utilisateur
clique, les boucles sont terminées depuis longtemps et les deux variables valent
leur valeur de sortie — `10` et `10`. Chaque bouton de la grille appelait donc
`PlaceAt(10, 10)`, que le contrôle de débordement rejetait en silence.

Le correctif tient en un mot : capturer la copie locale, `PlaceAt(cell)`.

Le détail qui rend l'affaire instructive : **la grille de tir voisine, écrite à
l'item 1, n'avait pas ce défaut.** Elle capture `target`, une copie locale créée
dans le corps de la boucle. Les deux grilles sont côte à côte dans le même
fichier ; l'une est correcte, l'autre non, et la différence tient à une variable
intermédiaire dont rien n'indique qu'elle est autre chose qu'une commodité de
lecture.

**Décision et justification**

**Défaut corrigé, et l'argument de couverture rejeté.**

Les 172 tests ne pouvaient pas détecter ce défaut, et il ne s'agit pas d'un trou
qu'on pourrait combler en en ajoutant. Aucun d'eux n'instancie un composant
Blazor : le projet n'a pas de `bUnit`, et `AGENTS.md` § 8 ne prévoit que le
domaine et l'API. Le défaut vit exactement dans l'espace que la stratégie de test
laisse vide — et c'est une décision assumée, pas un oubli.

Ce qui est rejeté, c'est donc l'inférence : « 172 tests verts » n'est pas un
argument sur le front, parce qu'aucun des 172 ne le traverse. La ligne d'`AGENTS.md`
§ 12 qui exige le navigateur n'est pas une formalité de plus, c'est la **seule**
vérification qui couvre cette zone.

**Preuves et limites**

| | |
|---|---|
| Défaut observé | 5 clics, compteur inchangé à 0 / 5 |
| Cause | Capture des variables de boucle `for` dans `@onclick` |
| Correction | `PlaceAt(cell)` sur la copie locale, plus un commentaire disant pourquoi |
| Contrôle après correction | 5 navires posés, flotte validée, partie jouée jusqu'au tir et à la riposte du bot |
| Contre-exemple dans le même fichier | La grille de tir de l'item 1, correcte parce qu'elle capture `target` |

Ce qui **reste non vérifié** :

- La correction n'a **aucun test automatisé**. Elle est protégée par la même
  chose qui l'a trouvée : quelqu'un qui clique. Une régression identique
  passerait la CI.
- Le contrôle navigateur a porté sur un seul chemin — grille 10 × 10, cinq
  navires verticaux, tous valides. Les refus locaux (débordement, chevauchement)
  n'ont pas été exercés à la main ; ils ne sont couverts ni par un test, ni par
  cette observation.
- Rien n'établit qu'il ne reste pas d'autres captures fautives ailleurs dans le
  front. Les deux grilles ont été relues ; le reste de `Play.razor` n'a pas été
  audité pour ce motif précis.

**Ce que cette revue enseigne pour la suite du projet**

Une suite de tests ne dit rien des zones qu'elle ne traverse pas, et la tentation
est de lire son verdict comme s'il portait sur le tout. « 172 tests, 0 échec »
est une phrase vraie qui, placée à côté de « le placement manuel est livré »,
suggère un lien qui n'existe pas.

La règle retenue : **avant d'annoncer une fonctionnalité livrée, nommer la zone
que les tests ne couvrent pas et dire par quoi elle a été vérifiée à la place.**
Ici la réponse est « le front, vérifié à la main dans le navigateur » — et c'est
cette phrase, pas le nombre de tests, qui porte la livraison.

Corollaire pour l'item 4 (hot-seat) et l'item 6 (flotte personnalisable), qui
ajouteront tous deux du code de composant : le contrôle navigateur n'est pas la
dernière étape de confort une fois les tests verts, c'est la vérification
principale de cette partie-là du code.
---

## Revue 6 — Un test de concurrence qui passe prouve-t-il qu'il y a un verrou ?

**Proposition examinée**

La revue Copilot de la PR #5 relève, justement, qu'aucun test ne lance deux
soumissions de flotte simultanées, alors que `GameConcurrencyTests` le fait pour
les tirs depuis l'item 1. Le test écrit en réponse :

```csharp
var accepted = 0;

Parallel.For(0, 64, _ =>
{
    if (game.PlaceFleetFromClient(ValidFleet()).IsAccepted)
    {
        Interlocked.Increment(ref accepted);
    }
});

Assert.Equal(1, accepted);
```

Il passait. Le trou signalé par la revue paraissait comblé.

**Hypothèse à vérifier**

> Ce test échoue si le verrou de `PlaceFleetFromClient` disparaît.

C'est la seule chose qui distingue un test de concurrence d'un test qui se
contente de ne pas planter. La formulation vient de `CLAUDE.md` : « pour chaque
nouveau test de règle, casser volontairement la règle, montrer le test au rouge,
rétablir ».

**Expérience**

Remplacer `lock (_gate)` par `if (true)` dans `PlaceFleetFromClient`, exécuter.

Résultat attendu, écrit avant exécution : sans verrou, plusieurs fils franchissent
le contrôle « une grille attend-elle une flotte ? » avant qu'aucun n'ait posé de
navire ; le test doit donc compter plus d'une acceptation et virer au rouge.

**Observation**

```
base (avec verrou)     : 0/14 au rouge
mutation (sans verrou) : 0/14 au rouge
```

**Le test passait sans verrou.** Il n'attestait rien.

La cause n'est pas dans le code testé mais dans la façon de lancer les fils.
`Parallel.For` démarre ses itérations progressivement : la première soumission —
quelques microsecondes — se termine avant que la deuxième ne commence. La course
ne se produisait jamais, donc le test ne pouvait pas la voir.

Une première correction, un `ManualResetEventSlim` sur lequel les fils attendent
un signal commun, **n'a pas suffi** : les premiers fils partaient pendant que les
derniers étaient encore créés. Il a fallu attendre explicitement que tous soient
garés sur le signal avant de le lever.

Avec 64 fils réellement synchronisés, l'effet est massif et reproductible :

| | acceptations | navires sur la grille |
|---|---|---|
| avec verrou | 1, dix fois sur dix | 5 |
| sans verrou | 2 à 64 | 5 à **18** |

Dix-huit navires sur une grille qui en admet cinq : deux soumissions entrelacées
posent chacune la leur, et `Board.Place` refuse silencieusement les
chevauchements sans que `PlaceFleetFromClient` regarde son retour.

**Décision et justification**

**Diagnostic de la revue accepté, premier correctif rejeté, deuxième adopté.**

La remarque de Copilot était juste et le trou réel. Le test écrit en réponse ne
le comblait pas : il documentait une intention. Seule la mutation l'a montré —
aucune relecture ne l'aurait fait, puisque le code du test *décrit* exactement ce
qu'il prétend faire.

Le test corrigé lance 64 fils, attend qu'ils soient tous en attente, puis les
libère ensemble. Il vérifie deux choses au lieu d'une : une seule acceptation, et
cinq navires — la seconde assertion attrape la corruption même si la première
passait par chance.

**Preuves et limites**

| | |
|---|---|
| Mutation avant correction | `lock` retiré → 0 / 14 au rouge |
| Mutation après correction | `lock` retiré → 1 / 14 au rouge, le test nommé |
| Mesure de la course | 10 essais, 64 fils : 2 à 64 acceptations, jusqu'à 18 navires |
| Remarque d'origine | PR #5, fil `PRRT_kwDOUbgUe86igkd4` |

Ce qui **reste non vérifié** :

- Le test **n'établit pas l'absence de course**, il en attrape l'occurrence —
  même réserve que l'ADR 0008 pour les tirs. Il échoue systématiquement sur cette
  machine quand le verrou saute ; rien ne garantit qu'il le ferait sur une
  machine à un seul cœur.
- Le `Thread.Sleep(20)` avant le signal est un délai empirique, pas une garantie
  de synchronisation. Il a été choisi parce qu'il rend la course reproductible
  ici, pas parce qu'une propriété le fonde.
- Les tests de concurrence des tirs, écrits à l'item 1, **n'ont pas été soumis à
  ce contrôle** : ils utilisent `Parallel.ForEach` et leur mutation avait bien
  mordu à l'époque. Rien ne dit qu'ils mordraient encore sur une machine plus
  rapide.

**Ce que cette revue enseigne pour la suite du projet**

Un test de concurrence est le seul type de test dont la réussite peut venir de ce
qu'il n'a pas fait le travail. Un test fonctionnel qui n'exerce rien échoue en
général sur une assertion ; un test de concurrence qui n'a pas produit de course
observe un état parfaitement valide et passe.

La règle retenue : **un test de concurrence n'est pas écrit tant que la mutation
correspondante n'a pas été exécutée.** Pour les autres tests, la mutation confirme
ce qu'on croit déjà ; pour ceux-là, elle est la seule chose qui distingue un test
d'un commentaire.

Corollaire : cette revue est née d'une remarque d'un relecteur automatique qui
avait raison sur le fond. Le trou existait. Mais la réponse spontanée à une
remarque juste — écrire le test manquant et le voir passer — reproduit
exactement le défaut que la remarque signalait, en donnant cette fois
l'apparence de l'avoir corrigé.
---

## Revue 7 — « Le domaine savait déjà le faire » suffit-il à livrer une feature ?

**Proposition examinée**

Au début de l'item 4, six tests de domaine décrivant une partie hot-seat ont été
écrits **avant** toute modification de production. Ils sont passés du premier
coup : l'agrégat alternait déjà entre deux humains, `ViewForClient()` suivait
déjà le joueur courant, et l'invariant de l'ADR 0003 tenait déjà.

D'où la proposition, formulée à ce moment-là : le hot-seat est « presque
livré », il ne reste qu'à laisser l'API créer une partie `Local`.

**Hypothèse à vérifier**

> Quand le domaine porte déjà la règle et que les tests le confirment, ce qui
> reste à faire est mécanique.

**Expérience**

Écrire la partie API et la partie front, puis **jouer une partie hot-seat dans
le navigateur** : créer, tirer, passer l'appareil, confirmer, jouer le tour
suivant.

Résultat attendu, écrit avant exécution : l'écran de passation masque tout,
puis la vue du second joueur apparaît avec sa propre flotte, différente de la
première.

Erreur que ce contrôle peut détecter : tout ce qui vit entre la règle et l'écran
— texte d'interface, état de rendu, séquence d'appels.

**Observation**

Le résultat attendu est bien obtenu, mais **au quatrième essai**. Les trois
premiers ont chacun révélé un défaut, dont aucun n'était visible dans les 192
tests verts.

1. **La page ne s'affichait plus du tout.** `NullReferenceException` au rendu.
   L'écran de passation avait été inséré au milieu d'une chaîne `@if / else if /
   else` de Razor, et l'insertion l'avait **coupée en deux** : la branche
   « partie en cours » ne dépendait plus de « la vue existe », donc elle
   s'exécutait avec une vue nulle. C'est du C# parfaitement valide ; le
   compilateur n'a rien à dire.
2. **Le bandeau annonçait « contre le bot Novice »** dans une partie qui n'oppose
   aucun bot, badge de difficulté compris. Le texte était correct tant qu'un seul
   mode existait.
3. **Le message du tir s'adressait au mauvais joueur** : « Vous manquez » affiché
   à Ulysse pour décrire le tir d'Olivier. En hot-seat, la vue bascule sur
   l'adversaire dès le tir résolu ; le nom du tireur doit être **capturé avant**
   l'appel, sinon il est déjà perdu quand le message se compose.

Le troisième est le seul des trois qui soit une vraie erreur de raisonnement, et
c'est une conséquence directe de la décision de l'ADR 0003 : le serveur change de
viewer sans que personne le lui demande. Le code d'interface écrit pour le mode
`Solo` supposait, sans le dire, que « le viewer » et « celui qui vient d'agir »
sont la même personne. En hot-seat, ils ne le sont jamais.

**Décision et justification**

**Hypothèse rejetée.** « Le domaine savait déjà le faire » était vrai, vérifiable,
et sans rapport avec ce qui restait à faire. Les six tests passés d'emblée ne
mesuraient pas l'avancement de la feature : ils mesuraient que la feature
*précédente* avait bien généralisé.

Ce constat ne dévalue pas ces six tests — ils ont une vraie valeur, celle
d'établir par exécution une affirmation que l'ADR 0010 s'était contenté de
formuler, et qui s'était d'ailleurs révélée fausse à la revue de la PR #5. Il
dévalue l'**inférence** qu'on en a tirée sur le travail restant.

**Preuves et limites**

| | |
|---|---|
| Tests domaine écrits avant production | 6, verts sans modification |
| Défauts trouvés au navigateur | 3, aucun couvert par les 192 tests |
| Défaut n° 1 | `Play.razor` : chaîne `if/else` coupée, `NullReferenceException` au rendu |
| Défaut n° 2 | Bandeau et badge parlant d'un bot absent |
| Défaut n° 3 | Nom du tireur capturé après la bascule de vue |
| Contrôle final | Création, tir, passation, confirmation, tour du second joueur |

Ce qui **reste non vérifié** :

- Les trois corrections n'ont **aucun test automatisé**, comme tout le front.
  Une régression identique passerait la CI.
- La partie hot-seat n'a été jouée que sur quelques tours, jamais jusqu'à la
  victoire, dans le navigateur. Le domaine, lui, la joue jusqu'au bout dans
  `ALocalGame_PlaysThroughToAWinner`.
- Le placement manuel **en hot-seat** — deux joueurs posant chacun leur flotte
  avec passation entre les deux — est couvert côté serveur par
  `ALocalGame_WithManualPlacement_AsksEachHumanInTurn`, mais **n'a pas été joué
  à la main**. C'est la combinaison la moins éprouvée de la livraison.

**Ce que cette revue enseigne pour la suite du projet**

Une feature traverse quatre couches, et une suite de tests qui en couvre deux ne
dit rien des deux autres. Le raccourci tentant — « le cœur est fait, le reste est
du branchement » — s'appuie sur la partie visible de la preuve.

La règle retenue, qui prolonge celle de la revue 5 : **le nombre d'essais qu'il
faut pour jouer la feature à la main est la mesure honnête de ce qui restait à
faire.** Ici, quatre.

Deuxième enseignement, plus spécifique : quand une décision d'architecture
déplace une notion — ici, « le viewer » cesse d'être « celui qui vient d'agir » —
tout le code écrit avant cette décision porte l'ancienne hypothèse sans l'avoir
écrite nulle part. Le défaut n° 3 n'était pas une faute d'inattention : c'était
une hypothèse devenue fausse, dans du code que personne n'avait de raison de
relire.
