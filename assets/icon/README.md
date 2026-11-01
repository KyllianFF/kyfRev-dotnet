# Icône de kyfRev

| Fichier | Rôle |
|---|---|
| `kyfrev.svg` | Source unique de l'icône de Windows : un dessin principal et, dans `defs`, ses versions simplifiées pour les petites tailles. |
| `kyfrev.ico` | Généré : de 16 à 256 px, pour l'application et l'installateur. |
| `kyfrev-plein-ivoire.svg`, `kyfrev-plein-foret.svg` | Le même éventail avec un fond. Gardés pour une éventuelle application mobile ou macOS, rien ne les génère ni ne les emploie pour l'instant. |

Après toute modification de `kyfrev.svg`, régénérer le `.ico` et les images de l'assistant
(`installer/images`), puis les versionner, à la racine du dépôt :

```powershell
dotnet run tools/icon/icon.cs
```

L'outil comprend le sous-ensemble du SVG employé ici : `rect`, `circle`, `path`, `fill`, `stroke`,
`stroke-width` et `transform="rotate(a x y)"`. Aucune image n'est retouchée à la main.
