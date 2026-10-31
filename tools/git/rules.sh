# Règles de gouvernance du dépôt, partagées par les crochets git et l'intégration continue.
# Ce fichier est chargé par les autres scripts (`. tools/git/rules.sh`), jamais exécuté seul.

KYFREV_NOM="KyllianFF"
KYFREV_COURRIEL="113480351+KyllianFF@users.noreply.github.com"
KYFREV_PREFIXES="feature|fix|hotfix|release|chore|refactor|docs|test"
KYFREV_MOTIF_BRANCHE="^($KYFREV_PREFIXES)/[a-z0-9][a-z0-9._-]*\$"
KYFREV_MOTIF_FUSION="^Merge branch '($KYFREV_PREFIXES)/[a-z0-9][a-z0-9._-]*'\$"

erreur() {
    printf 'kyfRev : %s\n' "$*" >&2
}

# verifier_identite <nom> <courriel> <rôle>
verifier_identite() {
    if [ "$1" != "$KYFREV_NOM" ] || [ "$2" != "$KYFREV_COURRIEL" ]; then
        erreur "$3 « $1 <$2> » refusé : seul « $KYFREV_NOM <$KYFREV_COURRIEL> » est admis."
        return 1
    fi
}

# verifier_message : lit un message de commit sur l'entrée standard.
# Refuse les co-auteurs et toute ligne finale (trailer) portant une adresse web.
verifier_message() {
    message=$(cat)
    if printf '%s\n' "$message" | grep -qiE '^co-authored-by:'; then
        erreur "message refusé : la mention d'un co-auteur n'est pas admise."
        return 1
    fi
    if printf '%s\n' "$message" | git interpret-trailers --parse | grep -qiE 'https?://'; then
        erreur "message refusé : une ligne finale (trailer) contient une adresse web."
        return 1
    fi
}
