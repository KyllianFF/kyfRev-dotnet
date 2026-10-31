#!/bin/sh
# Vérifie que main ne reçoit que des fusions sans avance rapide (--no-ff) de branches conformes.
# Le commit racine du dépôt est la seule exception.
# Usage : sh tools/git/check-main-merges.sh <plage>
set -eu

racine=$(git rev-parse --show-toplevel)
. "$racine/tools/git/rules.sh"

plage=$1
echec=0

for sha in $(git rev-list --first-parent "$plage"); do
    court=$(git rev-parse --short "$sha")
    sujet=$(git log -1 --format=%s "$sha")
    parents=$(git log -1 --format=%P "$sha" | wc -w | tr -d ' ')
    case "$parents" in
        0)
            ;;
        2)
            if ! printf '%s\n' "$sujet" | grep -qE "$KYFREV_MOTIF_FUSION"; then
                erreur "fusion $court refusée sur main : « $sujet » ne fusionne pas une branche conforme."
                echec=1
            fi
            ;;
        *)
            erreur "commit $court (« $sujet ») fait directement sur main : seules les fusions --no-ff y sont admises."
            echec=1
            ;;
    esac
done

if [ "$echec" -ne 0 ]; then
    exit 1
fi
printf 'kyfRev : historique de main conforme.\n'
