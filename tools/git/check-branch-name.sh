#!/bin/sh
# Vérifie qu'un nom de branche suit la convention du dépôt.
# Usage : sh tools/git/check-branch-name.sh <branche>
set -eu

racine=$(git rev-parse --show-toplevel)
. "$racine/tools/git/rules.sh"

if ! printf '%s\n' "$1" | grep -qE "$KYFREV_MOTIF_BRANCHE"; then
    erreur "branche « $1 » refusée : le nom doit être de la forme <type>/<sujet>, avec <type> parmi"
    erreur "feature, fix, hotfix, release, chore, refactor, docs, test (sujet en minuscules, chiffres, . _ -)."
    exit 1
fi
printf 'kyfRev : nom de branche « %s » conforme.\n' "$1"
