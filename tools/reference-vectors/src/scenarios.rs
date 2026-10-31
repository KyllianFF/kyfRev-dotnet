//! Scénarios d'opérations sur un coffre, joués par le crate `coffre` de kyfRev 0.x.
//!
//! Chaque scénario est un dossier qui contient `scenario.json` : une liste d'opérations
//! (celles du coffre, et quelques gestes faits hors de l'application). Ce programme les joue
//! dans `attendu/`, écrit le résultat de chaque opération dans `resultats.txt` et les
//! identifiants créés, dans l'ordre, dans `identifiants.txt`. La suite de tests C# rejoue le
//! scénario avec les mêmes identifiants et exige un coffre identique à l'octet près.

use std::collections::{BTreeSet, HashMap, HashSet};
use std::fmt::Write as _;
use std::fs;
use std::path::{Path, PathBuf};

use coffre::{CheminRelatif, Coffre, ErreurCoffre, PresenceFichier, ResultatEnregistrement};
use jiff::civil::Date;
use jiff::{Timestamp, tz::TimeZone};
use noyau::Moment;
use serde_json::Value;

mod complements;
mod sauvegardes;

fn le(texte: &str) -> Moment {
    let jour: Date = texte.parse().expect("date AAAA-MM-JJ");
    let instant = jour.at(12, 0, 0, 0).to_zoned(TimeZone::UTC).expect("date valide").timestamp();
    Moment::nouveau(jour, instant)
}

/// Nom de variante d'une erreur, et son message s'il ne contient pas de chemin absolu.
fn erreur(erreur: &ErreurCoffre) -> String {
    let debug = format!("{erreur:?}");
    let variante = debug.split([' ', '(']).next().unwrap_or_default().to_owned();
    let comparable = !matches!(
        erreur,
        ErreurCoffre::Io { .. }
            | ErreurCoffre::HorsDuCoffre { .. }
            | ErreurCoffre::PasUnCoffre(_)
            | ErreurCoffre::DossierNonVide(_)
            | ErreurCoffre::DejaExistant(_)
            | ErreurCoffre::EnteteInvalide { .. }
            | ErreurCoffre::SuiviIllisible { .. }
            | ErreurCoffre::ParametresIllisibles(_)
            | ErreurCoffre::SauvegardeDansLeCoffre(_)
            | ErreurCoffre::ExportDansLeCoffre(_)
            | ErreurCoffre::SauvegardeInvalide { .. }
    );
    if comparable { format!("{variante} : {erreur}") } else { variante }
}

fn json(texte: &str) -> String {
    serde_json::to_string(texte).expect("texte JSON")
}

fn liste(textes: &[String]) -> String {
    serde_json::to_string(textes).expect("liste JSON")
}

struct Partie {
    racine: PathBuf,
    coffre: Option<Coffre>,
    noms: HashMap<String, String>,
    identifiants: Vec<String>,
    lectures: HashMap<String, String>,
    dossiers_ranges: BTreeSet<String>,
    vus: HashSet<String>,
}

impl Partie {
    fn texte(&self, operation: &Value, cle: &str) -> String {
        let brut = operation[cle].as_str().unwrap_or_else(|| panic!("« {cle} » attendu : {operation}"));
        self.noms
            .iter()
            .fold(brut.to_owned(), |texte, (nom, id)| texte.replace(&format!("{{{{{nom}}}}}"), id))
    }

    fn option(&self, operation: &Value, cle: &str) -> Option<String> {
        operation.get(cle).filter(|v| !v.is_null()).map(|_| self.texte(operation, cle))
    }

    fn fiche(&self, operation: &Value) -> String {
        let nom = operation["fiche"].as_str().expect("« fiche » attendu");
        self.noms.get(nom).cloned().unwrap_or_else(|| nom.to_owned())
    }

    fn coffre(&mut self) -> &mut Coffre {
        self.coffre.as_mut().expect("coffre ouvert")
    }

    fn empreinte(&mut self, operation: &Value, id: &str) -> String {
        match operation["empreinte"].as_str() {
            Some("actuelle") => self.coffre().lire_fiche(id).map(|c| c.empreinte).unwrap_or_default(),
            Some("derniere_lecture") => self.lectures.get(id).cloned().unwrap_or_default(),
            Some(autre) => autre.to_owned(),
            None => panic!("« empreinte » attendue"),
        }
    }

    fn nouvel_identifiant(&mut self, id: &str) {
        let ulid = id.split_once('_').map_or(id, |(_, ulid)| ulid);
        if self.vus.insert(ulid.to_owned()) {
            self.identifiants.push(ulid.to_owned());
        }
    }

    /// Les dossiers rangés par une mise à la corbeille de dossier portent un ULID.
    fn noter_dossiers_ranges(&mut self) {
        let dossier = self.racine.join("fiches/.supprimees/dossiers");
        let Ok(entrees) = fs::read_dir(&dossier) else { return };
        let mut noms: Vec<String> = entrees.flatten().map(|e| e.file_name().to_string_lossy().into_owned()).collect();
        noms.sort();
        for nom in noms {
            if self.dossiers_ranges.insert(nom.clone()) {
                self.identifiants.push(nom);
            }
        }
    }

    fn resultat_enregistrement(resultat: &ResultatEnregistrement) -> String {
        match resultat {
            ResultatEnregistrement::Enregistre { empreinte } => format!("enregistre {empreinte}"),
            ResultatEnregistrement::Conflit { empreinte_disque, corps_disque } => {
                format!("conflit {empreinte_disque} {}", json(corps_disque))
            }
        }
    }

    fn sante(&self) -> String {
        rapport(self.coffre.as_ref().expect("coffre ouvert"))
    }
}

/// Résumé de « Santé du coffre », listes triées (même format côté C#).
pub fn rapport(coffre: &Coffre) -> String {
    {
        let sante = coffre.sante();
        let trier = |mut v: Vec<String>| {
            v.sort();
            v.join(";")
        };
        format!(
            "deplacees=[{}] modifiees=[{}] manquantes=[{}] doublons=[{}] non_suivies=[{}] sans_suivi=[{}] illisibles=[{}] liens=[{}] orphelines=[{}]",
            trier(sante.deplacees.iter().map(|d| format!("{} {} {}", d.id, d.de, d.vers)).collect()),
            trier(sante.modifiees_hors_application.clone()),
            trier(sante.manquantes.clone()),
            trier(sante.doublons.iter().map(|d| format!("{} {}", d.id, d.chemins.iter().map(ToString::to_string).collect::<Vec<_>>().join(","))).collect()),
            trier(sante.non_suivies.iter().map(ToString::to_string).collect()),
            trier(sante.sans_suivi.iter().map(|(id, c)| format!("{id} {c}")).collect()),
            trier(sante.fichiers_illisibles.iter().map(|(c, _)| c.clone()).collect()),
            trier(sante.liens_a_verifier.iter().map(ToString::to_string).collect()),
            trier(sante.annotations_orphelines.clone()),
        )
    }
}

impl Partie {
    fn jouer(&mut self, operation: &Value) -> Result<String, ErreurCoffre> {
        let op = operation["op"].as_str().expect("« op » attendu");
        let moment = operation.get("le").and_then(Value::as_str).map(le);
        let maintenant = || moment.expect("« le » attendu");
        match op {
            "initialiser" => {
                let coffre = Coffre::initialiser(&self.racine, &maintenant())?;
                let id = coffre.parametres().id.clone();
                self.coffre = Some(coffre);
                self.nouvel_identifiant(&id);
                Ok(String::new())
            }
            "ouvrir" => {
                self.coffre = None;
                self.coffre = Some(Coffre::ouvrir(&self.racine, &maintenant())?);
                Ok(String::new())
            }
            "analyser" => self.coffre().analyser(&maintenant()).map(|()| String::new()),
            "creer_fiche" => {
                let dossier = self.option(operation, "dossier");
                let (titre, corps) = (self.texte(operation, "titre"), self.texte(operation, "corps"));
                let id = self.coffre().creer_fiche(dossier.as_deref(), &titre, &corps, &maintenant())?;
                self.nouvel_identifiant(&id);
                self.noms.insert(operation["nom"].as_str().expect("« nom »").to_owned(), id);
                Ok(String::new())
            }
            "suivre_fichier" => {
                let chemin = CheminRelatif::nouveau(&self.texte(operation, "chemin"))?;
                let id = self.coffre().suivre_fichier(&chemin, &maintenant())?;
                self.nouvel_identifiant(&id);
                self.noms.insert(operation["nom"].as_str().expect("« nom »").to_owned(), id);
                Ok(String::new())
            }
            "lire_fiche" => {
                let id = self.fiche(operation);
                let contenu = self.coffre().lire_fiche(&id)?;
                self.lectures.insert(id, contenu.empreinte.clone());
                Ok(contenu.empreinte)
            }
            "enregistrer_fiche" => {
                let id = self.fiche(operation);
                let empreinte = self.empreinte(operation, &id);
                let texte = self.texte(operation, "texte");
                let session = operation["session"].as_u64().expect("« session »");
                self.coffre().enregistrer_fiche(&id, &texte, &empreinte, session, &maintenant())
            }
            "enregistrer_corps" => {
                let id = self.fiche(operation);
                let empreinte = self.empreinte(operation, &id);
                let corps = self.texte(operation, "corps");
                let session = operation["session"].as_u64().expect("« session »");
                let resultat = self.coffre().enregistrer_corps(&id, &corps, &empreinte, session, &maintenant())?;
                Ok(Self::resultat_enregistrement(&resultat))
            }
            "garder_en_version" => {
                let (id, corps) = (self.fiche(operation), self.texte(operation, "corps"));
                self.coffre().garder_en_version(&id, &corps, &maintenant()).map(|()| String::new())
            }
            "versions" => {
                let id = self.fiche(operation);
                let versions = self.coffre().versions(&id)?;
                Ok(versions.iter().map(|v| format!("{} {} {}", v.nom, v.enregistree_le, v.taille)).collect::<Vec<_>>().join(";"))
            }
            "lire_version" => {
                let (id, nom) = (self.fiche(operation), self.texte(operation, "version"));
                self.coffre().lire_version(&id, &nom).map(|corps| json(&corps))
            }
            "restaurer_version" => {
                let (id, nom) = (self.fiche(operation), self.texte(operation, "version"));
                let empreinte = self.empreinte(operation, &id);
                let resultat = self.coffre().restaurer_version(&id, &nom, &empreinte, &maintenant())?;
                Ok(Self::resultat_enregistrement(&resultat))
            }
            "modifier_etiquettes" => {
                let id = self.fiche(operation);
                let etiquettes: Vec<String> = operation["etiquettes"]
                    .as_array()
                    .expect("« etiquettes »")
                    .iter()
                    .map(|v| v.as_str().expect("texte").to_owned())
                    .collect();
                self.coffre().modifier_etiquettes(&id, &etiquettes, &maintenant()).map(|e| liste(&e))
            }
            "etiquettes" => Ok(liste(&self.coffre().etiquettes())),
            "changer_titre" => {
                let (id, titre) = (self.fiche(operation), self.texte(operation, "titre"));
                self.coffre().changer_titre(&id, &titre, &maintenant()).map(|()| String::new())
            }
            "deplacer_fiche" => {
                let (id, dossier) = (self.fiche(operation), self.option(operation, "dossier"));
                self.coffre().deplacer_fiche(&id, dossier.as_deref(), &maintenant()).map(|()| String::new())
            }
            "ajouter_ressource" => {
                let (id, nom, contenu) =
                    (self.fiche(operation), self.texte(operation, "nom_fichier"), self.texte(operation, "contenu"));
                self.coffre().ajouter_ressource(&id, &nom, contenu.as_bytes())
            }
            "dossiers" => Ok(liste(&self.coffre().dossiers())),
            "creer_dossier" => {
                let dossier = self.texte(operation, "dossier");
                self.coffre().creer_dossier(&dossier).map(|d| json(&d))
            }
            "deplacer_dossier" => {
                let (dossier, vers) = (self.texte(operation, "dossier"), self.texte(operation, "vers"));
                self.coffre().deplacer_dossier(&dossier, &vers, &maintenant()).map(|d| json(&d))
            }
            "corbeille_fiche" => {
                let id = self.fiche(operation);
                self.coffre().mettre_fiche_a_la_corbeille(&id, &maintenant()).map(|()| String::new())
            }
            "corbeille_dossier" => {
                let dossier = self.texte(operation, "dossier");
                let resultat = self.coffre().mettre_dossier_a_la_corbeille(&dossier, &maintenant());
                self.noter_dossiers_ranges();
                resultat.map(|n| n.to_string())
            }
            "restaurer_fiche" => {
                let id = self.fiche(operation);
                self.coffre().restaurer_fiche_de_la_corbeille(&id, &maintenant()).map(|()| String::new())
            }
            "fiches_a_la_corbeille" => Ok(self
                .coffre()
                .fiches_a_la_corbeille()?
                .iter()
                .map(|f| format!("{} {} {} {} {}", f.id, json(&f.titre), json(&f.dossier), f.mise_a_la_corbeille_le, f.annotations))
                .collect::<Vec<_>>()
                .join(";")),
            "reviser" => {
                let id = self.fiche(operation);
                let regles = self.coffre().parametres().rappels.clone();
                let m = maintenant();
                self.coffre().appliquer_au_suivi(&id, |s| s.valider_revision(&m, None, Some(&regles))).map(|_| String::new())
            }
            "reporter" => {
                let id = self.fiche(operation);
                let vers: Date = self.texte(operation, "vers").parse().expect("date");
                let m = maintenant();
                self.coffre().appliquer_au_suivi(&id, |s| s.reporter(&m, vers)).map(|()| String::new())
            }
            "archiver" => {
                let id = self.fiche(operation);
                let regles = self.coffre().parametres().rappels.clone();
                let m = maintenant();
                self.coffre().appliquer_au_suivi(&id, |s| s.archiver(&m, Some(&regles))).map(|()| String::new())
            }
            "sante" => Ok(self.sante()),
            "fiche" => {
                let id = self.fiche(operation);
                let entree = self.coffre().fiche(&id)?.clone();
                let presence = match entree.presence {
                    PresenceFichier::Present => "present",
                    PresenceFichier::Manquant => "manquant",
                    PresenceFichier::EnDoublon => "doublon",
                };
                Ok(format!(
                    "{} {} {} {} {}",
                    entree.suivie.chemin,
                    json(&entree.suivie.titre),
                    liste(&entree.suivie.etiquettes),
                    presence,
                    entree.suivie.empreinte
                ))
            }
            // Gestes faits hors de l'application.
            "ecrire" => {
                let chemin = self.racine.join(self.texte(operation, "chemin"));
                fs::create_dir_all(chemin.parent().expect("parent")).expect("dossier");
                let octets = match operation.get("octets").and_then(Value::as_array) {
                    Some(octets) => octets.iter().map(|o| o.as_u64().expect("octet") as u8).collect(),
                    None => self.texte(operation, "contenu").into_bytes(),
                };
                fs::write(&chemin, octets).expect("écriture");
                if let Some(instant) = operation.get("modifie_le").and_then(Value::as_str) {
                    let instant: Timestamp = instant.parse().expect("horodatage");
                    let fichier = fs::File::options().write(true).open(&chemin).expect("ouverture");
                    fichier.set_modified(instant.into()).expect("date de modification");
                }
                Ok(String::new())
            }
            "supprimer" => {
                let chemin = self.racine.join(self.texte(operation, "chemin"));
                if chemin.is_dir() { fs::remove_dir_all(&chemin) } else { fs::remove_file(&chemin) }.expect("suppression");
                Ok(String::new())
            }
            "deplacer" => {
                let de = self.racine.join(self.texte(operation, "de"));
                let vers = self.racine.join(self.texte(operation, "vers"));
                fs::create_dir_all(vers.parent().expect("parent")).expect("dossier");
                fs::rename(&de, &vers).expect("déplacement");
                Ok(String::new())
            }
            autre => self.jouer_complements(autre, operation, moment),
        }
    }
}

/// Joue le scénario du dossier `dossier` (qui contient `scenario.json`).
pub fn jouer(dossier: &Path) {
    let scenario: Value =
        serde_json::from_slice(&fs::read(dossier.join("scenario.json")).expect("scenario.json")).expect("JSON du scénario");
    let racine = dossier.join("attendu");
    for ancien in [racine.clone(), racine.with_extension("dehors")] {
        if ancien.exists() {
            fs::remove_dir_all(&ancien).expect("nettoyage de attendu/");
        }
    }
    let mut partie = Partie {
        racine: racine.clone(),
        coffre: None,
        noms: HashMap::new(),
        identifiants: Vec::new(),
        lectures: HashMap::new(),
        dossiers_ranges: BTreeSet::new(),
        vus: HashSet::new(),
    };
    let mut resultats = String::new();
    for (n, operation) in scenario["operations"].as_array().expect("« operations »").iter().enumerate() {
        // Des identifiants créés à des millisecondes différentes sont dans l'ordre de création.
        std::thread::sleep(std::time::Duration::from_millis(3));
        let op = operation["op"].as_str().unwrap_or_default();
        let ligne = match partie.jouer(operation) {
            Ok(valeur) if valeur.is_empty() => format!("{n} {op} ok"),
            Ok(valeur) => format!("{n} {op} ok {valeur}"),
            Err(e) => format!("{n} {op} erreur {}", erreur(&e)),
        };
        writeln!(resultats, "{ligne}").expect("écriture en mémoire");
    }
    partie.coffre = None;
    // Ce qui a été écrit hors du coffre est décrit dans les résultats.
    if partie.dehors().exists() {
        fs::remove_dir_all(partie.dehors()).expect("nettoyage de attendu.dehors/");
    }
    // Qui tenait le verrou (poste, processus) : propre à chaque exécution.
    let _ = fs::remove_file(racine.join(".suivi/verrou-info.json"));
    fs::write(dossier.join("resultats.txt"), resultats).expect("resultats.txt");
    let mut identifiants = partie.identifiants.join("\n");
    identifiants.push('\n');
    fs::write(dossier.join("identifiants.txt"), identifiants).expect("identifiants.txt");
    // Git ne garde pas les dossiers vides : leur liste complète est écrite à part.
    let mut dossiers = Vec::new();
    lister_dossiers(&racine, &racine, &mut dossiers);
    dossiers.sort();
    let texte: String = dossiers.iter().map(|d| format!("{d}\n")).collect();
    fs::write(dossier.join("dossiers.txt"), texte).expect("dossiers.txt");
}

fn lister_dossiers(racine: &Path, courant: &Path, dossiers: &mut Vec<String>) {
    for entree in fs::read_dir(courant).expect("dossier").flatten() {
        if entree.file_type().expect("type").is_dir() {
            let chemin = entree.path();
            let relatif = chemin.strip_prefix(racine).expect("relatif").to_string_lossy().replace(std::path::MAIN_SEPARATOR, "/");
            dossiers.push(relatif);
            lister_dossiers(racine, &chemin, dossiers);
        }
    }
}

/// Joue chaque scénario (sous-dossier) de `dossier`, ou le scénario `dossier` lui-même.
pub fn jouer_tous(dossier: &Path) {
    // Les exports datent les événements dans le fuseau du système : UTC, comme côté C#.
    // SAFETY : le programme n'a qu'un fil d'exécution à ce stade.
    unsafe { std::env::set_var("TZ", "UTC0") };
    // Un seul scénario : les autres gardent leurs identifiants.
    if dossier.join("scenario.json").is_file() {
        jouer(dossier);
        return;
    }
    let mut scenarios: Vec<PathBuf> = fs::read_dir(dossier)
        .expect("dossier des scénarios")
        .flatten()
        .map(|e| e.path())
        .filter(|p| p.join("scenario.json").is_file())
        .collect();
    scenarios.sort();
    for scenario in scenarios {
        println!("{}", scenario.display());
        jouer(&scenario);
    }
}
