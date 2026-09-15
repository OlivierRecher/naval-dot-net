# ADR 0006 : valider par filtre d'endpoint et intercepteur gRPC plutôt que par appel manuel

## Statut et date
Accepté — 2026-09-15.

## Contexte
FluentValidation est imposé sur les entrées serveur. Le support montre l'appel
manuel dans le corps de chaque endpoint (diapo 34) et dans chaque méthode de
service gRPC (diapo 50) :

```csharp
var check = await validator.ValidateAsync(input);
if (!check.IsValid)
    return TypedResults.ValidationProblem(check.ToDictionary());
```

Répété sur cinq endpoints HTTP et les méthodes gRPC, ce bloc devient du bruit —
et surtout, **rien ne signale l'endpoint où on aura oublié de l'écrire**.

## Options envisagées

**(a) Suivre le support littéralement.** Aucun écart à justifier, mécanisme
parfaitement explicite et facile à expliquer au QCM. Mais la répétition est
relevée par le critère « lisibilité, cohérence », et un oubli est silencieux.

**(b) `IEndpointFilter` générique côté HTTP + `Interceptor` côté gRPC.** La
validation devient transversale : elle s'applique par construction, et la
réponse d'erreur est identique partout.

**(c) Un paquet NuGet d'auto-validation.** Supprime le code à écrire, mais ajoute
une dépendance et un comportement implicite qu'il faudrait savoir expliquer sans
l'avoir écrit — mauvais compromis dans un projet noté sur la maîtrise.

## Décision
Option **(b)**.

Un filtre générique résout `IValidator<T>` pour le type du corps de requête et
retourne `ValidationProblem` en cas d'échec. Un intercepteur gRPC fait le même
travail et lève `RpcException(StatusCode.InvalidArgument)`.

**Cet écart au support est délibéré et revendiqué.** Le support demande de
justifier ses décisions plutôt que de recopier ses exemples ; l'écart tracé ici
vaut mieux qu'une conformité non réfléchie.

## Conséquences
- Un oubli de validation devient impossible sur un endpoint qui déclare un
  corps de requête typé : c'est le mécanisme qui garantit la règle, pas la
  vigilance.
- Réponse `ValidationProblem` uniforme, donc un seul format d'erreur à gérer
  côté front.
- Le filtre manipule le type de requête de façon générique. **Ce code doit être
  compris et explicable par les deux membres du binôme**, sous peine de
  retourner l'argument de l'option (c) contre nous. L'injection de dépendances
  et la liaison des paramètres en Minimal API sont au programme du QCM.
- Deux mécanismes distincts à écrire et maintenir — filtre HTTP et intercepteur
  gRPC — parce que les deux pipelines n'ont rien en commun.
- Les règles de validation elles-mêmes restent des `AbstractValidator<T>`
  ordinaires, testables isolément.

## Vérification et réexamen
- Un test d'intégration envoie une entrée invalide à un endpoint **sans appel de
  validation dans son corps** et attend un 400 : c'est le seul contrôle qui
  prouve que le filtre agit réellement.
- Un test équivalent côté gRPC attend `InvalidArgument`.
- Contrôle de mutation : retirer l'enregistrement du filtre doit faire échouer
  ces deux tests. Tant que cette manipulation n'a pas été faite, on ne sait pas
  si les tests passent grâce au filtre ou par accident.

À réexaminer si le filtre générique s'avérait plus difficile à expliquer qu'à
écrire : l'option (a), verbeuse mais limpide, resterait acceptable.

## Références
- Diapos 34, 40, 50 du support de cours
- `AGENTS.md` § 5
