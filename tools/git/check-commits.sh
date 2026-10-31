#!/bin/sh
# Vérifie l'auteur, le committer et le message de chaque commit d'une plage.
# Usage : sh tools/git/check-commits.sh <plage>   (par exemple « origin/main..HEAD »)
set -eu

racine=$(git rev-parse --show-toplevel)
. "$racine/tools/git/rules.sh"

plage=$1
echec=0
nombre=0

for sha in $(git rev-list "$plage"); do
    nombre=$((nombre + 1))
    court=$(git rev-parse --short "$sha")
    verifier_identite "$(git log -1 --format=%an "$sha")" "$(git log -1 --format=%ae "$sha")" "Auteur du commit $court" || echec=1
    verifier_identite "$(git log -1 --format=%cn "$sha")" "$(git log -1 --format=%ce "$sha")" "Committer du commit $court" || echec=1
    if ! git log -1 --format=%B "$sha" | verifier_message; then
        erreur "commit $court : message à corriger."
        echec=1
    fi
done

if [ "$echec" -ne 0 ]; then
    exit 1
fi
printf 'kyfRev : %s commit(s) vérifié(s), identité et messages conformes.\n' "$nombre"
