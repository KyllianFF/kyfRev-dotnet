// Chemins des liens Markdown, résolus comme côté Rust (crates/coffre/src/liens.rs).

/** Retire les échappements « \x » et décode les « %XX » (« %20 » → espace). */
export function decoder(brut: string): string {
  const sansEchappements = brut.replace(/\\([!-/:-@[-`{-~])/g, "$1");
  try {
    return decodeURIComponent(sansEchappements.replace(/%(?![0-9a-fA-F]{2})/g, "%25"));
  } catch {
    return sansEchappements;
  }
}

/** Adresse externe (https:, mailto:…), chemin absolu ou simple ancre. */
export function estExterne(cible: string): boolean {
  return cible === "" || cible.startsWith("/") || cible.startsWith("\\") || /^[^/]*:/.test(cible);
}

/** Chemin relatif au coffre désigné par `relatif` depuis `dossier` ; `null` s'il sort du coffre. */
export function resoudre(dossier: string, relatif: string): string | null {
  const parties = dossier.split("/").filter((p) => p !== "");
  for (const partie of relatif.split("/")) {
    if (partie === "" || partie === ".") continue;
    if (partie === "..") {
      if (parties.length === 0) return null;
      parties.pop();
    } else {
      parties.push(partie);
    }
  }
  return parties.join("/");
}

/** Sépare « chemin#section » en chemin et suffixe. */
export function separerSuffixe(brut: string): [string, string] {
  const coupure = brut.search(/[#?]/);
  return coupure === -1 ? [brut, ""] : [brut.slice(0, coupure), brut.slice(coupure)];
}
