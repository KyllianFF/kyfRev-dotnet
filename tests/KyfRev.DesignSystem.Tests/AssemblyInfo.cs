// Chaque test d'interface tourne sur son propre fil STA. WPF ne sait pas lire le BAML de
// plusieurs fils à la fois (cache de schéma partagé, KeyNotFoundException au hasard) : ces
// tests passent l'un après l'autre. L'application, elle, n'a qu'un fil d'interface.
[assembly: DoNotParallelize]
