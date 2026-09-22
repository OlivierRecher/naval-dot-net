using System.Runtime.CompilerServices;

// BoardTests exerce Place et Receive directement : ce sont les regles de
// placement et de resolution de tir, testees sans monter une partie autour.
// L'attribut n'ajoute aucune dependance a Domain — voir AGENTS.md § 4.
[assembly: InternalsVisibleTo("BattleShip.Tests")]
