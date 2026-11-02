# Politique de sécurité

## Signaler une vulnérabilité

Signalez toute vulnérabilité en privé, avec [le formulaire de signalement du dépôt](https://github.com/KyllianFF/kyfRev-dotnet/security/advisories/new),
sans la décrire dans une issue. Indiquez la version concernée, les étapes pour reproduire et
l'impact estimé.

## Versions suivies

| Version | Correctifs de sécurité |
|---|---|
| 1.x (en développement) | Oui |
| 0.x (application Tauri, dépôt `kyfRev`) | Jusqu'à la sortie de la 1.0.0 |

## Principes appliqués

- **Données au repos** : coffre en fichiers ordinaires ; chiffrement facultatif par coffre
  (AES-256-GCM, clé dérivée du mot de passe maître par Argon2id ou PBKDF2), clé de
  récupération, sauvegardes chiffrées avec le coffre.
- **Contenu des fiches** : jamais exécuté. La lecture est native ; le moteur web caché, réservé
  aux diagrammes et aux extensions, fonctionne sans réseau ni accès aux fichiers.
- **Extensions** : isolées, sans réseau, permissions contrôlées par l'application, catalogue signé.
- **Réseau** : aucune connexion sans action ou réglage explicite ; HTTPS uniquement, sans
  redirection, réponses de taille bornée.
- **Secrets** : aucun secret dans le dépôt (gitleaks en crochet et en intégration continue) ;
  jeton GitHub protégé par DPAPI sur le poste.
- **Chaîne d'approvisionnement** : versions figées, délai de 14 jours avant adoption, audit
  NuGet bloquant, actions épinglées par empreinte.

Le modèle de menace détaillé est dans [docs/plan-de-refonte.md](docs/plan-de-refonte.md) (§2.8).
