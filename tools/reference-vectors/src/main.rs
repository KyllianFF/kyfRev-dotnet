//! Vecteurs de référence du cœur Rust de kyfRev 0.x (crate `noyau`).
//!
//! Chaque cas est tiré d'un générateur SplitMix64 dont la graine est fixée : la suite
//! de tests C# régénère exactement les mêmes entrées, rejoue l'implémentation C# et compare
//! ses résultats aux lignes écrites ici. Seuls les résultats sont donc enregistrés.
//!
//! Usage :
//!   cargo run --release -- <dossier de sortie>      écrit tous les fichiers
//!   cargo run --release -- --transcript <n>         affiche la transcription du suivi n
//!   cargo run --release -- --coffre <dossier>       construit le coffre de référence

mod coffre_reference;

use std::fmt::Write as _;
use std::fs;
use std::path::Path;

use jiff::Timestamp;
use jiff::civil::{Date, date};
use jiff::tz::TimeZone;
use noyau::{
    Candidat, ErreurSuivi, IssueRappel, IssueValidation, Jour, Moment, ObjetModifie, OrigineJ0,
    ReglesRappels, Sequence, Suivi, ajouter_jours, ajouter_mois, graine_du_jour, jours_entre,
    tirer,
};
use rand_chacha::ChaCha8Rng;
use rand_core::{Rng, SeedableRng};
use sha2::{Digest, Sha256};

const NOMBRE_SUIVIS: u64 = 10_000;
const NOMBRE_TIRAGES: u64 = 3_000;
const NOMBRE_GRAINES: u64 = 300;
const NOMBRE_DATES: u64 = 5_000;
const NOMBRE_FLUX_CHACHA: u64 = 16;
const SORTIES_PAR_FLUX: usize = 70;

const BASE_SUIVIS: u64 = 0;
const BASE_TIRAGES: u64 = 1_000_000;
const BASE_FLUX_CHACHA: u64 = 2_000_000;
const BASE_GRAINES: u64 = 3_000_000;
const BASE_DATES: u64 = 4_000_000;

const SEQUENCES: [&[u16]; 4] = [&[0, 1, 3, 7, 14, 30], &[0, 1], &[0, 2, 5, 9], &[0, 1, 3, 7, 14, 30, 60]];
const ORIGINES: [OrigineJ0; 4] = [
    OrigineJ0::Creation,
    OrigineJ0::Adoption,
    OrigineJ0::Rappel,
    OrigineJ0::Redemarrage,
];
const OBJETS: [ObjetModifie; 3] = [ObjetModifie::Commentaire, ObjetModifie::Passage, ObjetModifie::Definition];
const CROCKFORD: &[u8] = b"0123456789ABCDEFGHJKMNPQRSTVWXYZ";

/// SplitMix64 : générateur des entrées, reproduit à l'identique côté C#.
struct SplitMix(u64);

impl SplitMix {
    fn suivant(&mut self) -> u64 {
        self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
        let mut z = self.0;
        z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
        z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
        z ^ (z >> 31)
    }

    fn sous(&mut self, n: u64) -> u64 {
        self.suivant() % n
    }
}

fn plus_jours(jour: Jour, n: i64) -> Jour {
    ajouter_jours(jour, n).expect("date dans les limites")
}

fn moment(jour: Jour) -> Moment {
    let instant: Timestamp = jour
        .at(12, 0, 0, 0)
        .to_zoned(TimeZone::UTC)
        .expect("date dans les limites")
        .timestamp();
    Moment::nouveau(jour, instant)
}

fn sequence(index: u64) -> Sequence {
    Sequence::nouvelle(SEQUENCES[index as usize].to_vec()).expect("séquence valide")
}

fn regles(index: u64) -> ReglesRappels {
    match index {
        0 => ReglesRappels::default(),
        1 => ReglesRappels {
            delai_minimal_mois: 1,
            sequence_repos_mois: vec![1, 3],
            max_par_jour: 1,
            attente_max_jours: 10,
            inclure_archivage_manuel: false,
        },
        _ => ReglesRappels {
            delai_minimal_mois: 3,
            sequence_repos_mois: vec![2, 4, 8, 12, 24],
            max_par_jour: 3,
            attente_max_jours: 45,
            inclure_archivage_manuel: true,
        },
    }
}

/// Une lettre par nature d'erreur ; « . » pour une opération réussie.
fn code(erreur: &ErreurSuivi) -> char {
    match erreur {
        ErreurSuivi::PasActif => 'a',
        ErreurSuivi::PasArchive => 'b',
        ErreurSuivi::DejaRevise { .. } => 'c',
        ErreurSuivi::DateFuture { .. } => 'd',
        ErreurSuivi::AnterieureDerniereRevision { .. } => 'e',
        ErreurSuivi::ReportDansLePasse { .. } => 'f',
        ErreurSuivi::ReportIdentique { .. } => 'g',
        ErreurSuivi::AucunRappelEnAttente => 'h',
        ErreurSuivi::RappelDejaEnAttente => 'i',
        ErreurSuivi::PasEncoreEligible { .. } => 'j',
        ErreurSuivi::ExcluDesRappels => 'k',
        ErreurSuivi::AucunPassageMarque => 'l',
        ErreurSuivi::ReglesInvalides(_) => 'm',
        ErreurSuivi::Incoherent(_) => 'n',
        ErreurSuivi::DateHorsLimites => 'o',
    }
}

struct Transcription {
    texte: String,
    codes: String,
}

impl Transcription {
    fn ligne(&mut self, ligne: &str) {
        self.texte.push_str(ligne);
        self.texte.push('\n');
    }

    fn resultat(&mut self, operation: &str, resultat: Result<String, ErreurSuivi>, suivi: &Suivi) {
        match resultat {
            Ok(detail) => {
                self.codes.push('.');
                let verification = if suivi.verifier().is_ok() { "ok" } else { "KO" };
                self.ligne(&format!(
                    "{operation} ok {detail} | {} | verifier {verification}",
                    serde_json::to_string(suivi).expect("suivi sérialisable")
                ));
            }
            Err(erreur) => {
                self.codes.push(code(&erreur));
                self.ligne(&format!("{operation} erreur {erreur}"));
            }
        }
    }
}

/// Rejoue la suite d'opérations n sur un suivi et en rend la transcription complète.
fn transcrire_suivi(n: u64) -> Transcription {
    let mut alea = SplitMix(BASE_SUIVIS + n);
    let mut jour = plus_jours(date(2026, 1, 1), alea.sous(730) as i64);
    let passage = alea.sous(10) < 3;
    let index_regles = alea.sous(3);
    let regles_fiche = regles(index_regles);
    let rappels = if passage { None } else { Some(&regles_fiche) };
    let origine = ORIGINES[alea.sous(4) as usize];
    let mut suivi =
        Suivi::demarrer(&moment(jour), sequence(alea.sous(4)), origine).expect("démarrage valide");

    let mut t = Transcription { texte: String::new(), codes: String::new() };
    t.ligne(&format!(
        "suivi {n} passage {passage} regles {index_regles} | {}",
        serde_json::to_string(&suivi).expect("suivi sérialisable")
    ));

    let operations = 1 + alea.sous(40);
    for _ in 0..operations {
        let avance = if alea.sous(3) == 0 { alea.sous(91) } else { alea.sous(4) };
        jour = plus_jours(jour, avance as i64);
        let m = moment(jour);
        t.ligne(&format!("jour {jour}"));

        match alea.sous(13) {
            0..=2 => {
                let date_revision = match alea.sous(4) {
                    1 => Some(plus_jours(jour, -(alea.sous(5) as i64))),
                    2 => Some(plus_jours(jour, 1)),
                    _ => None,
                };
                let resultat = suivi.valider_revision(&m, date_revision, rappels).map(|issue| match issue {
                    IssueValidation::EtapeSuivante { etape, echeance } => format!("suivante {etape} {echeance}"),
                    IssueValidation::CycleTermine { archivee_le } => format!("terminee {archivee_le}"),
                });
                let libelle = date_revision.map_or_else(|| "-".to_owned(), |d| d.to_string());
                t.resultat(&format!("valider {libelle}"), resultat, &suivi);
            }
            3 => {
                let vers = plus_jours(jour, alea.sous(20) as i64 - 2);
                let resultat = suivi.reporter(&m, vers).map(|()| String::new());
                t.resultat(&format!("reporter {vers}"), resultat, &suivi);
            }
            4 => {
                let index = alea.sous(4);
                let resultat = suivi.recommencer(&m, sequence(index)).map(|()| String::new());
                t.resultat(&format!("recommencer {index}"), resultat, &suivi);
            }
            5 => {
                let resultat = suivi.archiver(&m, rappels).map(|()| String::new());
                t.resultat("archiver", resultat, &suivi);
            }
            6 => {
                let resultat = suivi.marquer_tire(jour).map(|()| String::new());
                t.resultat("tirer", resultat, &suivi);
            }
            7 => {
                let (issue, libelle) = match alea.sous(3) {
                    0 => (IssueRappel::Connue, "connue".to_owned()),
                    1 => {
                        let passages_marques = alea.sous(3) as u32;
                        (
                            IssueRappel::CertainsPointsARevoir { passages_marques },
                            format!("certains {passages_marques}"),
                        )
                    }
                    _ => (IssueRappel::ARevoirEntierement, "entierement".to_owned()),
                };
                let index = alea.sous(4);
                let resultat = suivi
                    .traiter_rappel(&m, issue, &regles_fiche, sequence(index))
                    .map(|()| String::new());
                t.resultat(&format!("rappel {libelle} {index}"), resultat, &suivi);
            }
            8 => {
                let inclure = alea.sous(2) == 0;
                let resultat = suivi.inclure_dans_rappels(&m, inclure).map(|()| String::new());
                t.resultat(&format!("inclure {inclure}"), resultat, &suivi);
            }
            9 => {
                let index = alea.sous(3);
                suivi.noter_modification(&m, OBJETS[index as usize]);
                t.resultat(&format!("modifier {index}"), Ok(String::new()), &suivi);
            }
            10 => {
                t.codes.push(if suivi.est_eligible(jour) { 'T' } else { 'F' });
                t.ligne(&format!("eligible {}", suivi.est_eligible(jour)));
            }
            _ => {
                let jusqu_au = plus_jours(jour, alea.sous(60) as i64);
                let ligne = match suivi.cycle() {
                    None => "projection aucune".to_owned(),
                    Some(cycle) => {
                        let mut texte = format!(
                            "projection {} retard {} :",
                            cycle.libelle_etape(),
                            cycle.jours_de_retard(jour).expect("date dans les limites")
                        );
                        for (date, etape) in cycle.etapes_projetees(jour, jusqu_au).expect("projection") {
                            write!(texte, " {date}#{etape}").expect("écriture en mémoire");
                        }
                        texte
                    }
                };
                t.codes.push('p');
                t.ligne(&ligne);
            }
        }
    }
    t
}

fn empreinte(texte: &str) -> String {
    Sha256::digest(texte.as_bytes()).iter().fold(String::new(), |mut hex, octet| {
        write!(hex, "{octet:02x}").expect("écriture en mémoire");
        hex
    })
}

fn entete(fichier: &str) -> String {
    format!(
        "# {fichier} : vecteurs de référence produits par le cœur Rust de kyfRev 0.x (crate noyau).\n\
         # Générés par tools/reference-vectors ; ne pas modifier à la main.\n"
    )
}

fn ecrire_suivis(dossier: &Path) {
    let mut sortie = entete("tracking.txt");
    for n in 0..NOMBRE_SUIVIS {
        let t = transcrire_suivi(n);
        writeln!(sortie, "{}|{}", t.codes, empreinte(&t.texte)).expect("écriture en mémoire");
    }
    fs::write(dossier.join("tracking.txt"), sortie).expect("écriture de tracking.txt");
}

fn ecrire_tirages(dossier: &Path) {
    let mut sortie = entete("draws.txt");
    for k in 0..NOMBRE_TIRAGES {
        let mut alea = SplitMix(BASE_TIRAGES + k);
        let aujourdhui = plus_jours(date(2027, 1, 1), alea.sous(400) as i64);
        let nombre = alea.sous(40);
        let mut candidats = Vec::new();
        for _ in 0..nombre {
            let id = format!("f{}", alea.sous(30));
            let eligible_le = plus_jours(aujourdhui, alea.sous(150) as i64 - 120);
            candidats.push(Candidat { id, eligible_le });
        }
        let en_attente = alea.sous(5) as usize;
        let regles = regles(alea.sous(3));
        let graine = alea.suivant();
        let tirage = tirer(aujourdhui, &candidats, en_attente, &regles, graine).expect("règles valides");
        writeln!(sortie, "{}|{}", tirage.selection.join(","), tirage.prioritaires_non_servis)
            .expect("écriture en mémoire");
    }
    fs::write(dossier.join("draws.txt"), sortie).expect("écriture de draws.txt");
}

fn ecrire_flux_chacha(dossier: &Path) {
    let mut sortie = entete("chacha8.txt");
    for s in 0..NOMBRE_FLUX_CHACHA {
        let graine = SplitMix(BASE_FLUX_CHACHA + s).suivant();
        let mut generateur = ChaCha8Rng::seed_from_u64(graine);
        let sorties: Vec<String> = (0..SORTIES_PAR_FLUX).map(|_| generateur.next_u64().to_string()).collect();
        writeln!(sortie, "{graine}:{}", sorties.join(" ")).expect("écriture en mémoire");
    }
    fs::write(dossier.join("chacha8.txt"), sortie).expect("écriture de chacha8.txt");
}

fn ecrire_graines(dossier: &Path) {
    let mut sortie = entete("seeds.txt");
    for k in 0..NOMBRE_GRAINES {
        let mut alea = SplitMix(BASE_GRAINES + k);
        let mut id = String::from("v_");
        for _ in 0..26 {
            id.push(CROCKFORD[alea.sous(32) as usize] as char);
        }
        let jour = plus_jours(date(2000, 1, 1), alea.sous(20_000) as i64);
        writeln!(sortie, "{}", graine_du_jour(&id, jour)).expect("écriture en mémoire");
    }
    fs::write(dossier.join("seeds.txt"), sortie).expect("écriture de seeds.txt");
}

fn ecrire_dates(dossier: &Path) {
    let mut sortie = entete("calendar.txt");
    let origine: Date = date(1990, 1, 1);
    for k in 0..NOMBRE_DATES {
        let mut alea = SplitMix(BASE_DATES + k);
        let jour = plus_jours(origine, alea.sous(40_000) as i64);
        let mois = alea.sous(40) as i64;
        let autre = plus_jours(origine, alea.sous(40_000) as i64);
        let apres = ajouter_mois(jour, mois).expect("date dans les limites");
        let ecart = jours_entre(jour, autre).expect("date dans les limites");
        writeln!(sortie, "{apres}|{ecart}").expect("écriture en mémoire");
    }
    fs::write(dossier.join("calendar.txt"), sortie).expect("écriture de calendar.txt");
}

fn main() {
    let arguments: Vec<String> = std::env::args().skip(1).collect();
    match arguments.as_slice() {
        [option, n] if option == "--transcript" => {
            let n: u64 = n.parse().expect("numéro de suivi");
            print!("{}", transcrire_suivi(n).texte);
        }
        [option, dossier] if option == "--coffre" => {
            coffre_reference::construire(Path::new(dossier));
        }
        [option, dossier, secondes] if option == "--ouvrir" => {
            // Ouvre le coffre comme kyfRev 0.x (verrou compris) et le garde ouvert un moment.
            let secondes: u64 = secondes.parse().expect("durée en secondes");
            match coffre::Coffre::ouvrir(Path::new(dossier), &moment(date(2026, 10, 31))) {
                Ok(_coffre) => {
                    println!("ouvert : verrou tenu {secondes} s");
                    std::thread::sleep(std::time::Duration::from_secs(secondes));
                }
                Err(erreur) => {
                    println!("refus : {erreur}");
                    std::process::exit(3);
                }
            }
        }
        [dossier] => {
            let dossier = Path::new(dossier);
            fs::create_dir_all(dossier).expect("dossier de sortie");
            ecrire_suivis(dossier);
            ecrire_tirages(dossier);
            ecrire_flux_chacha(dossier);
            ecrire_graines(dossier);
            ecrire_dates(dossier);
        }
        _ => {
            eprintln!("usage : reference-vectors <dossier> | reference-vectors --transcript <n>");
            std::process::exit(2);
        }
    }
}
