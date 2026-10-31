//! Opérations des scénarios sur les annotations, les rappels, les réglages et examens, les
//! données d'extension, les citations, les schémas et les modèles.

use coffre::wikiquote::PageWikiquote;
use coffre::{
    Ancre, Annotation, ChoixRappel, Citation, ContenuCitation, Definition, ErreurCoffre, EtatAncrage, Examen,
    SourceCitation, TypeDeSource,
};
use jiff::civil::Date;
use noyau::Moment;
use serde_json::Value;

use super::{Partie, json, liste};

fn optionnel(texte: Option<&str>) -> String {
    texte.map_or_else(|| "null".to_owned(), json)
}

fn chaine(valeur: &Value, cle: &str) -> Option<String> {
    valeur.get(cle).and_then(Value::as_str).map(str::to_owned)
}

fn textes(valeur: &Value) -> Vec<String> {
    valeur.as_array().map(|v| v.iter().filter_map(|t| t.as_str().map(str::to_owned)).collect()).unwrap_or_default()
}

fn genre(nom: &str) -> TypeDeSource {
    match nom {
        "livre" => TypeDeSource::Livre,
        "journal" => TypeDeSource::Journal,
        "article" => TypeDeSource::Article,
        "discours" => TypeDeSource::Discours,
        "site" => TypeDeSource::Site,
        "film" => TypeDeSource::Film,
        "chanson" => TypeDeSource::Chanson,
        "emission" => TypeDeSource::Emission,
        "inconnue" => TypeDeSource::Inconnue,
        _ => TypeDeSource::Autre,
    }
}

fn contenu(valeur: &Value) -> ContenuCitation {
    let source = &valeur["source"];
    ContenuCitation {
        texte: chaine(valeur, "texte").unwrap_or_default(),
        auteur: chaine(valeur, "auteur"),
        date: chaine(valeur, "date"),
        source: SourceCitation {
            genre: source.get("type").and_then(Value::as_str).map(genre),
            titre: chaine(source, "titre"),
            page: chaine(source, "page"),
            url: chaine(source, "url"),
        },
        contexte: chaine(valeur, "contexte"),
        etiquettes: textes(&valeur["etiquettes"]),
    }
}

fn citation(c: &Citation) -> String {
    let s = &c.contenu.source;
    format!(
        "{} {} {} {} {} {} {} {} {} {} {} {} {} {}",
        c.chemin,
        c.id.as_deref().unwrap_or("-"),
        c.annee.map_or_else(|| "-".to_owned(), |a| a.to_string()),
        c.ajoutee.map_or_else(|| "-".to_owned(), |j| j.to_string()),
        c.origine.as_deref().unwrap_or("-"),
        json(&c.contenu.texte),
        optionnel(c.contenu.auteur.as_deref()),
        optionnel(c.contenu.date.as_deref()),
        s.genre.map_or_else(|| "-".to_owned(), |g| format!("{g:?}")),
        optionnel(s.titre.as_deref()),
        optionnel(s.page.as_deref()),
        optionnel(s.url.as_deref()),
        optionnel(c.contenu.contexte.as_deref()),
        liste(&c.contenu.etiquettes),
    )
}

fn annotation(a: &Annotation) -> String {
    let suivi = match a.suivi.cycle() {
        Some(cycle) => format!("cycle {} {} {}", cycle.numero, cycle.etape, cycle.echeance),
        None => "archive".to_owned(),
    };
    format!(
        "{} {} {:?} {} {} {} {} {} {} {}",
        a.fiche_id,
        json(&a.ancre.exact),
        a.etat_ancrage,
        optionnel(a.commentaire.as_deref()),
        optionnel(a.definition.as_ref().map(|d| d.terme.as_str())),
        optionnel(a.definition.as_ref().map(|d| d.explication.as_str())),
        a.corbeille_le.map_or_else(|| "-".to_owned(), |t| t.to_string()),
        a.ancre.debut,
        a.ancre.fin,
        suivi,
    )
}

fn definition(operation: &Value) -> Definition {
    Definition {
        terme: chaine(operation, "terme").unwrap_or_default(),
        explication: chaine(operation, "explication").unwrap_or_default(),
    }
}

impl Partie {
    fn reference(&self, operation: &Value, cle: &str) -> String {
        let nom = operation[cle].as_str().unwrap_or_else(|| panic!("« {cle} » attendu"));
        self.noms.get(nom).cloned().unwrap_or_else(|| nom.to_owned())
    }

    fn ancre(operation: &Value) -> Ancre {
        let ancre = &operation["ancre"];
        Ancre {
            exact: chaine(ancre, "exact").unwrap_or_default(),
            avant: chaine(ancre, "avant").unwrap_or_default(),
            apres: chaine(ancre, "apres").unwrap_or_default(),
            debut: usize::try_from(ancre["debut"].as_u64().unwrap_or(0)).expect("position"),
            fin: usize::try_from(ancre["fin"].as_u64().unwrap_or(0)).expect("position"),
            section: textes(&ancre["section"]),
            ligne_source: ancre.get("ligne_source").and_then(Value::as_u64).map(|n| u32::try_from(n).expect("ligne")),
            empreinte_fiche: chaine(ancre, "empreinte_fiche").unwrap_or_default(),
        }
    }

    /// Une annotation refusée parce que le fichier de la fiche est illisible a déjà tiré son
    /// identifiant : une valeur neutre le remplace dans la liste rejouée côté C#.
    fn identifiant_perdu(&mut self, resultat: Result<String, ErreurCoffre>) -> Result<String, ErreurCoffre> {
        if let Err(ErreurCoffre::AnnotationsIllisibles(_)) = &resultat {
            self.identifiants.push("0".repeat(26));
        }
        resultat
    }

    fn nommer(&mut self, operation: &Value, valeur: String) {
        self.noms.insert(operation["nom"].as_str().expect("« nom »").to_owned(), valeur);
    }

    #[allow(clippy::too_many_lines)]
    pub(super) fn jouer_complements(&mut self, op: &str, operation: &Value, moment: Option<Moment>) -> Result<String, ErreurCoffre> {
        let maintenant = || moment.expect("« le » attendu");
        match op {
            "creer_annotation" => {
                let fiche = self.reference(operation, "fiche");
                let commentaire = chaine(operation, "commentaire");
                let resultat = self.coffre().creer_annotation(&fiche, Self::ancre(operation), commentaire.as_deref(), &maintenant());
                let id = self.identifiant_perdu(resultat)?;
                self.nouvel_identifiant(&id);
                self.nommer(operation, id);
                Ok(String::new())
            }
            "creer_definition" => {
                let fiche = self.reference(operation, "fiche");
                let resultat = self.coffre().creer_definition(&fiche, Self::ancre(operation), definition(operation), &maintenant());
                let id = self.identifiant_perdu(resultat)?;
                self.nouvel_identifiant(&id);
                self.nommer(operation, id);
                Ok(String::new())
            }
            "modifier_definition" => {
                let id = self.reference(operation, "annotation");
                self.coffre().modifier_definition(&id, definition(operation), &maintenant()).map(|()| String::new())
            }
            "modifier_commentaire" => {
                let (id, commentaire) = (self.reference(operation, "annotation"), chaine(operation, "commentaire"));
                self.coffre().modifier_commentaire(&id, commentaire.as_deref(), &maintenant()).map(|()| String::new())
            }
            "reancrer" => {
                let id = self.reference(operation, "annotation");
                let etat = match operation["etat"].as_str() {
                    Some("a_verifier") => EtatAncrage::AVerifier,
                    Some("introuvable") => EtatAncrage::Introuvable,
                    _ => EtatAncrage::Ancree,
                };
                self.coffre().reancrer_annotation(&id, Self::ancre(operation), etat, &maintenant()).map(|()| String::new())
            }
            "modifier_passage" => {
                let id = self.reference(operation, "annotation");
                let recommencer = operation["recommencer"].as_bool().unwrap_or(false);
                self.coffre().modifier_passage(&id, Self::ancre(operation), recommencer, &maintenant()).map(|()| String::new())
            }
            "corbeille_annotation" => {
                let id = self.reference(operation, "annotation");
                self.coffre().mettre_annotation_a_la_corbeille(&id, &maintenant()).map(|()| String::new())
            }
            "restaurer_annotation" => {
                let id = self.reference(operation, "annotation");
                self.coffre().restaurer_annotation(&id, &maintenant()).map(|()| String::new())
            }
            "reviser_annotation" => {
                let (id, m) = (self.reference(operation, "annotation"), maintenant());
                self.coffre().appliquer_au_suivi_annotation(&id, |s| s.valider_revision(&m, None, None)).map(|_| String::new())
            }
            "reporter_annotation" => {
                let (id, m) = (self.reference(operation, "annotation"), maintenant());
                let vers: Date = chaine(operation, "vers").expect("« vers »").parse().expect("date");
                self.coffre().appliquer_au_suivi_annotation(&id, |s| s.reporter(&m, vers)).map(|()| String::new())
            }
            "annotation" => {
                let id = self.reference(operation, "annotation");
                self.coffre().annotation(&id).map(annotation)
            }
            "annotations_a_verifier" => Ok(self.coffre().annotations_a_verifier().to_string()),
            "passages_marques" => {
                let fiche = self.reference(operation, "fiche");
                Ok(self.coffre().passages_marques(&fiche, maintenant().jour).to_string())
            }
            "traiter_rappel" => {
                let fiche = self.reference(operation, "fiche");
                let choix = match operation["choix"].as_str() {
                    Some("certains_points_a_revoir") => ChoixRappel::CertainsPointsARevoir,
                    Some("a_revoir_entierement") => ChoixRappel::ARevoirEntierement,
                    _ => ChoixRappel::Connue,
                };
                self.coffre().traiter_rappel(&fiche, choix, &maintenant()).map(|()| String::new())
            }
            "inclure_dans_rappels" => {
                let fiche = self.reference(operation, "fiche");
                let inclure = operation["inclure"].as_bool().expect("« inclure »");
                self.coffre().inclure_dans_rappels(&fiche, inclure, &maintenant()).map(|()| String::new())
            }
            "preparer_journee" => {
                let journee = self.coffre().preparer_journee(&maintenant())?;
                Ok(if journee.deja_preparee {
                    "deja".to_owned()
                } else {
                    format!("[{}] {}", journee.nouveaux_rappels.join(";"), journee.prioritaires_non_servis)
                })
            }
            "reglages" => {
                let r = self.coffre().reglages();
                Ok(format!(
                    "etapes={:?} rappel_actif={} max_par_jour={} attente={} repos={:?} inclure={} rotation={} citations={} en_ligne={} domaines={} schemas={}",
                    r.etapes,
                    r.rappel_actif,
                    r.max_par_jour,
                    r.attente_max_jours,
                    r.sequence_repos_mois,
                    r.inclure_archivage_manuel,
                    r.rotation_des_sauvegardes,
                    r.citations_actives,
                    r.citations_en_ligne,
                    liste(&r.domaines_citations),
                    r.schemas_actifs
                ))
            }
            "modifier_reglages" => {
                let mut r = self.coffre().reglages();
                let nombres = |v: &Value| v.as_array().map(|a| a.iter().filter_map(Value::as_u64).collect::<Vec<_>>());
                if let Some(v) = nombres(&operation["etapes"]) {
                    r.etapes = v.iter().map(|&n| u16::try_from(n).expect("étape")).collect();
                }
                if let Some(v) = operation["rappel_actif"].as_bool() {
                    r.rappel_actif = v;
                }
                if let Some(v) = operation["max_par_jour"].as_u64() {
                    r.max_par_jour = u8::try_from(v).expect("rappels par jour");
                }
                if let Some(v) = operation["attente_max_jours"].as_u64() {
                    r.attente_max_jours = u16::try_from(v).expect("attente");
                }
                if let Some(v) = nombres(&operation["sequence_repos_mois"]) {
                    r.sequence_repos_mois = v.iter().map(|&n| u8::try_from(n).expect("repos")).collect();
                }
                if let Some(v) = operation["inclure_archivage_manuel"].as_bool() {
                    r.inclure_archivage_manuel = v;
                }
                if let Some(v) = operation["rotation_des_sauvegardes"].as_bool() {
                    r.rotation_des_sauvegardes = v;
                }
                if let Some(v) = operation["citations_actives"].as_bool() {
                    r.citations_actives = v;
                }
                if let Some(v) = operation["citations_en_ligne"].as_bool() {
                    r.citations_en_ligne = v;
                }
                if operation.get("domaines_citations").is_some() {
                    r.domaines_citations = textes(&operation["domaines_citations"]);
                }
                if let Some(v) = operation["schemas_actifs"].as_bool() {
                    r.schemas_actifs = v;
                }
                self.coffre().modifier_reglages(r).map(|()| String::new())
            }
            "definir_examen" => {
                let examen = Examen {
                    dossier: chaine(operation, "dossier").unwrap_or_default(),
                    nom: chaine(operation, "nom_examen").unwrap_or_default(),
                    jour: chaine(operation, "jour").expect("« jour »").parse().expect("date"),
                };
                self.coffre().definir_examen(examen).map(|()| String::new())
            }
            "retirer_examen" => {
                let dossier = chaine(operation, "dossier").unwrap_or_default();
                self.coffre().retirer_examen(&dossier).map(|()| String::new())
            }
            "examens_a_venir" => Ok(self
                .coffre()
                .examens_a_venir(maintenant().jour)?
                .iter()
                .map(|e| format!("{} {} {} {} {} {}", json(&e.dossier), json(&e.nom), e.jour, e.jours_restants, e.elements, e.apres_examen))
                .collect::<Vec<_>>()
                .join(";")),
            "resserrer" => {
                let dossier = chaine(operation, "dossier").unwrap_or_default();
                self.coffre().resserrer_avant_examen(&dossier, &maintenant()).map(|n| n.to_string())
            }
            "enregistrer_donnees_extension" => {
                let (extension, texte) = (chaine(operation, "extension").unwrap_or_default(), self.texte(operation, "texte"));
                self.coffre().enregistrer_donnees_extension(&extension, &texte).map(|()| String::new())
            }
            "donnees_extension" => {
                let extension = chaine(operation, "extension").unwrap_or_default();
                self.coffre().donnees_extension(&extension).map(|d| optionnel(d.as_deref()))
            }
            "ajouter_citation" => {
                let c = self.coffre().ajouter_citation(&contenu(operation), &maintenant())?;
                if let Some(id) = &c.id {
                    self.nouvel_identifiant(id);
                }
                self.nommer(operation, c.chemin.clone());
                Ok(citation(&c))
            }
            "modifier_citation" => {
                let chemin = self.reference(operation, "citation");
                // Un identifiant n'est tiré que si la citation n'en avait pas.
                let avait_un_id = self.coffre().citations().ok().is_some_and(|l| {
                    l.citations.iter().chain(&l.corbeille).any(|c| c.chemin == chemin && c.id.is_some())
                });
                let c = self.coffre().modifier_citation(&chemin, &contenu(operation), &maintenant())?;
                if let Some(id) = c.id.as_ref().filter(|_| !avait_un_id) {
                    self.nouvel_identifiant(id);
                }
                Ok(citation(&c))
            }
            "corbeille_citation" | "restaurer_citation" => {
                let nom = operation["citation"].as_str().expect("« citation »").to_owned();
                let chemin = self.reference(operation, "citation");
                let nouveau = if op == "corbeille_citation" {
                    self.coffre().mettre_citation_a_la_corbeille(&chemin)?
                } else {
                    self.coffre().restaurer_citation(&chemin)?
                };
                if self.noms.contains_key(&nom) {
                    self.noms.insert(nom, nouveau.clone());
                }
                Ok(nouveau)
            }
            "citations" => {
                let l = self.coffre().citations()?;
                let chemins = |v: &[Citation]| v.iter().map(|c| c.chemin.clone()).collect::<Vec<_>>().join(";");
                Ok(format!(
                    "citations=[{}] corbeille=[{}] illisibles=[{}]",
                    chemins(&l.citations),
                    chemins(&l.corbeille),
                    l.illisibles.iter().map(|i| i.chemin.clone()).collect::<Vec<_>>().join(";")
                ))
            }
            "lire_citation" => {
                let chemin = self.reference(operation, "citation");
                let l = self.coffre().citations()?;
                Ok(l.citations.iter().chain(&l.corbeille).find(|c| c.chemin == chemin).map_or_else(|| "absente".to_owned(), citation))
            }
            "retenir_citation_en_ligne" => {
                let titre: &'static str = Box::leak(chaine(operation, "page").unwrap_or_default().into_boxed_str());
                let page = PageWikiquote { domaine: "philosophie", titre };
                let trouvees: Vec<ContenuCitation> =
                    operation["trouvees"].as_array().map(|v| v.iter().map(contenu).collect()).unwrap_or_default();
                match self.coffre().retenir_citation_en_ligne(&page, &trouvees, &maintenant())? {
                    Some(c) => {
                        if let Some(id) = &c.id {
                            self.nouvel_identifiant(id);
                        }
                        if let Some(nom) = operation.get("nom").and_then(Value::as_str) {
                            self.noms.insert(nom.to_owned(), c.chemin.clone());
                        }
                        Ok(citation(&c))
                    }
                    None => Ok("aucune".to_owned()),
                }
            }
            "citation_du_jour" => {
                Ok(self.coffre().citation_du_jour(maintenant().jour).map_or_else(|| "aucune".to_owned(), |c| citation(&c)))
            }
            "pages_a_lire" => Ok(self
                .coffre()
                .pages_a_lire(maintenant().jour)
                .iter()
                .map(|p| format!("{}:{}", p.domaine, p.titre))
                .collect::<Vec<_>>()
                .join(",")),
            "creer_schema" => {
                let texte = self.texte(operation, "texte");
                let lu = self.coffre().creer_schema(&texte)?;
                self.nouvel_identifiant(&lu.id);
                self.nommer(operation, lu.id.clone());
                Ok(lu.empreinte)
            }
            "dupliquer_schema" => {
                let id = self.reference(operation, "schema");
                let lu = self.coffre().dupliquer_schema(&id)?;
                self.nouvel_identifiant(&lu.id);
                self.nommer(operation, lu.id.clone());
                Ok(lu.empreinte)
            }
            "enregistrer_schema" => {
                let (id, texte) = (self.reference(operation, "schema"), self.texte(operation, "texte"));
                let empreinte = match operation["empreinte"].as_str() {
                    Some("actuelle") => self.coffre().lire_schema(&id).map(|l| l.empreinte).unwrap_or_default(),
                    Some(autre) => autre.to_owned(),
                    None => panic!("« empreinte » attendue"),
                };
                self.coffre().enregistrer_schema(&id, &texte, &empreinte)
            }
            "lire_schema" => {
                let id = self.reference(operation, "schema");
                self.coffre().lire_schema(&id).map(|l| format!("{} {}", l.empreinte, json(&l.texte)))
            }
            "modeles" => Ok(self
                .coffre()
                .modeles()
                .iter()
                .map(|m| format!("{} {} {}", json(&m.nom), m.integre, json(&m.description)))
                .collect::<Vec<_>>()
                .join(";")),
            "corps_du_modele" => {
                let nom = chaine(operation, "modele").unwrap_or_default();
                let titre = chaine(operation, "titre").unwrap_or_default();
                let dossier = chaine(operation, "dossier").unwrap_or_default();
                Ok(json(&self.coffre().corps_du_modele(&nom, &titre, &dossier, maintenant().jour)))
            }
            "enregistrer_modele" => {
                let (fiche, nom) = (self.reference(operation, "fiche"), chaine(operation, "nom_modele").unwrap_or_default());
                self.coffre().enregistrer_modele(&fiche, &nom).map(|n| json(&n))
            }
            autre => self.jouer_sauvegardes(autre, operation, moment),
        }
    }
}
