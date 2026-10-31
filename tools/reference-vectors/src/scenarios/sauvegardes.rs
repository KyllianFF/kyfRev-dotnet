//! Opérations des scénarios sur les sauvegardes, les exports, le partage d'une fiche, l'import
//! d'un dossier de notes et les réparations de « Santé du coffre ».
//!
//! Ce qui est écrit hors du coffre l'est dans `attendu.dehors/`, effacé à la fin : les résultats
//! en donnent le contenu (empreinte de chaque entrée des archives, texte des autres fichiers).
//! Un chemin « coffre/… » désigne un emplacement dans le coffre, pour vérifier les refus.

use std::fs;
use std::io::Read as _;
use std::path::{Path, PathBuf};

use coffre::sauvegardes::{self, PolitiqueDeRotation};
use coffre::{CheminRelatif, ErreurCoffre, EtatAncrage, Export, FormatPartage, empreinte};
use jiff::civil::DateTime;
use noyau::Moment;
use serde_json::Value;

use super::{Partie, json, liste};

fn optionnel(texte: Option<&str>) -> String {
    texte.map_or_else(|| "null".to_owned(), json)
}

fn nom(chemin: &Path) -> String {
    chemin.file_name().map(|n| n.to_string_lossy().into_owned()).unwrap_or_default()
}

fn horodatage(operation: &Value) -> DateTime {
    operation["horodatage"].as_str().expect("« horodatage »").parse().expect("AAAA-MM-JJTHH:MM:SS")
}

fn nombre(operation: &Value, cle: &str) -> usize {
    usize::try_from(operation[cle].as_u64().unwrap_or_else(|| panic!("« {cle} » attendu"))).expect("nombre")
}

fn etat(etat: EtatAncrage) -> String {
    format!("{etat:?}")
}

fn trier(mut v: Vec<String>) -> String {
    v.sort();
    v.join(";")
}

/// Entrées d'une archive, dans leur ordre : « nom:empreinte ».
fn contenu_archive(chemin: &Path) -> String {
    let mut archive = zip::ZipArchive::new(fs::File::open(chemin).expect("archive")).expect("zip");
    (0..archive.len())
        .map(|i| {
            let mut entree = archive.by_index(i).expect("entrée");
            let nom = entree.name().expect("nom d'entrée").into_owned();
            let mut octets = Vec::new();
            entree.read_to_end(&mut octets).expect("lecture");
            format!("{nom}:{}", empreinte(&octets))
        })
        .collect::<Vec<_>>()
        .join(";")
}

/// Un fichier écrit hors du coffre : son nom, puis le contenu d'une archive ou le texte.
fn resume(chemin: &Path) -> String {
    if chemin.extension().is_some_and(|e| e == "zip") {
        format!("{} [{}]", nom(chemin), contenu_archive(chemin))
    } else {
        let texte = String::from_utf8(fs::read(chemin).expect("lecture")).expect("UTF-8");
        format!("{} {}", nom(chemin), json(&texte))
    }
}

/// Tous les fichiers sous `racine`, triés : « chemin:empreinte ».
fn arborescence(racine: &Path) -> String {
    fn lister(racine: &Path, courant: &Path, fichiers: &mut Vec<String>) {
        for entree in fs::read_dir(courant).expect("dossier").flatten() {
            let chemin = entree.path();
            if entree.file_type().expect("type").is_dir() {
                lister(racine, &chemin, fichiers);
            } else {
                let relatif = chemin.strip_prefix(racine).expect("relatif").to_string_lossy().replace('\\', "/");
                fichiers.push(format!("{relatif}:{}", empreinte(&fs::read(&chemin).expect("lecture"))));
            }
        }
    }
    let mut fichiers = Vec::new();
    lister(racine, racine, &mut fichiers);
    trier(fichiers)
}

impl Partie {
    /// Dossier « hors du coffre » du scénario, à côté de `attendu/`.
    pub(super) fn dehors(&self) -> PathBuf {
        self.racine.with_extension("dehors")
    }

    #[allow(clippy::too_many_lines)]
    pub(super) fn jouer_sauvegardes(
        &mut self,
        op: &str,
        operation: &Value,
        moment: Option<Moment>,
    ) -> Result<String, ErreurCoffre> {
        let maintenant = || moment.expect("« le » attendu");
        let dehors = self.dehors();
        // « coffre/… » désigne un emplacement dans le coffre (pour les refus), le reste est dehors.
        let chemin = |partie: &Self, cle: &str| {
            let texte = partie.texte(operation, cle);
            match texte.strip_prefix("coffre/") {
                Some(dans_le_coffre) => partie.racine.join(dans_le_coffre),
                None => dehors.join(texte),
            }
        };
        match op {
            "ecrire_dehors" => {
                let fichier = chemin(self, "chemin");
                fs::create_dir_all(fichier.parent().expect("parent")).expect("dossier");
                let octets = match operation.get("octets").and_then(Value::as_array) {
                    Some(octets) => octets.iter().map(|o| u8::try_from(o.as_u64().expect("octet")).expect("octet")).collect(),
                    None => self.texte(operation, "contenu").into_bytes(),
                };
                fs::write(&fichier, octets).expect("écriture");
                Ok(String::new())
            }
            "sauvegarder" => {
                let dossier = chemin(self, "dossier");
                let sauvegarde = self.coffre().sauvegarder(&dossier, horodatage(operation))?;
                Ok(format!("{} {} {}", nom(&sauvegarde.chemin), sauvegarde.horodatage, sauvegarde.nombre_de_fichiers))
            }
            "archive" => Ok(contenu_archive(&chemin(self, "chemin"))),
            "sauvegardes" => Ok(sauvegardes::lister_sauvegardes(&chemin(self, "dossier"))?
                .iter()
                .map(|(h, c)| format!("{h} {}", nom(c)))
                .collect::<Vec<_>>()
                .join(";")),
            "sauvegarde_necessaire" => {
                sauvegardes::sauvegarde_quotidienne_necessaire(&chemin(self, "dossier"), maintenant().jour).map(|b| b.to_string())
            }
            "rotation" => {
                let politique = PolitiqueDeRotation {
                    jours: nombre(operation, "jours"),
                    semaines: nombre(operation, "semaines"),
                    mois: nombre(operation, "mois"),
                };
                let retirees = sauvegardes::appliquer_rotation(&chemin(self, "dossier"), politique)?;
                Ok(retirees.iter().map(|c| nom(c)).collect::<Vec<_>>().join(";"))
            }
            "restaurer_coffre" => {
                let (sauvegarde, parent) = (chemin(self, "sauvegarde"), chemin(self, "dossier"));
                let restaure = sauvegardes::restaurer(&sauvegarde, &parent, maintenant().jour)?;
                Ok(format!("{} {}", json(&nom(&restaure)), arborescence(&restaure)))
            }
            "restaurer_fiche_sauvegarde" => {
                let (id, sauvegarde) = (self.fiche(operation), chemin(self, "sauvegarde"));
                self.coffre().restaurer_fiche(&id, &sauvegarde, &maintenant()).map(|c| c.to_string())
            }
            "exporter" => {
                let genre = match operation["genre"].as_str() {
                    Some("suivi") => Export::Suivi,
                    Some("annotations") => Export::Annotations,
                    Some("coffre") => Export::Coffre,
                    _ => Export::Citations,
                };
                let dossier = chemin(self, "dossier");
                fs::create_dir_all(&dossier).expect("dossier");
                let crees = self.coffre().exporter(genre, &dossier, horodatage(operation))?;
                Ok(crees.iter().map(|c| resume(c)).collect::<Vec<_>>().join(";"))
            }
            "partager" => {
                let format = match operation["format"].as_str() {
                    Some("markdown") => FormatPartage::Markdown,
                    Some("markdown_images") => FormatPartage::MarkdownImages,
                    Some("texte") => FormatPartage::Texte,
                    _ => FormatPartage::Html,
                };
                let (id, dossier, contenu) = (self.fiche(operation), chemin(self, "dossier"), self.option(operation, "contenu"));
                fs::create_dir_all(&dossier).expect("dossier");
                let fichier = self.coffre().partager_fiche(&id, format, &dossier, contenu.as_deref())?;
                Ok(resume(&fichier))
            }
            "importer" => {
                let (source, nom_dossier) = (chemin(self, "source"), self.texte(operation, "nom_dossier"));
                let suivre = operation["suivre"].as_bool().expect("« suivre »");
                let connues: Vec<String> = self.coffre().fiches().map(|e| e.suivie.id.clone()).collect();
                let bilan = self.coffre().importer_dossier(&source, &nom_dossier, suivre, &maintenant())?;
                // Une seule note mise en révision par import : des identifiants tirés dans la même
                // milliseconde ne sont pas forcément dans l'ordre.
                let nouvelles: Vec<String> =
                    self.coffre().fiches().map(|e| e.suivie.id.clone()).filter(|id| !connues.contains(id)).collect();
                assert!(nouvelles.len() <= 1, "une seule note suivie par import dans un scénario");
                for id in nouvelles {
                    self.nouvel_identifiant(&id);
                    if let Some(nom) = operation.get("nom").and_then(Value::as_str) {
                        self.noms.insert(nom.to_owned(), id);
                    }
                }
                Ok(format!(
                    "{} {} {} {} {} {}",
                    json(&bilan.dossier),
                    bilan.fiches,
                    bilan.suivies,
                    bilan.ressources,
                    bilan.liens,
                    liste(&bilan.ignores)
                ))
            }
            "rapport_sante" => {
                let r = self.coffre().rapport_sante();
                let concernee = |f: &coffre::FicheConcernee| format!("{} {} {}", f.id, json(&f.titre), f.chemin);
                let passage = |p: &coffre::PassageConcerne| format!("{} {} {} {} {}", p.id, p.fiche_id, json(&p.titre), json(&p.exact), etat(p.etat));
                Ok(format!(
                    "problemes={} deplacees=[{}] modifiees=[{}] manquantes=[{}] doublons=[{}] non_suivies=[{}] sans_suivi=[{}] illisibles=[{}] liens=[{}] liens_casses=[{}] orphelines=[{}] a_verifier=[{}]",
                    r.problemes,
                    trier(r.deplacees.iter().map(|d| format!("{} {} {}", json(&d.titre), d.de, d.vers)).collect()),
                    trier(r.modifiees_hors_application.iter().map(concernee).collect()),
                    trier(r.manquantes.iter().map(concernee).collect()),
                    trier(r.doublons.iter().map(|d| format!("{} {} {}", d.id, json(&d.titre), d.chemins.join(","))).collect()),
                    trier(r.non_suivies.clone()),
                    trier(r.sans_suivi.iter().map(concernee).collect()),
                    // La raison vient de l'analyseur YAML, propre à chaque version : seul le chemin compte.
                    trier(r.fichiers_illisibles.iter().map(|f| f.chemin.clone()).collect()),
                    trier(r.liens_a_verifier.clone()),
                    r.liens_casses.iter().map(|l| format!("{} {} {}", l.fiche_id, json(&l.titre), json(&l.cible))).collect::<Vec<_>>().join(";"),
                    trier(r.annotations_orphelines.iter().map(passage).collect()),
                    r.annotations_a_verifier.iter().map(passage).collect::<Vec<_>>().join(";"),
                ))
            }
            "garder_original" => {
                let id = self.fiche(operation);
                let original = CheminRelatif::nouveau(&self.texte(operation, "original"))?;
                let nouveaux = self.coffre().garder_original(&id, &original, &maintenant())?;
                for nouveau in &nouveaux {
                    self.nouvel_identifiant(nouveau);
                }
                if let (Some(nom), Some(premier)) = (operation.get("nom").and_then(Value::as_str), nouveaux.first()) {
                    self.noms.insert(nom.to_owned(), premier.clone());
                }
                Ok(nouveaux.join(";"))
            }
            "recreer_suivi" => {
                let chemin = CheminRelatif::nouveau(&self.texte(operation, "chemin"))?;
                self.coffre().recreer_suivi(&chemin, &maintenant())
            }
            "annotations_corbeille" => Ok(self
                .coffre()
                .annotations_a_la_corbeille()
                .iter()
                .map(|e| {
                    format!(
                        "{} {} {} {} {} {}",
                        e.id,
                        e.fiche_id,
                        json(&e.titre),
                        json(&e.exact),
                        optionnel(e.commentaire.as_deref()),
                        e.corbeille_le
                    )
                })
                .collect::<Vec<_>>()
                .join(";")),
            autre => panic!("opération inconnue : {autre}"),
        }
    }
}
