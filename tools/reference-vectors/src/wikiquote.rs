//! Vecteurs de la citation du jour en ligne (crate `coffre`, module `wikiquote`) : pages à lire,
//! domaines reconnus, et citations extraites d'un wikitexte. Les wikitextes sont ceux des tests
//! de la 0.x, puis des pages tirées au hasard de morceaux typiques de Wikiquote (modèles connus
//! ou non, liens, notes, entités, balises, références incomplètes). La suite C# relit entrées et
//! résultats et compare.

use std::fmt::Write as _;
use std::fs;
use std::panic;
use std::path::Path;

use coffre::wikiquote::{DOMAINES, PageWikiquote, citations_de_la_page, domaines_valides, pages_du_jour};
use jiff::civil::date;
use serde_json::{Value, json};

use crate::SplitMix;

fn c(code: u32) -> char {
    char::from_u32(code).expect("caractère")
}

fn choisir<'a>(hasard: &mut SplitMix, liste: &[&'a str]) -> &'a str {
    liste[usize::try_from(hasard.sous(liste.len() as u64)).expect("rang")]
}

/// Pages à lire : identifiants, jours et choix de domaines variés.
fn pages(hasard: &mut SplitMix) -> String {
    let mut sortie = String::new();
    let ids = ["v_01M69QK9SYZDQXMJC0WJRYXWF3", "v_01JQZ8S7ZK4V3H2N8M6X5W9PQR", "c_1", "", "v_é"];
    let noms: Vec<&str> = DOMAINES.iter().map(|d| d.nom).collect();
    let mut choix: Vec<Vec<String>> = (0..16u32)
        .map(|masque| (0..4).filter(|i| masque & (1 << i) != 0).map(|i| noms[i].to_owned()).collect())
        .collect();
    choix.push(vec!["Science".to_owned(), "art".to_owned()]);
    choix.push(vec!["science".to_owned(), "science".to_owned(), "cuisine".to_owned()]);
    choix.push(vec!["informatique".to_owned(), "philosophie".to_owned()]);
    let mut jours = vec![date(2000, 1, 1), date(1999, 12, 31), date(2026, 10, 11), date(2026, 10, 12)];
    for _ in 0..60 {
        let decalage = i64::try_from(hasard.sous(80_000)).expect("décalage") - 15_000;
        jours.push(crate::plus_jours(date(2000, 1, 1), decalage));
    }
    for id in ids {
        for jour in &jours {
            for domaines in &choix {
                let resultat: Vec<String> =
                    pages_du_jour(id, *jour, domaines).iter().map(|p| format!("{}:{}", p.domaine, p.titre)).collect();
                writeln!(
                    sortie,
                    "{}\t{jour}\t{}\t{}",
                    id,
                    serde_json::to_string(domaines).expect("JSON"),
                    resultat.join(",")
                )
                .expect("écriture");
            }
        }
    }
    sortie
}

/// Domaines reconnus parmi des noms saisis.
fn domaines(hasard: &mut SplitMix) -> String {
    let reserve = [
        "philosophie", "Philosophie", " art ", "ART", "science", "informatique", "cuisine", "", "sciences",
        "Informatique\t", "SCIENCE", "art",
    ];
    let mut sortie = String::new();
    for _ in 0..400 {
        let nombre = hasard.sous(7);
        let noms: Vec<String> = (0..nombre).map(|_| choisir(hasard, &reserve).to_owned()).collect();
        writeln!(sortie, "{}\t{}", serde_json::to_string(&noms).expect("JSON"), domaines_valides(&noms).join(","))
            .expect("écriture");
    }
    sortie
}

/// Wikitextes des tests de la 0.x (`wikiquote.rs`).
fn exemples_de_la_0x() -> Vec<String> {
    vec![
        "== [[Blaise Pascal]] ==\n{{citation|citation=Le cœur a ses raisons que la raison ne connaît point.}}\n{{Réf Livre|titre=''Pensées''|auteur=[[Blaise Pascal|Pascal]]|éditeur=Folio|année=1977|année d'origine=1670|page=142}}\n".to_owned(),
        concat!(
            "{{citation|Une phrase sans référence aucune.}}\n\n",
            "{{citation|Une phrase sans année de parution.}}\n{{Réf Livre|titre=Un livre|auteur=Quelqu'un}}\n",
            "{{citation|Une phrase sans auteur connu ici.}}\n{{Réf Livre|titre=Un livre|année=1900}}\n",
            "{{citation|Une phrase sans titre d'œuvre.}}\n{{Réf Livre|auteur=Quelqu'un|année=1900}}\n",
            "{{citation|Une phrase avec un modèle {{inconnu|x}} dedans.}}\n{{Réf Livre|titre=T|auteur=A|année=1900}}\n",
        )
        .to_owned(),
        "== [[René Descartes]], ''Discours de la méthode'' ==\n=== Première partie ===\n{{citation|citation=Le bon sens est la chose du monde la mieux partagée.}}\n{{Réf Livre|titre=Discours de la méthode|année=1637}}\n".to_owned(),
        concat!(
            "{{citation|citation=Première phrase d'un article de revue.}}{{Réf Article|titre=Un article|auteur=[[Jacques Baron]]|publication=Littérature|date=Septembre 1922|page=15}}\n",
            "{{citation|citation=Une phrase prononcée à la tribune.}}\n{{Réf Discours|titre=Discours d'ouverture|auteur=Jean Jaurès|date=30 juillet 1903}}\n",
            "{{citation|citation=Une phrase d'un ouvrage collectif.}}\n{{Réf Livre|titre=Recueil|titre de la contribution=Préface|auteur de la contribution=Albert Camus|année=1950}}\n",
        )
        .to_owned(),
        format!(
            "{{{{citation|citation=''Être'' ou ne pas [[être (philosophie)|être]],<br />c'est la {{{{w|question}}}}.<ref>Note retirée.</ref> Au {{{{Siècle|XVII}}}}, {{{{Personnage|Hamlet}}}}&nbsp;!|précisions=Acte III}}}}\n{{{{Choisie citation du jour|année=2007|mois=mai|jour=3}}}}\n{{{{Réf Livre|titre=Hamlet{}|auteur=[[William Shakespeare]]|année=1603}}}}",
            c(0x200e)
        ),
        // Le « é » chevauche le 12e octet après « & » : la 0.x panique.
        "{{citation|Laurel & ses amis été là, tous ensemble.}}\n{{Réf Livre|titre=Souvenirs|auteur=Quelqu'un|année=1950}}\n".to_owned(),
    ]
}

/// Une page tirée au hasard de morceaux typiques de Wikiquote.
fn page_au_hasard(hasard: &mut SplitMix) -> String {
    let invisibles = format!("Marques{}invisibles{}retirées{}ici, fin.", c(0x200b), c(0xad), c(0xfeff));
    let textes: Vec<String> = vec![
        "Le cœur a ses raisons que la raison ne connaît point.".into(),
        "''Être'' ou ne pas [[être (philosophie)|être]],<br />c'est la {{w|question}}.".into(),
        "Une phrase avec&nbsp;une entité &amp; un &laquo;guillemet&raquo; &#x2019;apostrophe&#8217; et &inconnue; ici.".into(),
        "Court.".into(),
        "Texte avec une note<ref name=\"a\">Note retirée.</ref> et un commentaire <!-- caché --> qui part.".into(),
        "Au {{Siècle|XVII}}, le {{s-|XIX}} siècle et le 1{{er}} jour du 2{{e}} mois, {{ème}} fois.".into(),
        "Un {{lang|en|English}} mot, un {{nobr|bloc insécable}} puis {{tab}}tabulé et {{lien|cible|affiché}}.".into(),
        "Lien externe [https://exemple.org vers un site] et [https://nu.org] nu, puis [[w:fr:Voltaire#Vie|Voltaire]].".into(),
        "Un modèle {{inconnu|x}} qui écarte la citation.".into(),
        "Texte avec '''gras''' et ''italique'' sur\nplusieurs   lignes\n\n\n\net    des\tblancs.".into(),
        "Fichier [[Fichier:image.png|vignette]] ignoré et [[Catégorie:Philosophie]] aussi, mais [[Page]] gardée.".into(),
        invisibles,
        "<span style=\"x\">Balise</span> < pas une balise > et <br> retour, <BR/> encore, <ref/> vide.".into(),
        "Un texte de plus de dix caractères, sans rien de spécial.".into(),
        "Lien sans fin [[ouvert".into(),
        "Modèle {{w|sans fin".into(),
        "&#55357; substitut seul, &#x1F600; emoji, &#xZZ; faux, &#+65; signe, &#12345678901; trop grand.".into(),
        "Ééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééééé.".into(),
        "{{Personnage|}} vide et {{w|}} vide.".into(),
        "Entités &thinsp;fines&nnbsp;; &lt;balise&gt; &quot;guillemets&quot; &apos;apostrophe&apos; &hellip; &mdash; long &ndash; court &#X41; majuscule.".into(),
        "Comparaison a < b sans chevron fermant, puis la fin.".into(),
        "Lien [https://exemple.org sans crochet fermant, puis la fin.".into(),
    ];
    let auteurs = [
        "Blaise Pascal", "[[René Descartes]]", "[[Jean Racine (dramaturge)|Racine]]", "Albert Camus", "  ",
        "[[w:fr:Voltaire|Voltaire]]", "{{w|Victor Hugo}}", "{{inconnu|x}}", "[[Auteur (sans fin]]", "''Anonyme''",
    ];
    let titres = ["''Pensées''", "[[Les Misérables]]", "Un titre", "  ", "{{inconnu}}", "[[Œuvre|Titre affiché]]"];
    let dates = ["1670", "mai 1968", "XVIIe siècle", "inconnue", "30 juillet 1903", "12 av. J.-C.", "", "vers 1650"];
    let references = [
        "Réf Livre", "Réf_Article", "réf discours", "Ref Film", "Réf Chanson", "Réf Émission", "Réf Nouvelle",
        "Réf Inconnue", "Modèle:Réf Livre", "Pas une réf", "Ref  Emission",
    ];
    let sections = ["== [[Blaise Pascal]] ==", "=== Partie ===", "== Sans lien ==", "== [[Catégorie:Truc|Texte]] ==", "==[[w:fr:Platon|Platon]]==", "= Titre =", "== [[Sans fin =="];

    let mut page = String::new();
    for _ in 0..1 + hasard.sous(5) {
        if hasard.sous(3) > 0 {
            page.push_str(choisir(hasard, &sections));
            page.push('\n');
        }
        for _ in 0..1 + hasard.sous(3) {
            let texte = textes[usize::try_from(hasard.sous(textes.len() as u64)).expect("rang")].clone();
            match hasard.sous(4) {
                0 => write!(page, "{{{{citation|{texte}}}}}").expect("écriture"),
                1 => write!(page, "{{{{Citation|citation={texte}|précisions={}}}}}", choisir(hasard, &titres)).expect("écriture"),
                _ => write!(page, "{{{{citation|citation={texte}}}}}").expect("écriture"),
            }
            page.push_str(choisir(hasard, &["\n", "", " }\n", "\n\n", "\r\n"]));
            if hasard.sous(4) == 0 {
                page.push_str("{{Choisie citation du jour|année=2007|mois=mai|jour=3}}\n");
            }
            if hasard.sous(6) > 0 {
                let mut champs = Vec::new();
                for (cle, valeurs) in [
                    ("titre", &titres[..]),
                    ("auteur", &auteurs[..]),
                    ("année", &dates[..]),
                    ("date", &dates[..]),
                    ("année d'origine", &dates[..]),
                    ("année de la contribution", &dates[..]),
                    ("publication", &titres[..]),
                    ("titre de la contribution", &titres[..]),
                    ("auteur de la contribution", &auteurs[..]),
                    ("page", &["142", "12-14", "  ", "{{inconnu}}"][..]),
                ] {
                    if hasard.sous(3) == 0 {
                        champs.push(format!("{cle}={}", choisir(hasard, valeurs)));
                    }
                }
                writeln!(page, "{{{{{}|{}}}}}", choisir(hasard, &references), champs.join("|")).expect("écriture");
            }
        }
    }
    match hasard.sous(10) {
        0 => page.push_str("{{citation|non fermé"),
        // Page sans saut de ligne final : la référence est sur la dernière ligne.
        1 | 2 => page.truncate(page.trim_end_matches(['\n', '\r']).len()),
        _ => {}
    }
    page
}

fn citation_en_json(citation: &coffre::ContenuCitation) -> Value {
    serde_json::to_value(citation).expect("JSON")
}

/// Écrit `pages.txt`, `domaines.txt` et `citations.json` dans `dossier`.
pub fn ecrire(dossier: &Path) {
    let mut hasard = SplitMix(0x5749_4B49_5155_4F54);
    fs::create_dir_all(dossier).expect("dossier");
    fs::write(dossier.join("pages.txt"), pages(&mut hasard)).expect("pages.txt");
    fs::write(dossier.join("domaines.txt"), domaines(&mut hasard)).expect("domaines.txt");
    let mut wikitextes = exemples_de_la_0x();
    for _ in 0..800 {
        wikitextes.push(page_au_hasard(&mut hasard));
    }
    let crochet = panic::take_hook();
    panic::set_hook(Box::new(|_| {}));
    let cas: Vec<Value> = wikitextes
        .iter()
        .enumerate()
        .map(|(n, wikitexte)| {
            let domaine = DOMAINES[n % DOMAINES.len()];
            let page = PageWikiquote { domaine: domaine.nom, titre: domaine.pages[n % domaine.pages.len()] };
            // La 0.x panique quand un caractère de plus d'un octet chevauche la fenêtre de
            // 12 octets d'une entité : le cas est noté, la suite C# vérifie qu'il passe.
            match panic::catch_unwind(|| citations_de_la_page(wikitexte, &page)) {
                Ok(citations) => json!({
                    "domaine": page.domaine,
                    "titre": page.titre,
                    "wikitexte": wikitexte,
                    "citations": citations.iter().map(citation_en_json).collect::<Vec<_>>(),
                }),
                Err(_) => json!({
                    "domaine": page.domaine,
                    "titre": page.titre,
                    "wikitexte": wikitexte,
                    "panique": true,
                }),
            }
        })
        .collect();
    panic::set_hook(crochet);
    let paniques = cas.iter().filter(|c| c.get("panique").is_some()).count();
    let trouvees: usize = cas.iter().map(|c| c["citations"].as_array().map_or(0, Vec::len)).sum();
    fs::write(dossier.join("citations.json"), serde_json::to_string_pretty(&cas).expect("JSON") + "\n").expect("citations.json");
    println!("{} wikitextes, {trouvees} citations retenues, {paniques} paniques de la 0.x", cas.len());
}
