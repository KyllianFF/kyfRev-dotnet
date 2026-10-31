//! Coffre de référence : un coffre synthétique construit par le crate `coffre` de kyfRev 0.x,
//! qui contient chaque sorte de fichier du format 1 (suivis actifs et archivés, annotations et
//! définitions, corbeilles, versions, journal d'opération, tirages, citations, examens,
//! schéma, données d'extension). Les suivis réunis contiennent chaque sorte d'événement,
//! d'origine de J0, de résultat de rappel et d'état d'ancrage. Il sert de corpus aux tests de
//! compatibilité de KyfRev.Infrastructure.Vault ; son contenu est fictif.
//!
//! Les horodatages des actions sont fixés (midi UTC) ; les identifiants et quelques dates
//! d'enregistrement viennent de l'horloge : le corpus est généré une fois puis versionné.

use std::fs;
use std::path::Path;

use coffre::{
    Ancre, CheminRelatif, ChoixRappel, Coffre, ContenuCitation, Definition, EtatAncrage, Examen,
    SourceCitation, TypeDeSource,
};
use jiff::civil::date;
use jiff::tz::TimeZone;
use noyau::{Jour, Moment};

fn le(jour: Jour) -> Moment {
    let instant = jour
        .at(12, 0, 0, 0)
        .to_zoned(TimeZone::UTC)
        .expect("date dans les limites")
        .timestamp();
    Moment::nouveau(jour, instant)
}

fn ancre(exact: &str, avant: &str, apres: &str, debut: usize, section: &[&str], empreinte: &str) -> Ancre {
    Ancre {
        exact: exact.into(),
        avant: avant.into(),
        apres: apres.into(),
        debut,
        fin: debut + exact.encode_utf16().count(),
        section: section.iter().map(|&s| s.to_owned()).collect(),
        ligne_source: Some(7),
        empreinte_fiche: empreinte.into(),
    }
}

const REVOLUTION: &str = "\
# Révolution française : les causes

## Causes sociales

La société d'Ancien Régime est divisée en trois ordres : le clergé, la noblesse
et le tiers état. Le **tiers état** paie l'essentiel des impôts.

## Causes financières

- Dette publique très élevée
- Crise de 1788 : mauvaises récoltes, prix du pain en hausse
";

const MITOSE: &str = "\
# Mitose

Division d'une cellule en **deux cellules filles** identiques.

1. Prophase
2. Métaphase
3. Anaphase
4. Télophase
";

const PYTHAGORE: &str = "\
# Théorème de Pythagore

Dans un triangle rectangle, $a^2 + b^2 = c^2$, où $c$ est l'hypoténuse.
";

const PHOTOSYNTHESE: &str = "\
# Photosynthèse

Grâce à la **chlorophylle**, la plante produit du glucose à partir d'eau, de dioxyde de
carbone et de lumière.
";

/// Fichier écrit hors de kyfRev, sans en-tête, puis suivi (« Suivre cette fiche »).
const COGITO: &str = "\
# Cogito

« Je pense, donc je suis » : Descartes, Discours de la méthode, 1637.
";

/// Construit le coffre de référence dans `dossier` (qui doit être absent ou vide).
pub fn construire(dossier: &Path) {
    let debut = date(2026, 9, 1);
    let mut coffre = Coffre::initialiser(dossier, &le(debut)).expect("initialisation");
    let regles = coffre.parametres().rappels.clone();

    // Réglages : citations actives, module Schémas choisi, un examen.
    let mut reglages = coffre.reglages();
    reglages.citations_actives = true;
    reglages.schemas_actifs = true;
    coffre.modifier_reglages(reglages).expect("réglages");
    coffre
        .definir_examen(Examen { dossier: "Histoire".into(), nom: "Bac blanc".into(), jour: date(2026, 12, 15) })
        .expect("examen");

    // Fiche active, révisée deux fois, avec un passage annoté et une définition.
    let revolution = coffre
        .creer_fiche(Some("Histoire"), "Révolution française : les causes", REVOLUTION, &le(debut))
        .expect("fiche");
    for jour in [date(2026, 9, 2), date(2026, 9, 5)] {
        coffre
            .appliquer_au_suivi(&revolution, |s| s.valider_revision(&le(jour), None, Some(&regles)))
            .expect("révision");
    }
    let empreinte = coffre.lire_fiche(&revolution).expect("lecture").empreinte;
    let passage = coffre
        .creer_annotation(
            &revolution,
            ancre("divisée en trois ordres", "Ancien Régime est ", " : le clergé, la", 72, &["Révolution française : les causes", "Causes sociales"], &empreinte),
            Some("Clergé, noblesse, tiers état"),
            &le(date(2026, 9, 3)),
        )
        .expect("passage");
    coffre
        .appliquer_au_suivi_annotation(&passage, |s| s.valider_revision(&le(date(2026, 9, 4)), None, None))
        .expect("révision du passage");
    let definition = coffre
        .creer_definition(
            &revolution,
            ancre("Ancien Régime", "La société d'", " est divisée", 59, &["Révolution française : les causes", "Causes sociales"], &empreinte),
            Definition { terme: "Ancien Régime".into(), explication: "La France d'avant 1789, divisée en trois ordres.".into() },
            &le(date(2026, 9, 3)),
        )
        .expect("définition");
    let jetee = coffre
        .creer_annotation(
            &revolution,
            ancre("prix du pain", "mauvaises récoltes, ", " en hausse", 260, &["Révolution française : les causes", "Causes financières"], &empreinte),
            None,
            &le(date(2026, 9, 3)),
        )
        .expect("surlignage");
    coffre.mettre_annotation_a_la_corbeille(&jetee, &le(date(2026, 9, 6))).expect("corbeille d'annotation");

    // Modifications inscrites dans l'historique : commentaire, définition, puis passage remis à J0.
    coffre
        .modifier_commentaire(&passage, Some("Clergé, noblesse et tiers état"), &le(date(2026, 9, 7)))
        .expect("commentaire modifié");
    coffre
        .modifier_definition(
            &definition,
            Definition { terme: "Ancien Régime".into(), explication: "La France d'avant 1789, organisée en trois ordres.".into() },
            &le(date(2026, 9, 7)),
        )
        .expect("définition modifiée");
    coffre
        .modifier_passage(
            &passage,
            ancre("trois ordres", "est divisée en ", " : le clergé, la", 83, &["Révolution française : les causes", "Causes sociales"], &empreinte),
            true,
            &le(date(2026, 9, 8)),
        )
        .expect("passage modifié");

    // Passages dont la position est à vérifier, ou introuvable, après une modification de la fiche.
    let section_sociale = ["Révolution française : les causes", "Causes sociales"];
    let incertain = coffre
        .creer_annotation(&revolution, ancre("tiers état", "et le ", ". Le **", 128, &section_sociale, &empreinte), None, &le(date(2026, 9, 3)))
        .expect("passage");
    coffre
        .reancrer_annotation(&incertain, ancre("tiers état", "Le **", "** paie", 141, &section_sociale, &empreinte), EtatAncrage::AVerifier, &le(date(2026, 9, 9)))
        .expect("position à vérifier");
    let section_financiere = ["Révolution française : les causes", "Causes financières"];
    let perdu = coffre
        .creer_annotation(&revolution, ancre("Dette publique", "financières\n\n- ", " très élevée", 205, &section_financiere, &empreinte), Some("Dette de l'État"), &le(date(2026, 9, 3)))
        .expect("passage");
    coffre
        .reancrer_annotation(&perdu, ancre("Dette publique", "financières\n\n- ", " très élevée", 205, &section_financiere, &empreinte), EtatAncrage::Introuvable, &le(date(2026, 9, 9)))
        .expect("passage introuvable");

    // Fiche modifiée deux fois dans deux sessions (versions), étiquetée, déplacée, reportée
    // puis archivée manuellement.
    let mitose = coffre
        .creer_fiche(Some("Biologie/Cellule"), "Mitose", MITOSE, &le(debut))
        .expect("fiche");
    for (session, ajout) in [(1_u64, "\nVoir aussi : méiose.\n"), (2, "\nDurée : environ une heure.\n")] {
        let contenu = coffre.lire_fiche(&mitose).expect("lecture");
        let corps = contenu.texte.split_once("\n---\n").map_or(contenu.texte.clone(), |(_, c)| c.to_owned());
        coffre
            .enregistrer_corps(&mitose, &format!("{corps}{ajout}"), &contenu.empreinte, session, &le(date(2026, 9, 2)))
            .expect("enregistrement");
    }
    coffre
        .modifier_etiquettes(&mitose, &["biologie".into(), "examen".into()], &le(date(2026, 9, 2)))
        .expect("étiquettes");
    coffre.deplacer_fiche(&mitose, Some("Biologie"), &le(date(2026, 9, 2))).expect("déplacement");
    coffre
        .appliquer_au_suivi(&mitose, |s| s.reporter(&le(date(2026, 9, 2)), date(2026, 9, 4)))
        .expect("report");
    coffre
        .appliquer_au_suivi(&mitose, |s| s.archiver(&le(date(2026, 9, 10)), Some(&regles)))
        .expect("archivage manuel");

    // Fiche révisée ponctuellement jusqu'à la fin de son cycle, puis tirée et déclarée connue.
    let pythagore = coffre
        .creer_fiche(Some("Mathématiques"), "Théorème de Pythagore", PYTHAGORE, &le(debut))
        .expect("fiche");
    while let Some(echeance) = coffre.fiche(&pythagore).expect("fiche").suivie.suivi.cycle().map(|c| c.echeance) {
        coffre
            .appliquer_au_suivi(&pythagore, |s| s.valider_revision(&le(echeance), None, Some(&regles)))
            .expect("révision");
    }

    // Fiche archivée à la main, tirée, dont certains points sont à revoir (un passage marqué le
    // jour du rappel), puis exclue des rappels.
    let photosynthese = coffre
        .creer_fiche(Some("Biologie"), "Photosynthèse", PHOTOSYNTHESE, &le(debut))
        .expect("fiche");
    coffre
        .appliquer_au_suivi(&photosynthese, |s| s.archiver(&le(date(2026, 9, 2)), Some(&regles)))
        .expect("archivage manuel");
    let rappel = date(2026, 11, 10);
    coffre.appliquer_au_suivi(&photosynthese, |s| s.marquer_tire(rappel)).expect("tirage");
    let empreinte = coffre.lire_fiche(&photosynthese).expect("lecture").empreinte;
    coffre
        .creer_annotation(&photosynthese, ancre("chlorophylle", "Grâce à la **", "**, la plante", 26, &["Photosynthèse"], &empreinte), None, &le(rappel))
        .expect("passage marqué");
    coffre
        .traiter_rappel(&photosynthese, ChoixRappel::CertainsPointsARevoir, &le(rappel))
        .expect("rappel");
    coffre
        .inclure_dans_rappels(&photosynthese, false, &le(date(2026, 11, 11)))
        .expect("exclusion des rappels");

    // Fichier existant suivi après coup (J0 « adoption »), révisé jusqu'à la fin de son cycle,
    // tiré puis à revoir entièrement : réactivé, avec un J0 « rappel ».
    let chemin = "fiches/Philosophie/Cogito.md";
    fs::create_dir_all(dossier.join("fiches/Philosophie")).expect("dossier");
    fs::write(dossier.join(chemin), COGITO).expect("fichier");
    let cogito = coffre
        .suivre_fichier(&CheminRelatif::nouveau(chemin).expect("chemin"), &le(debut))
        .expect("adoption");
    while let Some(echeance) = coffre.fiche(&cogito).expect("fiche").suivie.suivi.cycle().map(|c| c.echeance) {
        coffre
            .appliquer_au_suivi(&cogito, |s| s.valider_revision(&le(echeance), None, Some(&regles)))
            .expect("révision");
    }
    let rappel = date(2027, 1, 5);
    coffre.appliquer_au_suivi(&cogito, |s| s.marquer_tire(rappel)).expect("tirage");
    coffre
        .traiter_rappel(&cogito, ChoixRappel::ARevoirEntierement, &le(rappel))
        .expect("rappel");

    // Tirage du jour : Mitose et Pythagore sont les seules fiches éligibles (deux places).
    let tirage = date(2027, 1, 26);
    coffre.preparer_journee(&le(tirage)).expect("tirage du jour");
    coffre.traiter_rappel(&pythagore, ChoixRappel::Connue, &le(tirage)).expect("rappel");
    coffre
        .inclure_dans_rappels(&pythagore, false, &le(date(2027, 2, 1)))
        .expect("exclusion des rappels");
    coffre
        .inclure_dans_rappels(&pythagore, true, &le(date(2027, 2, 2)))
        .expect("réinclusion dans les rappels");

    // Fiche renommée (journal d'opération) et fiche mise à la corbeille.
    let renommee = coffre
        .creer_fiche(None, "Ancien titre", "# Ancien titre\n\nUne fiche à la racine.\n", &le(debut))
        .expect("fiche");
    coffre.changer_titre(&renommee, "Nouveau titre", &le(date(2026, 9, 2))).expect("renommage");
    let corbeille = coffre
        .creer_fiche(Some("Brouillons"), "Fiche abandonnée", "# Fiche abandonnée\n\nUn passage annoté.\n", &le(debut))
        .expect("fiche");
    let empreinte = coffre.lire_fiche(&corbeille).expect("lecture").empreinte;
    coffre
        .creer_annotation(&corbeille, ancre("passage annoté", "Un ", ".", 21, &["Fiche abandonnée"], &empreinte), None, &le(debut))
        .expect("passage");
    coffre.mettre_fiche_a_la_corbeille(&corbeille, &le(date(2026, 9, 3))).expect("corbeille");

    // Citations : une complète, une réduite à son texte.
    coffre
        .ajouter_citation(
            &ContenuCitation {
                texte: "Le cœur a ses raisons que la raison ne connaît point.".into(),
                auteur: Some("Blaise Pascal".into()),
                date: Some("1670".into()),
                source: SourceCitation { genre: Some(TypeDeSource::Livre), titre: Some("Pensées".into()), page: None, url: None },
                contexte: None,
                etiquettes: vec!["philosophie".into()],
            },
            &le(debut),
        )
        .expect("citation");
    coffre
        .ajouter_citation(
            &ContenuCitation { texte: "Rien ne se perd, rien ne se crée, tout se transforme.".into(), ..ContenuCitation::default() },
            &le(debut),
        )
        .expect("citation");

    // Schéma réseau et données d'extension.
    coffre
        .creer_schema(r#"{"format":2,"id":"s_00000000000000000000000000","titre":"Réseau de la salle","equipements":[],"liens":[]}"#)
        .expect("schéma");
    coffre.enregistrer_donnees_extension("quiz", r#"{"scores":{"f":3}}"#).expect("données d'extension");
}
