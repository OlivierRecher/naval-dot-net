# ADR 0007 : multijoueur local (hot-seat) plutôt que multijoueur en ligne

## Statut et date
Accepté — 2026-09-15. Concerne le backlog item 4 ; hors périmètre du socle.

## Contexte
Le support note l'ambition du périmètre livré et sanctionne explicitement le
minimum (diapos 60 et 61). Il cite le multijoueur parmi les pistes
d'extension. Il exige en contrepartie que « chaque fonctionnalité livrée soit
intégrée, vérifiée et comprise par le binôme ».

Le multijoueur est l'extension la plus visible. Reste à choisir sa forme, et
cette décision conditionne l'architecture dès le premier commit : un multijoueur
en ligne ne se greffe pas après coup sur un serveur conçu sans lui.

## Options envisagées

**(a) Multijoueur en ligne.** Deux navigateurs, un transport temps réel
(SignalR ou streaming gRPC), gestion des reconnexions, des abandons et des
désynchronisations. Le plus impressionnant. Aussi le seul dont l'échec partiel
produit une fonctionnalité **inutilisable** plutôt qu'incomplète : une partie
qui se désynchronise ne se démontre pas.

**(b) Multijoueur local en alternance sur un seul navigateur.** Aucune
infrastructure réseau supplémentaire : le serveur gère déjà deux joueurs et une
alternance de tours, puisque le socle fait jouer un humain contre un bot. Le
travail se réduit à remplacer le bot par un second humain et à masquer l'écran
entre les tours.

**(c) Les deux.** Doublerait la surface à tester et à défendre, au détriment des
autres items du backlog.

## Décision
Option **(b)**.

Le multijoueur en ligne est **retiré du périmètre**, décision assumée. Le budget
libéré finance trois extensions finies — niveaux de bot, placement manuel,
persistance et statistiques — plutôt qu'une infrastructure temps réel fragile.

Le raisonnement : à ambition affichée égale, trois fonctionnalités démontrables
valent mieux qu'une quatrième que le binôme ne saurait ni finir ni expliquer.

## Conséquences
- Le socle et le multijoueur local partagent le même modèle de tour : le mode
  `Local` remplace simplement le bot par un humain. Le surcoût est faible.
- **Une difficulté est créée, pas supprimée** : deux joueurs devant un seul
  écran contredisent frontalement l'exigence de secret des positions
  (diapo 36). C'est l'ADR 0003 qui la résout, en supprimant la possibilité même
  pour le client de demander la vue de l'adversaire.
- Un écran de passation est nécessaire — confort d'affichage, jamais la
  protection.
- Aucune dépendance temps réel, aucune gestion de reconnexion, aucun état de
  session distribué.
- Un jury attendant du multijoueur en ligne lira ce périmètre comme un recul.
  L'argument se défend par les faits : trois extensions livrées et vérifiées,
  et un invariant de sécurité renforcé plutôt que contourné.

## Vérification et réexamen
- Une partie complète à deux humains se joue du début à la victoire.
- La `GameView` ne divulgue jamais les positions adverses non découvertes, y
  compris en mode `Local` — c'est le contrôle qui distingue un hot-seat correct
  d'un hot-seat qui triche par affichage.

À réexaminer si les items 1 à 3 du backlog étaient terminés avec une avance
confortable. Le multijoueur en ligne redeviendrait alors envisageable, au prix
d'un ADR successeur remplaçant l'ADR 0003.

## Références
- Diapos 36, 46, 60, 61 du support de cours
- ADR 0003
- `AGENTS.md` § 10
