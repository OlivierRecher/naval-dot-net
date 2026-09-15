# ADR 0003 : le serveur est autoritaire et le client ne transmet aucune identité de joueur

## Statut et date
Accepté — 2026-09-15.

## Contexte
Deux exigences du support entrent en collision dès qu'on introduit le
multijoueur local.

La première : « garder secrètes les positions adverses non découvertes »
(diapo 36), et ne présenter que « les informations que le joueur est autorisé à
connaître » (diapo 46).

La seconde, née de notre choix de multijoueur hot-seat (ADR 0007) : **un seul
navigateur pilote les deux joueurs**. Si le client demande au serveur « donne-moi
la vue du joueur A », il peut tout aussi bien demander celle du joueur B et
révéler sa flotte. Le secret ne serait alors garanti que par la discipline du
code client — c'est-à-dire pas garanti du tout.

## Options envisagées

**(a) Le client transmet un identifiant de joueur, le serveur vérifie qu'il
correspond au tour courant.** Fonctionne, mais le secret devient une règle
*contrôlée* : il existe un paramètre dont la validation peut être oubliée,
contournée ou mal implémentée. C'est une surface d'erreur créée volontairement.

**(b) Un jeton par joueur, émis à la création de la partie.** Solution correcte
pour du multijoueur en ligne, où les deux joueurs sont sur des machines
distinctes. En hot-seat, les deux jetons vivent dans le même navigateur : la
protection est illusoire, pour un coût réel.

**(c) Le client ne transmet rien. Le serveur renvoie toujours la vue du joueur
dont c'est le tour.** Le paramètre disparaît, donc la faille aussi.

## Décision
Option **(c)**.

`GET /games/{id}` retourne la `GameView` du `CurrentPlayer`, sans paramètre.
Aucun endpoint n'accepte d'identifiant de joueur en entrée. Le serveur est seul
juge du tour courant et de ce qui est visible.

L'écran de passation du mode local est un **confort d'affichage**, pas la
protection. La protection est que le serveur n'a jamais eu la capacité
d'envoyer autre chose.

## Conséquences
- Le secret des positions devient **structurellement impossible à violer par le
  client**, au lieu d'être vérifié. C'est une différence de nature, pas de degré.
- Une classe entière de tests de sécurité disparaît : il n'y a pas de paramètre
  à fuzzer.
- Un mode spectateur, un affichage multi-appareils ou un multijoueur en ligne
  sont **impossibles sans remplacer cet ADR**. C'est le compromis accepté, et il
  est cohérent avec le retrait du multijoueur en ligne du périmètre (ADR 0007).
- Le front ne peut pas pré-charger la vue du joueur suivant pendant la
  passation : il doit rappeler l'API après le changement de tour.
- Toute proposition réintroduisant un `playerId` fourni par le client est à
  rejeter sans discussion tant que cet ADR est en vigueur.

## Vérification et réexamen
- Un test du domaine vérifie qu'une `GameView` ne contient **aucune** position
  de navire adverse non découverte.
- Un test d'intégration vérifie qu'après un tir accepté, le `GET` suivant
  retourne la vue de l'autre joueur — et donc que la bascule est bien pilotée
  par le serveur.
- Contrôle de mutation : retirer le filtrage des positions adverses dans la
  projection doit faire échouer le premier test.

À remplacer par un ADR successeur si le multijoueur en ligne revenait dans le
périmètre. L'option (b) serait alors la bonne.

## Références
- Diapos 36, 46, 63 du support de cours
- ADR 0007
- `AGENTS.md` § 4
- `CONTEXT.md` — définitions de `GameView` et `Handover`
