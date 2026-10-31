//! Vecteurs des fonctions pures du crate `coffre` de kyfRev 0.x : noms de fichiers, en-têtes
//! des fiches et liens Markdown. Les entrées sont des assemblages tirés au hasard de jetons
//! piégeux (Unicode, CRLF, BOM, crochets, chevrons, pourcentages, blocs de code) ; les mêmes
//! jetons et le même générateur sont repris côté C#.
//!
//! Chaque ligne d'un fichier contient, séparées par « | », les empreintes courtes du résultat de
//! chaque fonction pour un cas ; `--fonctions <famille> <n>` affiche le détail d'un cas.

use std::fmt::Write as _;

use coffre::{CheminRelatif, dossier_de_ressources, entete, liens, nom_de_fichier_pour, nom_de_ressource};
use jiff::civil::date;

use crate::{SplitMix, empreinte};

pub const NOMBRE_NOMS: u64 = 3_000;
pub const NOMBRE_ENTETES: u64 = 3_000;
pub const NOMBRE_LIENS: u64 = 3_000;

const BASE_NOMS: u64 = 5_000_000;
const BASE_ENTETES: u64 = 6_000_000;
const BASE_LIENS: u64 = 7_000_000;

const JETONS_NOMS: [&str; 64] = [
    "Chapitre", " ", "3", ":", "la", "mitose", "é", "É", "ß", "Σ", "ς", "İ", "ǅ", "\t", "\n", "\u{a0}",
    "\u{2028}", "\u{3000}", "\u{1}", "\u{7f}", "\u{85}", "<", ">", "?", "*", "\"", "/", "\\", "|", ".", "..", "#",
    "##", "CON", "con", "LPT1", "nul", "😀", "🇫🇷", "e\u{301}", "ﬁ", "Ⅻ", "-", "_", ",", "(", ")", "[",
    "]", "%", ".PNG", ".png", ".Exe", ".html", ".tar.gz", "~", "'", "a", "Z", "日本", "\u{200b}", "\u{feff}",
    "abcdefghijklmnopqrstuvwxyz0123456789", "éééééééééééééééééééé",
];

const LIGNES_ENTETE: [&str; 36] = [
    "---", "...", "--- ", "id: f_01JA8Z3K5Q6WXYZABCDEFGHJKM", "titre: Mitose", "titre: \"Révolution : les causes\"",
    "titre:", "titre: 'l''Ancien Régime'", "creee: 2026-10-09", "etiquettes: [bio, examen]", "etiquettes:",
    "  - bio", "  - \"examen\"", "- liste", "tags: examen, bac", "tags: [a]", "tags:", "auteur: X",
    "titre : espace", "titre:collé", "  titre: indenté", "\tclé: tabulation", "# commentaire", "",
    "Corps du texte.", "id: f_court", "etiquettes: [\"a\", \"b\"]", "titre: [liste]", "titre: |", "  bloc",
    "clé: valeur: deux", "titrex: y", "url: http://x.org", "id: 42", "titre: 12", "titre: ~",
];

const CLES: [&str; 6] = ["id", "titre", "etiquettes", "tags", "creee", "nouveau"];

const JETONS_LIENS: [&str; 52] = [
    "[", "]", "(", ")", "![", "](", "]: ", "<", ">", "\\", "`", "```", "~~~", "\n", "\r\n", " ", "\t", "    ",
    "a.md", "../b.md", "./c.md", "Mitose.md", "Mitose.ressources/img.png", "Mitose.ressources/", "%20", "%2",
    "%C3%A9", "%FF", "#section", "?q=1", "https://x.org", "mailto:a@b", "C:/x.md", "/abs.md", "é",
    "Révolution française.md", "C#.md", "Bio/sub/x.md", "..", ".", "/", "[ref]: ", "[ref]", "\"titre\"",
    "texte", "Bio/", "../Bio/Mitose.md", "\\)", "\\>", "x y.md", "<a b.md>", "\u{feff}",
];

const DOSSIERS: [&str; 5] = ["fiches", "fiches/Bio", "fiches/Bio/Cellule", "fiches/Histoire", ""];

const PARTIES_CHEMIN: [&str; 9] = ["a", "b", "..", ".", "", "fiches", "Bio", "é", "x.md"];

/// Assemblage de 0 à `max - 1` jetons.
fn assembler(alea: &mut SplitMix, jetons: &[&str], max: u64) -> String {
    let nombre = alea.sous(max);
    (0..nombre).map(|_| jetons[alea.sous(jetons.len() as u64) as usize]).collect()
}

fn liste(alea: &mut SplitMix) -> Vec<String> {
    let nombre = alea.sous(7);
    (0..nombre).map(|_| assembler(alea, &JETONS_NOMS, 5)).collect()
}

fn json(texte: &str) -> String {
    serde_json::to_string(texte).expect("texte JSON")
}

fn json_option(texte: Option<&str>) -> String {
    texte.map_or_else(|| "null".to_owned(), json)
}

fn json_liste(textes: &[String]) -> String {
    serde_json::to_string(textes).expect("liste JSON")
}

/// Une transcription : une partie par fonction, chacune réduite à une empreinte courte.
pub struct Transcription(Vec<(&'static str, String)>);

impl Transcription {
    fn ligne(&self) -> String {
        self.0.iter().map(|(_, texte)| empreinte(texte)[..12].to_owned()).collect::<Vec<_>>().join("|")
    }

    fn detail(&self) -> String {
        self.0.iter().fold(String::new(), |mut sortie, (nom, texte)| {
            writeln!(sortie, "{nom}: {texte}").expect("écriture en mémoire");
            sortie
        })
    }
}

pub fn transcrire_noms(n: u64) -> Transcription {
    let mut alea = SplitMix(BASE_NOMS + n);
    let titre = assembler(&mut alea, &JETONS_NOMS, 13);
    let nom = nom_de_fichier_pour(&titre);
    let chemin = match CheminRelatif::nouveau(&format!("fiches/{nom}.md")) {
        Ok(chemin) => match dossier_de_ressources(&chemin) {
            Ok(ressources) => format!("ok {chemin} {ressources}"),
            Err(erreur) => format!("erreur ressources : {erreur}"),
        },
        Err(erreur) => format!("erreur : {erreur}"),
    };
    let original = assembler(&mut alea, &JETONS_NOMS, 9);
    let ressource = match nom_de_ressource(&original) {
        Ok(nom) => format!("ok {}", json(&nom)),
        Err(erreur) => format!("erreur : {erreur}"),
    };
    let etiquettes = entete::normaliser_etiquettes(&liste(&mut alea));
    Transcription(vec![
        ("nom", json(&nom)),
        ("chemin", chemin),
        ("ressource", ressource),
        ("etiquettes", json_liste(&etiquettes)),
    ])
}

pub fn transcrire_entetes(n: u64) -> Transcription {
    let mut alea = SplitMix(BASE_ENTETES + n);
    let bom = if alea.sous(5) == 0 { "\u{feff}" } else { "" };
    let fin_de_ligne = if alea.sous(3) == 0 { "\r\n" } else { "\n" };
    let nombre = alea.sous(9);
    let mut lignes: Vec<&str> = Vec::new();
    if alea.sous(4) != 0 {
        lignes.push("---");
    }
    for _ in 0..nombre {
        lignes.push(LIGNES_ENTETE[alea.sous(LIGNES_ENTETE.len() as u64) as usize]);
    }
    let mut texte = format!("{bom}{}", lignes.join(fin_de_ligne));
    if alea.sous(2) == 0 {
        texte.push_str(fin_de_ligne);
    }
    texte.push_str(&assembler(&mut alea, &JETONS_NOMS, 4));

    let decoupage = entete::decouper(&texte);
    let lu = entete::lire(&texte);
    let lecture = match &lu {
        Ok(e) => format!(
            "ok {} {} {} {} {}",
            json_option(e.id.as_deref()),
            json_option(e.titre.as_deref()),
            json_option(e.creee.as_deref()),
            json_liste(&e.etiquettes()),
            e.cle_des_etiquettes()
        ),
        Err(_) => "erreur".to_owned(),
    };
    let cle_etiquettes = lu.as_ref().map_or("etiquettes", |e| e.cle_des_etiquettes());
    let titre = assembler(&mut alea, &JETONS_NOMS, 6);
    let etiquettes = entete::normaliser_etiquettes(&liste(&mut alea));
    let cle = CLES[alea.sous(CLES.len() as u64) as usize];
    Transcription(vec![
        (
            "decoupage",
            format!("{} {} {}", json_option(decoupage.yaml), decoupage.debut_yaml, decoupage.debut_corps),
        ),
        ("lire", lecture),
        ("ajouter_id", json(&entete::ajouter_id(&texte, "f_01M69JFV2X5VM53EY3DR1BY61M"))),
        ("titre", json(&entete::remplacer_champ(&texte, "titre", &entete::valeur_texte(&titre)))),
        (
            "etiquettes",
            json(&entete::remplacer_champ(&texte, cle_etiquettes, &entete::valeur_liste(&etiquettes))),
        ),
        ("champ", json(&entete::remplacer_champ(&texte, cle, "x"))),
        ("composer", json(&entete::composer("f_01M69JFV2X5VM53EY3DR1BY61M", &titre, date(2026, 10, 9)))),
    ])
}

/// Où se retrouvent les cibles après un renommage fictif (même règle côté C#).
fn transformer(cible: &str) -> Option<String> {
    const RESSOURCES: &str = "fiches/Bio/Mitose.ressources";
    if cible == "fiches/Bio/Mitose.md" {
        return Some("fiches/Histoire/Mitose (2).md".to_owned());
    }
    if cible == "fiches/a.md" {
        return Some("fiches/Z/a b.md".to_owned());
    }
    if cible == RESSOURCES {
        return Some("fiches/Histoire/Mitose (2).ressources".to_owned());
    }
    cible
        .strip_prefix(RESSOURCES)
        .filter(|reste| reste.starts_with('/'))
        .map(|reste| format!("fiches/Histoire/Mitose (2).ressources{reste}"))
}

fn chemin_au_hasard(alea: &mut SplitMix) -> String {
    let nombre = alea.sous(5);
    (0..nombre)
        .map(|_| PARTIES_CHEMIN[alea.sous(PARTIES_CHEMIN.len() as u64) as usize])
        .collect::<Vec<_>>()
        .join("/")
}

pub fn transcrire_liens(n: u64) -> Transcription {
    let mut alea = SplitMix(BASE_LIENS + n);
    let texte = assembler(&mut alea, &JETONS_LIENS, 25);
    let dossier = DOSSIERS[alea.sous(DOSSIERS.len() as u64) as usize];
    let nouveau_dossier = DOSSIERS[alea.sous(DOSSIERS.len() as u64) as usize];
    let reperes = liens::reperer(&texte)
        .iter()
        .map(|l| format!("{}-{}-{}", l.debut, l.fin, l.chevrons))
        .collect::<Vec<_>>()
        .join(";");
    let reecrit = liens::reecrire(&texte, dossier, nouveau_dossier, transformer);
    let brute = assembler(&mut alea, &JETONS_LIENS, 6);
    let chevrons = alea.sous(2) == 0;
    let a_encoder = assembler(&mut alea, &JETONS_NOMS, 6);
    let relatif = chemin_au_hasard(&mut alea);
    let cible = chemin_au_hasard(&mut alea);
    Transcription(vec![
        ("reperer", reperes),
        ("reecrire", json_option(reecrit.as_deref())),
        ("decoder", json(&liens::decoder(&brute))),
        ("encoder", json(&liens::encoder(&a_encoder, chevrons))),
        ("resoudre", json_option(liens::resoudre(dossier, &relatif).as_deref())),
        ("relatif_depuis", json(&liens::relatif_depuis(dossier, &cible))),
    ])
}

pub fn transcrire(famille: &str, n: u64) -> Transcription {
    match famille {
        "noms" => transcrire_noms(n),
        "entetes" => transcrire_entetes(n),
        "liens" => transcrire_liens(n),
        _ => panic!("famille inconnue : {famille} (noms, entetes ou liens)"),
    }
}

/// Affiche le détail du cas `n` de la famille.
pub fn afficher(famille: &str, n: u64) {
    print!("{}", transcrire(famille, n).detail());
}

/// Écrit `noms.txt`, `entetes.txt` et `liens.txt` dans `dossier`.
pub fn ecrire(dossier: &std::path::Path) {
    for (famille, nombre) in [("noms", NOMBRE_NOMS), ("entetes", NOMBRE_ENTETES), ("liens", NOMBRE_LIENS)] {
        let fichier = format!("{famille}.txt");
        let mut sortie = format!(
            "# {fichier} : vecteurs de référence produits par le crate coffre de kyfRev 0.x.\n\
             # Générés par tools/reference-vectors ; ne pas modifier à la main.\n"
        );
        for n in 0..nombre {
            writeln!(sortie, "{}", transcrire(famille, n).ligne()).expect("écriture en mémoire");
        }
        std::fs::write(dossier.join(&fichier), sortie).expect("écriture du fichier de vecteurs");
    }
}
