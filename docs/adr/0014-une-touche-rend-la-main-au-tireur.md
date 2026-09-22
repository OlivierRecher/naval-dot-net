# ADR 0014 : une touche rend la main au tireur, et ce que cela coûte au rejeu

## Statut et date
Accepté — 2026-09-16. Rédigé avec le changement de règle du tour.

## Contexte

Depuis le cadrage, `AGENTS.md` § 3 posait :

> **Le tour passe toujours**, touché ou non.

C'était un choix, pas une contrainte du support : la règle la plus répandue de la
bataille navale veut au contraire qu'un joueur qui touche rejoue. La règle
retenue avantageait mécaniquement le premier joueur d'un seul demi-tour et
rendait les parties plus longues ; surtout, elle privait le jeu de sa seule
tension — l'enchaînement après une touche.

Le changement paraît local : une condition dans `Game.FireLocked`. Il ne l'est
pas. « Le tour passe à chaque tir » était une hypothèse tacite du front, des
tests d'intégration et de la stratégie de persistance.

## Décision

Le tour passe **au coup manqué, et seulement là**. Un navire coulé compte comme
une touche : le tireur rejoue.

La règle reste dans le domaine, dans `FireLocked`, **après** le test de fin de
partie — le vainqueur ne rejoue pas. Elle ne touche pas à la seconde règle du § 3 :
une case déjà visée reste une tentative refusée, qui ne consomme pas le tour.

```csharp
if (_waiting.Board.AllShipsSunk)
{
    Status = GameStatus.Finished;
    Winner = shooter;
    return FireOutcome.Accepted(target, result);
}

if (result is ShotResult.Miss)
{
    (_current, _waiting) = (_waiting, _current);
}
```

## Conséquence 1 — « l'humain a tiré » ne veut plus dire « c'est au bot »

C'est l'hypothèse qui était partout.

- **Le front** demandait le tour du bot après chaque tir. Il ne le demande plus
  que sur un raté, et le bot enchaîne alors tant qu'il touche
  (`GameSession.PlayBotTurnsAsync`). Ses tirs sont annoncés d'un bloc : sans
  cela le joueur lirait la même ligne répétée autant de fois que le bot a touché.
- **Le hot-seat** armait la passation à chaque tir. Sur une touche, le tireur
  garde la main : armer la passation laissait l'interface attendre un adversaire
  que le serveur n'appellerait jamais, avec un bouton « Réessayer » incapable de
  réussir. La passation ne s'arme désormais que sur un raté.
- **Les tests d'intégration** alternaient tir humain / tour de bot en aveugle.
  Un tour de bot réclamé hors de son tour répond 409, et l'ignorer faisait
  dériver silencieusement le nombre de tirs. Ils conduisent maintenant la partie
  comme le navigateur : ils lisent la vue pour savoir à qui est le tour
  (`GamePlay` côté API, `Rounds.PlayRound` côté domaine).

## Conséquence 2 — un journal n'est rejouable que sous ses propres règles

C'est le prix réel de ce changement, et il n'était pas prévu.

L'ADR 0012 ne persiste que les **coordonnées** des tirs : « tout le reste — tour
courant, statut, vainqueur — est rejoué, jamais stocké ». Le rejeu déduit donc
qui tirait **en appliquant la règle du tour**. Changer la règle change cette
déduction, donc la grille visée par chaque tir.

Une partie enregistrée sous l'ancienne règle se reconstruit alors soit
différemment — mauvaise flotte endommagée, vainqueur perdu, alors que les
colonnes `Result` et `WinnerName` gardent les anciennes valeurs — soit pas du
tout. Exemple minimal, vérifié :

| Journal | Sous l'ancienne règle | Sous la nouvelle |
|---|---|---|
| `(7,7)` puis `(7,7)` | A touche la grille de B, puis B tire sur celle de A | A touche, garde la main, rejoue la **même** case : refusé |

`Game.Restore` lève alors `InvalidOperationException`, que `GetGame` ne
rattrapait pas : HTTP 500 sur une lecture anodine.

**Options envisagées**

1. **Refus explicite** — le dépôt traduit l'incohérence en
   `UnreplayableJournalException`, un filtre d'endpoint la rend en 409 avec un
   message, et les bases de développement sont purgées.
2. **Rejeu par le tireur** — `ShotRecord.ShooterId` est déjà stocké mais jamais
   relu ; le passer à `Game.Restore` rendrait le rejeu indépendant de la règle.
   Mais le domaine gagnerait une API de rejeu qui **impose** le tireur, ce qui
   défait « le tour est dérivé, jamais stocké » (ADR 0012).
3. **Version de règle en base** — chaque partie porte la règle sous laquelle elle
   a été jouée, et `Game` applique celle-là. Le plus complet, et une branche de
   règle de plus dans le domaine pour un périmètre qui n'en a pas besoin.

**Retenu : l'option 1.** Une partie en cours ne survit pas à un changement de
règle du jeu, et prétendre le contraire serait mentir sur ce qui est rejoué. Le
refus est explicite et nommé ; il n'est pas silencieux.

## Conséquences

- Le tour est déductible du seul journal **tant que la règle ne change pas** —
  c'est la limite que l'ADR 0012 n'énonçait pas.
- Les parties enregistrées avant ce changement répondent 409 au lieu de 500. Elles
  restent lisibles dans l'historique, qui lit des colonnes sans rejouer (ADR 0012).
- Un aller-retour ne porte plus un nombre de tirs connu d'avance : tout test qui
  en comptait doit compter ce qu'il a réellement obtenu.
- **Limite assumée** : le correctif du hot-seat est vérifié par lecture, par le
  contrat d'API (`Fire_InLocalMode_OnAHit_KeepsTheViewOnTheSameHuman`) et dans le
  navigateur — pas par un test automatisé. `BattleShip.App` n'a aucun projet de
  test ; l'ouvrir est une décision d'architecture qui n'a pas été prise ici.
- **Limite assumée** : le bot peut désormais couler une flotte entière en un seul
  aller-retour. C'est la règle qui le veut ; rien ne borne la série.

## Vérification et réexamen

`dotnet build` puis `dotnet test` : **253 tests, 0 échec**, suite exécutée 17 fois
de suite sans un seul échec — les flottes sont placées avec `Random.Shared`, donc
une suite stable sur 17 exécutions est ce qui remplace ici un tirage fixe.

| Contrôle | Résultat |
|---|---|
| Règle du tour cassée (échange inconditionnel) | 8 tests rouges, dont le hot-seat et le rejeu |
| Garde de rejeu retirée | `AJournalWrittenUnderAnotherTurnRule_IsRefusedAtReplay` rouge |
| Filtre 409 retiré | `GetGame_WhenTheJournalNoLongerReplays_Returns409` rouge |
| Touche, humain et bot | Le tireur garde la main, domaine et API |
| Hot-seat sur touche | La vue reste au tireur ; sur raté elle passe à l'autre |
| Journal écrit sous l'ancienne règle | Refusé, 409, message nommant la cause |
| Garde du tireur retirée | `AJournalWhoseShooterDivergesFromTheReplay_IsRefused` rouge |

**Complément du 2026-09-22.** Le refus ci-dessus n'était acquis que lorsque le
tir divergent retombait sur une case **déjà prise**. Sur une case libre, le rejeu
l'acceptait : le tir changeait de tireur et de grille, et la partie rechargée
n'était plus celle qui avait été jouée — sans exception ni 409. `ShooterId` est
écrit à chaque tir et n'était relu par personne ; il est désormais comparé au
tireur déduit au rechargement. Le refus devient vérifié au lieu d'être attrapé
par hasard, et la colonne cesse d'être du poids mort.

À réexaminer si une partie devait survivre à un changement de règle : il faudrait
alors l'option 3, et une colonne de version.

## Références
- ADR 0002 (journal en ajout seul), ADR 0003 (serveur autoritaire), ADR 0011 (hot-seat), ADR 0012 (persistance par rejeu)
- `AGENTS.md` § 3 · `CONTEXT.md`, entrée « Tour »
- `REVUE-IA.md`, revue 11
