# ADR 0005 : porter l'opération `Fire` en gRPC-Web, garder le reste en HTTP/JSON

## Statut et date
Accepté — 2026-09-15.

## Contexte
Le socle impose « au moins un échange fonctionnel entre le front et l'API, avec
une réponse et une erreur attendue démontrables » en gRPC-Web (diapo 48). Le
choix de l'opération nous revient, et le support demande d'expliquer
« l'articulation avec les autres échanges du projet ».

Le piège est de porter en gRPC une opération marginale — création de partie,
appelée une fois — puis de devoir justifier dans un ADR pourquoi un contrat
binaire servait à quelque chose. La justification serait creuse.

## Options envisagées

**(a) `CreateGame` en gRPC.** Opération appelée une seule fois par partie :
aucun argument de performance ou de volume ne tient. Erreurs pauvres.

**(b) `GetGameView` en gRPC.** Appelée souvent, mais c'est une lecture sans
entrée à valider : la démonstration d'une erreur attendue devient artificielle.

**(c) `Fire` en gRPC.** L'opération la plus fréquente d'une partie, la seule à
porter une entrée validée et un enjeu de règles. Trois erreurs naturelles à
démontrer.

**(d) Tout le contrat en gRPC.** Cohérent, mais supprime le fichier `api.http`
et les essais manuels que le support recommande (diapo 35), et rend la
comparaison entre transports impossible.

## Décision
Option **(c)**.

`Fire` passe par gRPC-Web, et **le front tire réellement via ce canal** — c'est
ce qui établit que l'échange est « fonctionnel » au sens du support, plutôt
qu'un endpoint gRPC présent mais jamais appelé.

Erreurs démontrables :

| Situation | Statut gRPC |
|---|---|
| Coordonnées hors grille ou malformées | `InvalidArgument` |
| Partie inconnue | `NotFound` |
| Case déjà visée, pas son tour, partie terminée | `FailedPrecondition` |

L'endpoint HTTP `POST /games/{id}/shots` est **conservé** : il sert les tests
d'intégration et les essais manuels de `api.http`, et donne à cet ADR un point
de comparaison réel plutôt que théorique.

## Conséquences
- Un contrat `.proto` à maintenir, partagé par les deux projets
  (`GrpcServices="Server"` côté API, `"Client"` côté App).
- `Grpc.Tools` en `PrivateAssets="all"` : c'est un outil de build, pas une
  dépendance d'exécution.
- gRPC-Web impose une configuration d'hébergement supplémentaire —
  `UseGrpcWeb()`, `EnableGrpcWeb()` — et une politique CORS compatible. C'est la
  principale source de perte de temps anticipée sur cet item.
- **Deux chemins mènent au même tir. Ils ne doivent pas diverger** : les deux
  délèguent au même appel du domaine, aucune règle n'est réimplémentée dans le
  service gRPC.
- Le front dépend de gRPC-Web pour sa fonction centrale : si le transport casse,
  le jeu casse. Mitigation : l'endpoint HTTP reste testé et permet d'isoler un
  incident au transport plutôt qu'au domaine.
- La correspondance entre statuts gRPC et codes HTTP doit rester cohérente —
  `FailedPrecondition` correspond à notre 409, `InvalidArgument` à notre 400.

## Vérification et réexamen
- Les trois erreurs du tableau sont démontrables depuis le navigateur.
- Un test vérifie que le chemin HTTP et le chemin gRPC produisent le **même
  résultat** pour un même tir sur une même partie. C'est ce test qui détecterait
  une règle réimplémentée dans le service gRPC.
- Le fonctionnement dans le navigateur — et non seulement en test — fait partie
  de la vérification (diapo 52).

À réexaminer si la configuration gRPC-Web devenait un blocage : l'option (a)
resterait conforme au socle, au prix d'un ADR beaucoup moins défendable.

## Références
- Diapos 35, 48, 49, 50, 51, 52 du support de cours
- `AGENTS.md` § 5
