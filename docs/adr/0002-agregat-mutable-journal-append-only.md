# ADR 0002 : agrégat `Game` mutable doublé d'un journal de tirs en ajout seul

## Statut et date
Accepté — 2026-09-15.

## Contexte
Il faut choisir comment `Game` porte son état avant d'écrire les premiers tests
du moteur : la décision devient très coûteuse à changer une fois quelques
dizaines de tests écrits.

Deux exigences pèsent sur ce choix. Le moteur doit être testable en isolation
(diapo 36). Et le backlog prévoit historique, rejeu et statistiques
(`AGENTS.md` § 10, item 5) — des fonctionnalités qui dépendent entièrement de la
capacité à reconstituer le déroulé d'une partie.

## Options envisagées

**(a) Agrégat mutable simple.** `Fire()` modifie l'état et retourne un
`ShotResult`. Rapide à écrire, stockage trivial dans un dictionnaire. Mais
l'historique doit être reconstruit après coup, et rien ne garantit qu'un
changement d'état laisse une trace.

**(b) Records immuables, transitions pures.** `Fire(game, coordinates)` retourne
un nouveau `Game`. Testabilité maximale, historique gratuit. Mais copier une
grille 10 × 10 à chaque tir impose une cérémonie permanente en C# sans type
persistant natif, sur toute la durée du projet.

**(c) Agrégat mutable + journal de tirs en ajout seul.** `Game` porte l'état
courant, et tout tir accepté est ajouté à une liste ordonnée jamais modifiée.

**(d) Event sourcing complet.** L'état n'existe que comme projection des
événements. Réponse correcte à un problème que nous n'avons pas, pour un coût
sans rapport avec cinq jours de projet.

## Décision
Option **(c)**.

`Game` expose son état courant et un `IReadOnlyList<Shot> Shots` en ajout seul.
Les vérifications de règles — case dans la grille, case non déjà visée, partie
en cours, tour du joueur — sont des **fonctions pures**, testables sans
instancier une partie.

**Invariant central : un tir accepté et une entrée au journal sont une seule et
même chose.** Aucune modification d'état ne se produit sans entrée
correspondante, et une tentative refusée n'en crée aucune.

## Conséquences
- Historique, rejeu et statistiques deviennent des lectures du journal : le
  backlog item 5 ne demandera pas de remodeler le domaine.
- Le journal donne une assertion de test simple et puissante : le nombre de tirs
  ne bouge que lorsqu'un tir est accepté. C'est exactement le contrôle qui
  détecte une tentative refusée traitée à tort comme un coup joué.
- Pas de copie de grille : coût mémoire et cérémonie syntaxique évités.
- `Game` étant mutable et stocké en singleton (ADR 0004), l'accès concurrent
  devient une préoccupation réelle dès qu'une partie peut être manipulée par
  deux requêtes simultanées.
- L'immuabilité partielle est perdue : rien n'empêche structurellement un futur
  développeur de muter l'état sans journaliser. Seul un test le détecte.

## Vérification et réexamen
- Un test vérifie qu'une tentative sur une case déjà visée **n'ajoute rien** au
  journal et ne fait pas passer le tour.
- Un test vérifie que le vainqueur déduit du journal est le même que celui porté
  par l'état courant.
- Contrôle de mutation : casser volontairement la règle « une case déjà visée
  est refusée » doit faire échouer le test ci-dessus. Tant que cette
  manipulation n'a pas été faite, le test ne prouve rien.

À réexaminer si la concurrence sur une même partie devenait réelle : l'option
(b) redeviendrait attractive parce qu'elle supprime le problème au lieu de le
gérer.

## Références
- Diapos 36, 55 du support de cours
- `AGENTS.md` § 4, § 8
- `CONTEXT.md` — définitions de `Shot` et de `Shots`
