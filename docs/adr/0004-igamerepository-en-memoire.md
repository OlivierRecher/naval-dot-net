# ADR 0004 : abstraire le stockage derrière `IGameRepository` dès le socle

## Statut et date
Accepté — 2026-09-15.

## Contexte
Le socle n'a besoin d'aucune persistance : une partie contre un bot vit le temps
de la session. Mais le backlog prévoit persistance, historique et statistiques
(`AGENTS.md` § 10, item 5), qui imposeront une base de données.

La question est donc de savoir s'il faut introduire une abstraction **avant**
d'en avoir l'usage — ce qui va à l'encontre du réflexe « ne pas abstraire par
anticipation ».

## Options envisagées

**(a) `ConcurrentDictionary<Guid, Game>` directement dans les endpoints.** Le
minimum de code. Mais le jour de la bascule vers SQLite, chaque endpoint est
réécrit, et les tests d'intégration avec.

**(b) `IGameRepository` + implémentation en mémoire.** Une interface de quatre
méthodes et une implémentation triviale maintenant ; la bascule ultérieure ne
touche aucun endpoint.

**(c) EF Core + SQLite dès le socle.** Supprime la migration future, mais ajoute
migrations, `DbContext` et configuration d'hébergement à la charge la plus lourde
du projet, pour une fonctionnalité que personne ne demande encore.

## Décision
Option **(b)**.

`IGameRepository` est défini dans `BattleShip.Domain` — c'est le domaine qui
exprime son besoin de persistance, pas l'infrastructure qui l'impose.
L'implémentation en mémoire vit dans `BattleShip.API` et est enregistrée en
**Singleton**.

L'anticipation est ici justifiée par un fait, pas par un principe : l'item 5 est
**déjà dans le backlog validé**. Ce n'est pas une abstraction spéculative.

## Conséquences
- La bascule vers SQLite consistera à écrire une seconde implémentation et à
  changer une ligne d'enregistrement. Aucun endpoint modifié.
- Les tests d'intégration peuvent injecter une implémentation de test sans monter
  de base de données.
- Les parties sont perdues au redémarrage du serveur. Accepté pour le socle,
  résolu par l'item 5.
- **Le cycle de vie Singleton impose une implémentation thread-safe.** C'est la
  raison du `ConcurrentDictionary` plutôt qu'un `Dictionary` : deux requêtes HTTP
  concurrentes touchent la même instance. Point à savoir expliquer — les durées
  de vie de l'injection de dépendances font partie des sujets du QCM.
- `Domain` ne doit pas pour autant gagner de dépendance : l'interface ne
  manipule que des types du domaine, jamais `DbContext` ni `IQueryable`.

## Vérification et réexamen
- Aucun `ConcurrentDictionary` n'apparaît en dehors de l'implémentation :
  vérifiable par recherche textuelle.
- Les endpoints ne dépendent que de `IGameRepository`, jamais du type concret.
- `Endpoints_RunAgainstASubstitutedRepository_WithoutAnyEndpointChange` :
  une implémentation de test, volontairement non concurrente et distincte de
  celle de production, est injectée ; les quatre endpoints fonctionnent sans
  qu'une ligne change, et les compteurs du double établissent que la partie a
  bien transité par lui. C'est ce test qui **prouve** que l'abstraction sert à
  quelque chose. Sans lui, cet ADR ne serait qu'une intention.
- Contrôle de mutation exécuté : supprimer l'appel `repository.Add(game)` dans
  l'endpoint de création fait échouer ce test.

À réexaminer si l'item 5 sortait du périmètre : l'abstraction deviendrait alors
du code mort, et l'option (a) serait la bonne.

## Références
- Diapos 33, 63 du support de cours
- `AGENTS.md` § 4, § 10
