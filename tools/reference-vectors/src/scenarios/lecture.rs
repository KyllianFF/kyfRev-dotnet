//! Opérations des scénarios sur la lecture : la fiche ouverte (`ouvrir_fiche`) et l'état de
//! révision d'une fiche ou d'un passage (`etat_de`), au jour donné.

use coffre::{AnnotationVue, ErreurCoffre, EtatRevision, FicheOuverte, Genre};
use noyau::{Jour, Moment};
use serde_json::Value;

use super::{Partie, json, liste};

fn jour(jour: Option<Jour>) -> String {
    jour.map_or_else(|| "-".to_owned(), |j| j.to_string())
}

fn optionnel(texte: Option<&str>) -> String {
    texte.map_or_else(|| "null".to_owned(), json)
}

/// État de révision, sur une ligne (même format côté C#).
pub(super) fn etat(e: &EtatRevision) -> String {
    let etapes: Vec<String> =
        e.etapes.iter().map(|x| format!("{}:{}:{}", x.libelle, jour(x.faite_le), x.retard_jours)).collect();
    format!(
        "{:?} [{}] {} {} {} {} {} {} {} {} {} {} {} {} {}",
        e.statut,
        etapes.join(","),
        e.etape.as_deref().unwrap_or("-"),
        jour(e.echeance),
        e.cycle.map_or_else(|| "-".to_owned(), |c| c.to_string()),
        jour(e.derniere_revision),
        e.retard_jours,
        e.revise_aujourdhui,
        jour(e.archivee_le),
        e.rappel_en_attente,
        e.inclus_dans_rappels,
        jour(e.eligible_le),
        e.demarre_aujourdhui,
        e.cycles_commences,
        e.cycles_termines
    )
}

fn annotation(a: &AnnotationVue) -> String {
    format!(
        "{} {} {} {:?} {} <{}>",
        a.id,
        optionnel(a.commentaire.as_deref()),
        optionnel(a.definition.as_ref().map(|d| d.terme.as_str())),
        a.etat_ancrage,
        a.dans_la_corbeille,
        etat(&a.revision)
    )
}

fn fiche_ouverte(f: &FicheOuverte) -> String {
    format!(
        "{} {} {} {} {} {} {} {} {} {} <{}> [{}]",
        f.id,
        json(&f.titre),
        f.chemin,
        json(&f.dossier),
        liste(&f.etiquettes),
        json(&f.corps),
        f.premiere_ligne_corps,
        f.empreinte,
        jour(f.creee_le),
        f.modifiee_le,
        etat(&f.revision),
        f.annotations.iter().map(annotation).collect::<Vec<_>>().join(";")
    )
}

impl Partie {
    pub(super) fn jouer_lecture(&mut self, op: &str, operation: &Value, moment: Option<Moment>) -> Result<String, ErreurCoffre> {
        let jour_de = || moment.expect("« le » attendu").jour;
        match op {
            "ouvrir_fiche" => {
                let id = self.fiche(operation);
                self.coffre().ouvrir_fiche(&id, jour_de()).map(|f| fiche_ouverte(&f))
            }
            "etat_de" => {
                let (genre, cle) = match operation["genre"].as_str() {
                    Some("passage") => (Genre::Passage, "annotation"),
                    _ => (Genre::Fiche, "fiche"),
                };
                let nom = operation[cle].as_str().expect("élément attendu").to_owned();
                let id = self.noms.get(&nom).cloned().unwrap_or(nom);
                self.coffre().etat_de(genre, &id, jour_de()).map(|e| etat(&e))
            }
            autre => panic!("opération inconnue : {autre}"),
        }
    }
}
