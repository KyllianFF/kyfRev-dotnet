// Dates « civiles » (AAAA-MM-JJ, sans heure) affichées en français.

const formatLong = new Intl.DateTimeFormat("fr-FR", { weekday: "long", day: "numeric", month: "long" });
const formatCourt = new Intl.DateTimeFormat("fr-FR", { weekday: "short", day: "numeric", month: "short" });

/** « 2026-10-09 » → date locale à midi (évite tout décalage de fuseau). */
export function versDate(jour: string): Date {
  const [annee = 1970, mois = 1, quantieme = 1] = jour.split("-").map(Number);
  return new Date(annee, mois - 1, quantieme, 12);
}

/** Date locale du jour au format AAAA-MM-JJ. */
export function aujourdhuiIso(decalageJours = 0): string {
  const date = new Date();
  date.setDate(date.getDate() + decalageJours);
  const deux = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${deux(date.getMonth() + 1)}-${deux(date.getDate())}`;
}

/** « vendredi 9 octobre » */
export function jourLong(jour: string): string {
  return formatLong.format(versDate(jour));
}

/** « ven. 9 oct. » */
export function jourCourt(jour: string): string {
  return formatCourt.format(versDate(jour));
}

/** « Vendredi 9 octobre » */
export function majuscule(texte: string): string {
  return texte.charAt(0).toLocaleUpperCase("fr-FR") + texte.slice(1);
}

/** « 1 élément », « 3 éléments » */
export function pluriel(nombre: number, singulier: string, pluriel = `${singulier}s`): string {
  return `${nombre} ${nombre > 1 ? pluriel : singulier}`;
}

const formatMoment = new Intl.DateTimeFormat("fr-FR", {
  weekday: "short",
  day: "numeric",
  month: "short",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

/** « sam. 10 oct. 2026, 09:31 » (heure locale) pour un horodatage ISO. */
export function momentCourt(horodatage: string): string {
  return formatMoment.format(new Date(horodatage));
}

/** « 840 octets », « 12,4 Ko », « 3,1 Mo » */
export function taille(octets: number): string {
  if (octets < 1024) return pluriel(octets, "octet");
  const nombre = (valeur: number) => valeur.toLocaleString("fr-FR", { maximumFractionDigits: 1 });
  if (octets < 1024 * 1024) return `${nombre(octets / 1024)} Ko`;
  return `${nombre(octets / (1024 * 1024))} Mo`;
}
