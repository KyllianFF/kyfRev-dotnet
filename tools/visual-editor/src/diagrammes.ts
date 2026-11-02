// La page cachée qui dessine les schémas et les graphiques : Mermaid, en mode strict, rien
// d'autre. Elle reçoit du code, et rend une image : aucun lien ni aucun script d'un
// schéma ne peut y agir, puisqu'il ne reste qu'un dessin.

declare const mermaid: {
  initialize(configuration: unknown): void;
  render(id: string, code: string): Promise<{ svg: string }>;
};

declare const chrome: {
  webview: {
    postMessage(message: unknown): void;
    addEventListener(type: "message", ecoute: (evenement: { data: unknown }) => void): void;
  };
};

type Demande = { id: number; code: string; theme: Record<string, string> } | { id: number; svg: string };

/** Une image deux fois plus fine que sa taille : elle reste nette sur un écran agrandi. */
const FINESSE = 2;

/** Pas d'étiquettes HTML : elles empêcheraient d'en faire une image. */
const POLICE = '"Segoe UI", sans-serif';

/** Les suites de couleurs de Mermaid (cartes mentales, tableaux, cartes proportionnelles) : celles du thème, en boucle. */
function suites(theme: Record<string, string>): Record<string, string> {
  const fonds = [theme.primaryColor, theme.secondaryColor, theme.tertiaryColor];
  const textes = [theme.primaryTextColor, theme.secondaryTextColor, theme.tertiaryTextColor];
  const bords = [theme.primaryBorderColor, theme.secondaryBorderColor, theme.tertiaryBorderColor];
  const suite: Record<string, string> = {};
  for (let i = 0; i < 12; i++) {
    suite[`cScale${i}`] = fonds[i % 3] as string;
    suite[`cScaleInv${i}`] = bords[i % 3] as string;
    suite[`cScalePeer${i}`] = bords[i % 3] as string;
    suite[`cScaleLabel${i}`] = textes[i % 3] as string;
  }
  return suite;
}

function configurer(theme: Record<string, string>): void {
  const sansHtml = { htmlLabels: false };
  mermaid.initialize({
    startOnLoad: false,
    securityLevel: "strict",
    theme: "base",
    deterministicIds: true,
    suppressErrorRendering: true,
    fontFamily: POLICE,
    htmlLabels: false,
    flowchart: sansHtml,
    class: sansHtml,
    state: sansHtml,
    // Sans cela, un planning prend la largeur de la page cachée, et son texte devient illisible.
    gantt: { useWidth: 640, barHeight: 26, barGap: 6, fontSize: 13, sectionFontSize: 13, leftPadding: 110, topPadding: 40 },
    themeVariables: {
      ...theme,
      ...suites(theme),
      pieOpacity: "1",
      pieStrokeColor: theme.background,
      pieOuterStrokeColor: theme.background,
      fontFamily: POLICE,
      darkMode: theme.darkMode === "true",
      xyChart: {
        backgroundColor: "transparent",
        titleColor: theme.textColor,
        xAxisLabelColor: theme.textColor,
        xAxisTitleColor: theme.textColor,
        xAxisTickColor: theme.lineColor,
        xAxisLineColor: theme.lineColor,
        yAxisLabelColor: theme.textColor,
        yAxisTitleColor: theme.textColor,
        yAxisTickColor: theme.lineColor,
        yAxisLineColor: theme.lineColor,
        plotColorPalette: `${theme.primaryBorderColor}, ${theme.secondaryBorderColor}`,
      },
    },
  });
}

/** Un SVG de taille écrite : sans elle, une image SVG n'a pas de dimensions. */
function dimensionne(svg: string): { source: string; largeur: number; hauteur: number } {
  const racine = new DOMParser().parseFromString(svg, "image/svg+xml").documentElement;
  const cadre = (racine.getAttribute("viewBox") ?? "").split(/[\s,]+/).map(Number);
  const largeur = Math.ceil(cadre[2] || Number.parseFloat(racine.getAttribute("width") ?? "") || 640);
  const hauteur = Math.ceil(cadre[3] || Number.parseFloat(racine.getAttribute("height") ?? "") || 400);
  racine.setAttribute("width", String(largeur));
  racine.setAttribute("height", String(hauteur));
  racine.removeAttribute("style");
  return { source: new XMLSerializer().serializeToString(racine), largeur, hauteur };
}

function adresseSvg(source: string): string {
  const octets = new TextEncoder().encode(source);
  let binaire = "";
  for (const octet of octets) binaire += String.fromCharCode(octet);
  return `data:image/svg+xml;base64,${btoa(binaire)}`;
}

/**
 * Le SVG donné, ou celui que Mermaid tire de son code. Une image SVG du coffre n'entre jamais dans la
 * page : elle n'y est qu'analysée, puis montrée comme une image, où ni script ni adresse n'agissent.
 */
async function sourceDe(demande: Demande): Promise<string> {
  if ("svg" in demande) {
    return new TextDecoder().decode(Uint8Array.from(atob(demande.svg), (lettre) => lettre.charCodeAt(0)));
  }
  configurer(demande.theme);
  return (await mermaid.render(`dessin${demande.id}`, demande.code)).svg;
}

async function dessiner(demande: Demande): Promise<void> {
  const { id } = demande;
  try {
    const { source, largeur, hauteur } = dimensionne(await sourceDe(demande));
    const adresse = adresseSvg(source);
    const image = new Image();
    image.src = adresse;
    await image.decode();
    const toile = document.createElement("canvas");
    toile.width = largeur * FINESSE;
    toile.height = hauteur * FINESSE;
    toile.getContext("2d")?.drawImage(image, 0, 0, toile.width, toile.height);
    const png = toile.toDataURL("image/png");
    chrome.webview.postMessage({
      kind: "drawn",
      id,
      image: adresse,
      png: png.slice(png.indexOf(",") + 1),
      width: largeur,
      height: hauteur,
    });
  } catch (erreur) {
    chrome.webview.postMessage({ kind: "failed", id, message: String(erreur) });
  }
}

// Un dessin après l'autre : Mermaid n'en fait qu'un à la fois.
let chaine = Promise.resolve();
chrome.webview.addEventListener("message", (evenement) => {
  chaine = chaine.then(() => dessiner(evenement.data as Demande));
});
chrome.webview.postMessage({ kind: "ready" });
