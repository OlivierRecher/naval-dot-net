# ADR 0001 : séparer `BattleShip.Domain` de `BattleShip.Models`

## Statut et date
Accepté — 2026-09-15.

## Contexte
Le support impose quatre projets (`API`, `App`, `Models`, `Tests`) et une règle
de dépendance : `Models` ne dépend d'aucun autre projet, et « le moteur reste
indépendant de HTTP, JSON et gRPC » (diapo 28). Il demande par ailleurs un
moteur « exécutable et testable indépendamment des transports et de
l'interface » (diapo 36).

Or `Models` est référencé **à la fois** par l'API et par le front Blazor. Y
loger le moteur de jeu revient à embarquer les règles, le placement des flottes
et l'algorithme du bot dans le bundle WebAssembly téléchargé par le navigateur —
alors que ces règles ne doivent s'exécuter que côté serveur (diapo 63 : « les
règles sont vérifiées côté serveur »).

## Options envisagées

**(a) Quatre projets, moteur dans `Models`.** Conforme à la lettre du support.
Mais `Models` mélange deux responsabilités — les données échangées à la
frontière et les règles du jeu — et le front reçoit du code qu'il n'exécute
jamais. Le critère « qualité du code : responsabilités » est explicitement noté.

**(b) Cinq projets : `Domain` pour le moteur, `Models` pour les DTO.** Le
support autorise « les éventuels projets supplémentaires » (diapo 62) et laisse
l'organisation interne libre (diapo 29). Coût : un projet de plus, et une
traduction `Domain` → DTO à la frontière de l'API.

**(c) Moteur dans le projet `API`.** Supprime un projet mais rend le moteur
indissociable de l'hébergement ASP.NET : les tests métier chargent alors tout le
pipeline HTTP, ce que la diapo 36 demande précisément d'éviter.

## Décision
Option **(b)**.

```
BattleShip.Domain   moteur pur — aucune dépendance
BattleShip.Models   DTO de frontière — aucune dépendance
BattleShip.API      → Domain, Models
BattleShip.App      → Models          (jamais Domain)
BattleShip.Tests    → Domain, API
```

## Conséquences
- Les tests métier s'exécutent sans hôte web ni sérialisation.
- Le navigateur ne reçoit jamais les règles ni l'algorithme du bot.
- Une traduction `Domain` → DTO est nécessaire dans l'API. C'est du code en
  plus, assumé : c'est le prix de la frontière explicite.
- La liste de commandes de la diapo 28 n'est plus suivie littéralement. L'écart
  est volontaire et tracé ici.
- Toute dépendance ajoutée à `Domain` invalide cet ADR.

## Vérification et réexamen
- `BattleShip.Domain.csproj` ne contient ni `<ProjectReference>` ni
  `<PackageReference>` : vérifiable par lecture directe du fichier.
- `BattleShip.App.csproj` ne référence pas `Domain`.
- Les tests de `Tests/Domain/` n'utilisent aucun type ASP.NET.

À réexaminer si le front devait un jour exécuter des règles localement — par
exemple pour une prévisualisation de placement hors ligne. Dans ce cas, extraire
uniquement les règles concernées dans un troisième projet partagé, et non
déplacer le moteur entier.

## Références
- Diapos 28, 29, 36, 62 du support de cours
- `AGENTS.md` § 4
