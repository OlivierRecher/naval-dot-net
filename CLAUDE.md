# CLAUDE.md

**Lire [AGENTS.md](./AGENTS.md) en premier.** C'est la source unique de vérité :
contraintes imposées, architecture, contrat d'API, conventions, backlog et
définition de « terminé ». Ce fichier n'ajoute que ce qui est propre à Claude Code.

Le vocabulaire du domaine est dans [CONTEXT.md](./CONTEXT.md).

---

## Ce qui est en jeu

TP noté en binôme. La **maîtrise de l'IA** est un critère d'évaluation à part
entière : ce qui est noté, ce n'est pas le code que tu produis, c'est la
capacité du binôme à le cadrer, le vérifier et le défendre.

Conséquence directe sur ta façon de travailler : **une proposition non vérifiée
n'a aucune valeur ici**, même si elle compile.

---

## Expliquer dans la réponse, pas dans les commentaires

**Règle de fond : tout ce que tu écris sert à ce que le binôme sache défendre le
code sans toi.**

Le QCM compte pour 50 % de la note, il est **individuel, sans document et sans
IA**, et chaque membre doit pouvoir expliquer « un parcours complet, du
navigateur jusqu'au serveur ». Du code livré que personne ne sait expliquer,
c'est deux fois des points perdus.

### Dans la réponse : expliquer

À chaque fois que tu produis ou modifies du code, explique **dans la
conversation** :

- **Ce que fait le code** — le comportement, pas la paraphrase ligne à ligne.
- **Pourquoi cette approche** plutôt qu'une autre crédible.
- **Quelle notion du cours est mobilisée**, nommée explicitement : propriétés et
  `record` C#, asynchrone `async`/`await` et `Task<T>`, commandes `dotnet`,
  liaison des paramètres en Minimal API, injection de dépendances et durées de
  vie, cycle de vie Blazor, FluentValidation, xUnit, gRPC, CORS. Ce sont
  exactement les sujets du QCM.
- **Ce qui pourrait casser** et comment on le verrait.

Le support demande de « relier chaque notion à un cas du projet : entrée,
comportement, erreur possible et vérification ». C'est le format attendu de tes
explications.

Explique aussi quand tu **corriges** quelque chose : ce qui était faux, pourquoi
ça l'était, ce que ça révèle. C'est la matière première de `REVUE-IA.md`.

### Dans le code : ne pas commenter

Le code livré reste **propre et idiomatique**. Un commentaire ne se justifie que
là où le code ne peut pas parler tout seul :

- un invariant non évident (« le serveur ne reçoit jamais d'identité de joueur »),
- un compromis délibéré, avec un renvoi vers l'ADR qui le motive,
- une contrainte externe subie.

Pas de commentaire qui paraphrase la ligne suivante, pas de bandeau d'en-tête,
pas de `// Étape 1 :` pédagogique. Du code sur-commenté est le marqueur le plus
visible d'une génération non relue — et « le code généré automatiquement est
identifié et son rôle est compris » est une ligne de la checklist de remise.

**Le raisonnement va dans la conversation et dans les ADR. Le code reste du code.**

---

## Avant d'écrire du code

1. Lire `CONTEXT.md` et employer ses termes.
2. Vérifier les règles de dépendance d'`AGENTS.md` § 4.
3. Dans `BattleShip.Domain` : écrire le test d'abord.

## Après avoir écrit du code

- Lancer `dotnet build` puis `dotnet test`.
- Pour chaque nouveau test de règle : **casser volontairement la règle**,
  montrer le test au rouge, rétablir. Sans cette manipulation, on ne sait pas si
  le test protège quoi que ce soit.
- Si une décision structurante a été prise : proposer l'entrée `PROMPTS.md` ou
  l'ADR, **courte**, dans le même commit que le code.

---

## Vérifier avant d'affirmer

Le support est explicite : « le ton assuré du modèle ne démontre ni la
pertinence ni la correction de la solution », et « recopier une analyse ou
demander à l'IA de se déclarer correcte ne constitue pas une vérification ».

Donc :

- Une API .NET affirmée se confirme dans la documentation **.NET 10** ou par
  exécution — pas de mémoire.
- Options d'une commande : `dotnet new <modèle> --help`.
- Comportement d'un bout de C# isolé : `dotnet run --file essai.cs`, fichier
  placé **hors** d'un dossier contenant un `.csproj`.
- Si tu n'es pas sûr, dis-le au lieu de produire une réponse plausible.

---

## Ce que tu ne fais pas

- **Ne pas déborder du périmètre.** Les features se font dans l'ordre d'`AGENTS.md` § 10, une à la fois, mergée avant la suivante.
- **Ne pas ajouter de dépendance** à `BattleShip.Domain`, jamais.
- **Ne pas faire référencer `Domain` par `App`.**
- **Ne pas introduire de `playerId` fourni par le client** — voir `AGENTS.md` § 4.
- **Ne pas ajouter de package NuGet** sans que ce soit une décision discutée et tracée.
- **Ne pas commiter ni pousser** sans qu'on te le demande. `main` est protégée : on travaille en branche + PR.
- **Ne pas rédiger les livrables IA à la place du binôme.** Tu proposes un
  brouillon factuel ; l'analyse et la décision leur appartiennent — c'est
  précisément ce qui est noté.

---

## Ton et langue

- Réponses et livrables `.md` en **français**.
- Code et noms de tests en **anglais**.
- Quand une proposition a une limite connue, la dire. Une limite explicite se
  transforme en ligne de `REVUE-IA.md` ; une limite tue se transforme en point perdu.
