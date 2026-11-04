// Illustrations des équipements (D43) : des volumes simples, à la manière des
// pictogrammes réseau usuels (routeur en cylindre à quatre flèches, switch en
// bloc à flèches croisées, pare-feu en mur de briques, serveur en tour…),
// dessinés pour kyfRev. Une face avant à la couleur de la famille, un dessus
// plus clair, un côté plus sombre ; les signes en blanc.
//
// Chaque illustration tient dans un carré de 64 × 64, sans transformation
// (les points sont calculés ici) : elle se dessine telle quelle dans le
// canevas, les aperçus et les exports.

import { icone } from "./pictogrammes";

type P = [number, number];

const f = (n: number) => Number(n.toFixed(2));
const points = (ps: P[]) => ps.map(([x, y]) => `${f(x)},${f(y)}`).join(" ");
const BLANC = "#ffffff";

/** Couleur éclaircie (t > 0, vers le blanc) ou assombrie (t < 0, vers le noir). */
export function teinte(hex: string, t: number): string {
  const n = Number.parseInt(hex.slice(1), 16);
  const canal = (c: number) => Math.round(t >= 0 ? c + (255 - c) * t : c * (1 + t));
  const [r, g, b] = [(n >> 16) & 255, (n >> 8) & 255, n & 255].map(canal);
  return `#${(((r ?? 0) << 16) | ((g ?? 0) << 8) | (b ?? 0)).toString(16).padStart(6, "0")}`;
}

const polygone = (ps: P[], remplissage: string, extra = "") =>
  `<polygon points="${points(ps)}" fill="${remplissage}"${extra}/>`;

/** Contour discret des volumes. */
const bord = (c: string) => ` stroke="${teinte(c, -0.45)}" stroke-width="0.8" stroke-linejoin="round"`;

/** Flèche pleine de `de` à `vers` (coordonnées locales), projetée par `vers2d`. */
function fleche(de: P, vers: P, vers2d: (p: P) => P, epaisseur = 0.07, tete = 0.2, largeur = 0.13): string {
  const [dx, dy] = [vers[0] - de[0], vers[1] - de[1]];
  const l = Math.hypot(dx, dy) || 1;
  const [ux, uy] = [dx / l, dy / l];
  const [nx, ny] = [-uy, ux];
  const base: P = [vers[0] - ux * tete, vers[1] - uy * tete];
  const e = epaisseur / 2;
  const contour: P[] = [
    [de[0] + nx * e, de[1] + ny * e],
    [base[0] + nx * e, base[1] + ny * e],
    [base[0] + nx * largeur, base[1] + ny * largeur],
    vers,
    [base[0] - nx * largeur, base[1] - ny * largeur],
    [base[0] - nx * e, base[1] - ny * e],
    [de[0] - nx * e, de[1] - ny * e],
  ];
  return polygone(contour.map(vers2d), BLANC);
}

type Boite = {
  svg: string;
  /** Point du dessus : u de gauche à droite, v de l'avant vers l'arrière (0 à 1). */
  dessus: (p: P) => P;
  /** Point de la face avant : u de gauche à droite, v de haut en bas (0 à 1). */
  devant: (p: P) => P;
};

/** Bloc en perspective légère : face avant, dessus, côté droit. */
function boite(x: number, y: number, l: number, h: number, profondeur: number, c: string): Boite {
  const p = profondeur;
  const q = profondeur * 0.7;
  const svg = [
    polygone(
      [
        [x, y],
        [x + p, y - q],
        [x + l + p, y - q],
        [x + l, y],
      ],
      teinte(c, 0.28),
      bord(c),
    ),
    polygone(
      [
        [x + l, y],
        [x + l + p, y - q],
        [x + l + p, y + h - q],
        [x + l, y + h],
      ],
      teinte(c, -0.28),
      bord(c),
    ),
    polygone(
      [
        [x, y],
        [x + l, y],
        [x + l, y + h],
        [x, y + h],
      ],
      c,
      bord(c),
    ),
  ].join("");
  return {
    svg,
    dessus: ([u, v]) => [x + u * l + v * p, y - v * q],
    devant: ([u, v]) => [x + u * l, y + v * h],
  };
}

/** Cylindre debout : corps et dessus elliptique. */
function cylindre(cx: number, haut: number, rx: number, ry: number, h: number, c: string) {
  const corps = `<path d="M${cx - rx} ${haut}V${haut + h}A${rx} ${ry} 0 0 0 ${cx + rx} ${haut + h}V${haut}Z" fill="${c}"${bord(c)}/>`;
  const reflet = `<path d="M${cx - rx} ${haut}V${haut + h}A${rx} ${ry} 0 0 0 ${cx - rx * 0.35} ${haut + h + ry * 0.94}V${haut + ry * 0.94}Z" fill="${BLANC}" fill-opacity="0.12"/>`;
  const dessus = `<ellipse cx="${cx}" cy="${haut}" rx="${rx}" ry="${ry}" fill="${teinte(c, 0.3)}"${bord(c)}/>`;
  return {
    svg: corps + reflet + dessus,
    /** Point du dessus, u et v de -1 à 1. */
    plan: ([u, v]: P): P => [cx + u * rx, haut + v * ry],
  };
}

/** Pictogramme Lucide (24 × 24) en blanc, posé dans un carré de côté `cote` centré en (cx, cy). */
const glyphe = (nom: string, cx: number, cy: number, cote: number, couleur = BLANC) =>
  `<g transform="translate(${f(cx - cote / 2)} ${f(cy - cote / 2)}) scale(${f(cote / 24)})" fill="none" stroke="${couleur}" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">${icone(nom)}</g>`;

const rect = (x: number, y: number, l: number, h: number, remplissage: string, extra = "") =>
  `<rect x="${f(x)}" y="${f(y)}" width="${f(l)}" height="${f(h)}" fill="${remplissage}"${extra}/>`;

// --- Réseau -------------------------------------------------------------------

function routeur(c: string, signe?: string): string {
  const cyl = cylindre(32, 25, 24, 9, 17, c);
  const flechesDessus = signe
    ? ""
    : [
        fleche([0.16, -0.16], [0.74, -0.74], cyl.plan, 0.13, 0.26, 0.2),
        fleche([-0.16, 0.16], [-0.74, 0.74], cyl.plan, 0.13, 0.26, 0.2),
        fleche([-0.8, -0.8], [-0.2, -0.2], cyl.plan, 0.13, 0.26, 0.2),
        fleche([0.8, 0.8], [0.2, 0.2], cyl.plan, 0.13, 0.26, 0.2),
      ].join("");
  return cyl.svg + flechesDessus + (signe ? glyphe(signe, 32, 36, 13) : "");
}

function switchReseau(c: string, niveau3 = false): string {
  const b = boite(5, 34, 42, 15, 17, c);
  const fleches = [
    fleche([0.45, 0.27], [0.05, 0.27], b.dessus, 0.15, 0.1, 0.21),
    fleche([0.55, 0.27], [0.95, 0.27], b.dessus, 0.15, 0.1, 0.21),
    fleche([0.05, 0.73], [0.45, 0.73], b.dessus, 0.15, 0.1, 0.21),
    fleche([0.95, 0.73], [0.55, 0.73], b.dessus, 0.15, 0.1, 0.21),
  ].join("");
  // Ports en façade.
  const ports = Array.from({ length: 8 }, (_, i) => {
    const [x, y] = b.devant([0.08 + i * 0.108, 0.38]);
    return rect(x, y, 3.2, 3.2, teinte(c, -0.45), ' rx="0.5"');
  }).join("");
  const voyants = niveau3
    ? ""
    : rect(b.devant([0.08, 0.72])[0], b.devant([0, 0.72])[1], 3, 1.6, "#7ee08a", ' rx="0.6"');
  return b.svg + fleches + ports + voyants;
}

function parefeu(c: string): string {
  const b = boite(10, 22, 36, 30, 10, c);
  const mortier = teinte(c, -0.42);
  const rangs = 5;
  const lignes: string[] = [];
  for (let r = 1; r < rangs; r++) {
    const [x0, y] = b.devant([0, r / rangs]);
    const [x1] = b.devant([1, r / rangs]);
    lignes.push(`<line x1="${f(x0)}" y1="${f(y)}" x2="${f(x1)}" y2="${f(y)}"/>`);
  }
  for (let r = 0; r < rangs; r++) {
    const decale = r % 2 === 0 ? 0 : 0.5;
    for (const u of [0.25, 0.5, 0.75, 1]) {
      const uu = (u - decale / 2) % 1;
      if (uu <= 0.02 || uu >= 0.98) continue;
      const [x, y0] = b.devant([uu, r / rangs]);
      const [, y1] = b.devant([uu, (r + 1) / rangs]);
      lignes.push(`<line x1="${f(x)}" y1="${f(y0)}" x2="${f(x)}" y2="${f(y1)}"/>`);
    }
  }
  return `${b.svg}<g stroke="${mortier}" stroke-width="1.2">${lignes.join("")}</g>${flamme(47, 58, 27)}`;
}

/**
 * Flamme du pare-feu (D51), devant le coin du mur : trois langues, du rouge
 * orangé au jaune. `base` : le bas de la flamme, `h` : sa hauteur.
 */
function flamme(cx: number, base: number, h: number): string {
  // Contour d'une langue de feu, pour une hauteur de 26 (base en 0, 0).
  const contour: [number, number][][] = [
    [
      [-6.5, 0],
      [-9.5, -4.5],
      [-8.5, -9.5],
    ],
    [
      [-7.6, -13.8],
      [-3.6, -15.6],
      [-3.8, -21],
    ],
    [
      [0.2, -18.6],
      [1.8, -15.2],
      [0.8, -11.6],
    ],
    [
      [2.8, -13],
      [3.8, -15.6],
      [3.4, -18],
    ],
    [
      [7.8, -13.6],
      [9.6, -8.6],
      [8.4, -4.8],
    ],
    [
      [7.4, -1.8],
      [4.2, 0],
      [0, 0],
    ],
  ];
  const langue = (echelle: number, couleur: string) => {
    const k = (h / 26) * echelle;
    const p = ([x, y]: [number, number]) => `${f(cx + x * k)} ${f(base + y * k)}`;
    return `<path d="M${f(cx)} ${f(base)}${contour.map((c) => `C${c.map(p).join(" ")}`).join("")}Z" fill="${couleur}"/>`;
  };
  return (
    langue(1, "#e8590c").replace("/>", ` stroke="#9c2f06" stroke-width="0.8" stroke-linejoin="round"/>`) +
    langue(0.72, "#fd9a1f") +
    langue(0.42, "#ffd43b")
  );
}

function boitier(c: string, antennes: number): string {
  const b = boite(10, 34, 40, 11, 10, c);
  const voyants = [0, 1, 2, 3]
    .map((i) => {
      const [x, y] = b.devant([0.12 + i * 0.12, 0.5]);
      return `<circle cx="${f(x)}" cy="${f(y)}" r="1.3" fill="${i === 0 ? "#7ee08a" : BLANC}"/>`;
    })
    .join("");
  const tiges = Array.from({ length: antennes }, (_, i) => {
    const [x, y] = b.dessus([0.75 - i * 0.5, 0.6]);
    return `<line x1="${f(x)}" y1="${f(y)}" x2="${f(x + 2)}" y2="${f(y - 18)}" stroke="${teinte(c, -0.35)}" stroke-width="2.2" stroke-linecap="round"/>`;
  }).join("");
  return tiges + b.svg + voyants;
}

function pointAcces(c: string): string {
  const coque = teinte(c, 0.82);
  const ondes = [8, 13, 18]
    .map(
      (r) =>
        `<path d="M${32 - r * 0.8} ${24 - r * 0.6}A${r} ${r} 0 0 1 ${32 + r * 0.8} ${24 - r * 0.6}" fill="none" stroke="${c}" stroke-width="2.4" stroke-linecap="round"/>`,
    )
    .join("");
  return [
    ondes,
    `<path d="M10 40A22 7 0 0 0 54 40V44A22 7 0 0 1 10 44Z" fill="${teinte(c, 0.55)}"${bord(c)}/>`,
    `<path d="M10 40C10 31 20 27 32 27S54 31 54 40A22 7 0 0 1 10 40Z" fill="${coque}"${bord(c)}/>`,
    `<ellipse cx="32" cy="35" rx="3" ry="1.4" fill="${c}"/>`,
  ].join("");
}

function repartiteur(c: string): string {
  const b = boite(5, 34, 42, 15, 17, c);
  return (
    b.svg +
    fleche([0.05, 0.5], [0.42, 0.5], b.dessus, 0.15, 0, 0.075) +
    fleche([0.4, 0.5], [0.95, 0.18], b.dessus, 0.12, 0.1, 0.2) +
    fleche([0.4, 0.5], [0.95, 0.82], b.dessus, 0.12, 0.1, 0.2)
  );
}

/** Bloc plat portant un signe en façade (contrôleurs, appliances). */
function appliance(c: string, signe: string): string {
  const b = boite(8, 28, 42, 20, 10, c);
  const [cx, cy] = b.devant([0.5, 0.5]);
  return b.svg + glyphe(signe, cx, cy, 14);
}

// --- Serveurs et virtualisation ------------------------------------------------

function tour(c: string, x = 18, y = 14, l = 24, h = 42, signe?: string, virtuelle = false): string {
  const b = boite(x, y, l, h, 9, virtuelle ? teinte(c, 0.45) : c);
  const sombre = teinte(c, -0.4);
  const baies = [0.1, 0.22, 0.34]
    .map((v) => {
      const [bx, by] = b.devant([0.14, v]);
      return rect(bx, by, l * 0.72, h * 0.07, sombre, ' rx="0.8"');
    })
    .join("");
  const [lx, ly] = b.devant([0.82, 0.88]);
  const voyant = `<circle cx="${f(lx)}" cy="${f(ly)}" r="1.4" fill="#7ee08a"/>`;
  const [gx, gy] = b.devant([0.5, 0.64]);
  const contourVirtuel = virtuelle
    ? `<rect x="${x - 3}" y="${y - 9}" width="${l + 15}" height="${h + 12}" rx="4" fill="none" stroke="${c}" stroke-width="1.4" stroke-dasharray="4 3"/>`
    : "";
  return (
    contourVirtuel + b.svg + baies + voyant + (signe ? glyphe(signe, gx, gy, Math.min(l * 0.6, 14)) : "")
  );
}

function hyperviseur(c: string): string {
  const b = boite(16, 14, 28, 42, 9, c);
  const vm = teinte(c, 0.55);
  const fenetres = [
    [0.12, 0.12],
    [0.54, 0.12],
    [0.12, 0.42],
    [0.54, 0.42],
  ]
    .map(([u, v]) => {
      const [x, y] = b.devant([u ?? 0, v ?? 0]);
      return rect(x, y, 28 * 0.34, 42 * 0.24, vm, ` rx="1.2" stroke="${BLANC}" stroke-width="0.8"`);
    })
    .join("");
  const [lx, ly] = b.devant([0.82, 0.88]);
  return `${b.svg}${fenetres}<circle cx="${f(lx)}" cy="${f(ly)}" r="1.4" fill="#7ee08a"/>`;
}

function grappe(c: string): string {
  return tour(teinte(c, -0.12), 8, 10, 20, 36) + tour(c, 30, 20, 20, 36);
}

function conteneurDocker(c: string): string {
  const b = boite(6, 28, 46, 20, 10, c);
  const nervures = Array.from({ length: 9 }, (_, i) => {
    const [x, y0] = b.devant([0.08 + i * 0.105, 0.1]);
    const [, y1] = b.devant([0, 0.9]);
    return `<line x1="${f(x)}" y1="${f(y0)}" x2="${f(x)}" y2="${f(y1)}"/>`;
  }).join("");
  return `${b.svg}<g stroke="${teinte(c, -0.35)}" stroke-width="1.6">${nervures}</g>`;
}

function cube(c: string, signe?: string): string {
  const b = boite(16, 26, 28, 26, 12, c);
  const [cx, cy] = b.devant([0.5, 0.5]);
  return b.svg + (signe ? glyphe(signe, cx, cy, 14) : "");
}

function heptagone(c: string, roue: boolean): string {
  const sommets: P[] = Array.from({ length: 7 }, (_, i) => {
    const a = -Math.PI / 2 + (i * 2 * Math.PI) / 7;
    return [32 + 24 * Math.cos(a), 33 + 24 * Math.sin(a)];
  });
  const fond = polygone(sommets, c, ` stroke="${teinte(c, -0.4)}" stroke-width="1" stroke-linejoin="round"`);
  if (!roue) return fond + glyphe("box", 32, 33, 22);
  const rayons = Array.from({ length: 7 }, (_, i) => {
    const a = -Math.PI / 2 + (i * 2 * Math.PI) / 7;
    return `<line x1="${f(32 + 4 * Math.cos(a))}" y1="${f(33 + 4 * Math.sin(a))}" x2="${f(32 + 15 * Math.cos(a))}" y2="${f(33 + 15 * Math.sin(a))}"/>`;
  }).join("");
  return `${fond}<g fill="none" stroke="${BLANC}" stroke-width="2.4" stroke-linecap="round"><circle cx="32" cy="33" r="10"/><circle cx="32" cy="33" r="3.2"/>${rayons}</g>`;
}

// --- Stockage -------------------------------------------------------------------

function nas(c: string): string {
  const b = boite(14, 20, 32, 34, 10, c);
  const baies = [0, 1, 2, 3]
    .map((i) => {
      const [x, y] = b.devant([0.08 + i * 0.23, 0.1]);
      return `${rect(x, y, 32 * 0.17, 34 * 0.62, teinte(c, -0.38), ' rx="1"')}<circle cx="${f(x + 32 * 0.085)}" cy="${f(y + 34 * 0.55)}" r="1" fill="#7ee08a"/>`;
    })
    .join("");
  return b.svg + baies;
}

function disques(c: string): string {
  return [42, 30, 18].map((haut) => cylindre(32, haut, 20, 6.5, 8, c).svg).join("");
}

function bandes(c: string): string {
  const b = boite(6, 22, 46, 30, 10, c);
  const fentes = [0, 1, 2, 3, 4]
    .map((i) => {
      const [x, y] = b.devant([0.07 + i * 0.18, 0.2]);
      return rect(x, y, 46 * 0.13, 30 * 0.6, teinte(c, -0.4), ' rx="1"');
    })
    .join("");
  return b.svg + fentes;
}

// --- Postes et périphériques ------------------------------------------------------

function ecran(c: string, x: number, y: number, l: number, h: number): string {
  const cadre = rect(x, y, l, h, c, ` rx="3"${bord(c)}`);
  const dalle = `<rect x="${x + 2.5}" y="${y + 2.5}" width="${l - 5}" height="${h - 5}" rx="1.5" fill="url(#dalle)"/>`;
  return cadre + dalle;
}

function pc(c: string): string {
  return [
    `<path d="M27 46h10l2 7H25Z" fill="${teinte(c, -0.2)}"${bord(c)}/>`,
    `<ellipse cx="32" cy="54" rx="13" ry="2.6" fill="${teinte(c, -0.1)}"${bord(c)}/>`,
    ecran(c, 6, 10, 52, 37),
  ].join("");
}

function portable(c: string): string {
  return [
    ecran(c, 13, 12, 38, 27),
    `<path d="M13 39H51L58 50H6Z" fill="${teinte(c, 0.2)}"${bord(c)}/>`,
    `<path d="M15 41H49L53 47H11Z" fill="${teinte(c, -0.15)}"/>`,
  ].join("");
}

function clientLeger(c: string): string {
  return [
    `<path d="M24 44h8l1.6 6H22.4Z" fill="${teinte(c, -0.2)}"${bord(c)}/>`,
    ecran(c, 6, 14, 40, 31),
    rect(49, 20, 9, 32, teinte(c, -0.1), ` rx="2"${bord(c)}`),
    `<circle cx="53.5" cy="25" r="1.3" fill="#7ee08a"/>`,
  ].join("");
}

function telephone(c: string): string {
  const touches = [0, 1, 2]
    .flatMap((ligne) =>
      [0, 1, 2].map(
        (col) => `<circle cx="${f(37 + col * 5)}" cy="${f(37 + ligne * 4.4)}" r="1.2" fill="${BLANC}"/>`,
      ),
    )
    .join("");
  return [
    `<path d="M8 50L14 26H56L58 50Z" fill="${c}"${bord(c)}/>`,
    rect(33, 28, 20, 6, "url(#dalle)", ' rx="1"'),
    `<path d="M10 25C10 20 13 18 17 18H24C27 18 29 20 29 24V44C29 48 27 50 24 50H17C13 50 10 48 10 44Z" fill="${teinte(c, -0.25)}"${bord(c)}/>`,
    touches,
  ].join("");
}

function mobile(c: string): string {
  return [
    rect(19, 6, 26, 52, c, ` rx="5"${bord(c)}`),
    rect(22, 11, 20, 40, "url(#dalle)", ' rx="1.5"'),
    `<circle cx="32" cy="54.5" r="1.6" fill="${teinte(c, 0.4)}"/>`,
  ].join("");
}

function imprimante(c: string): string {
  const b = boite(8, 28, 42, 20, 10, c);
  const [fx, fy] = b.dessus([0.22, 0.75]);
  const feuille = `<path d="M${f(fx)} ${f(fy)}l3 -12h22l-3 12Z" fill="${BLANC}"${bord("#9aa3ad")}/>`;
  const [sx, sy] = b.devant([0.15, 0.62]);
  return feuille + b.svg + rect(sx, sy, 42 * 0.7, 3, teinte(c, -0.45), ' rx="1"');
}

function camera(c: string): string {
  return [
    `<path d="M36 40v10h-8" fill="none" stroke="${teinte(c, -0.3)}" stroke-width="3" stroke-linecap="round"/>`,
    rect(22, 51, 14, 4, teinte(c, -0.3), ' rx="1.5"'),
    `<path d="M8 22H46L54 30V38H8Z" fill="${c}"${bord(c)}/>`,
    `<path d="M8 22H46L54 30H8Z" fill="${teinte(c, 0.28)}"${bord(c)}/>`,
    `<circle cx="50" cy="34" r="4.4" fill="#1d2430"/><circle cx="51" cy="33" r="1.4" fill="#5fa8e8"/>`,
  ].join("");
}

function capteur(c: string): string {
  return [
    `<circle cx="32" cy="36" r="17" fill="${c}"${bord(c)}/>`,
    `<circle cx="32" cy="36" r="11" fill="${teinte(c, 0.3)}"/>`,
    `<circle cx="32" cy="36" r="4" fill="${BLANC}"/>`,
    `<path d="M24 13A12 12 0 0 1 40 13M27 17A7 7 0 0 1 37 17" fill="none" stroke="${c}" stroke-width="2.4" stroke-linecap="round"/>`,
  ].join("");
}

// --- Infrastructure -------------------------------------------------------------

function baie(c: string): string {
  const b = boite(18, 10, 26, 48, 9, c);
  const unites = Array.from({ length: 6 }, (_, i) => {
    const [x, y] = b.devant([0.1, 0.06 + i * 0.15]);
    return `${rect(x, y, 26 * 0.8, 48 * 0.1, teinte(c, -0.38), ' rx="0.8"')}<circle cx="${f(x + 26 * 0.7)}" cy="${f(y + 2.4)}" r="0.9" fill="#7ee08a"/>`;
  }).join("");
  return b.svg + unites;
}

function panneau(c: string): string {
  const b = boite(4, 30, 50, 12, 8, c);
  const trous = [0, 1]
    .flatMap((ligne) =>
      Array.from({ length: 10 }, (_, i) => {
        const [x, y] = b.devant([0.06 + i * 0.092, 0.18 + ligne * 0.42]);
        return rect(x, y, 3, 3, "#1d2430", ' rx="0.4"');
      }),
    )
    .join("");
  return b.svg + trous;
}

function prise(c: string): string {
  return [
    rect(14, 12, 36, 40, teinte(c, 0.85), ` rx="5"${bord(c)}`),
    `<path d="M24 26h16v12h-4v3h-8v-3h-4Z" fill="#1d2430"/>`,
    `<circle cx="32" cy="17" r="1.4" fill="${teinte(c, 0.2)}"/><circle cx="32" cy="47" r="1.4" fill="${teinte(c, 0.2)}"/>`,
  ].join("");
}

function onduleur(c: string): string {
  const b = boite(18, 14, 26, 42, 9, c);
  const [ex, ey] = b.devant([0.15, 0.08]);
  return [
    b.svg,
    rect(ex, ey, 26 * 0.7, 6, "url(#dalle)", ' rx="1"'),
    `<path d="M33 27l-7 12h6l-3 11 8-14h-6l3-9Z" fill="${BLANC}"/>`,
  ].join("");
}

function pdu(c: string): string {
  const b = boite(4, 30, 50, 11, 8, c);
  const prises = Array.from({ length: 6 }, (_, i) => {
    const [x, y] = b.devant([0.1 + i * 0.15, 0.5]);
    return `<circle cx="${f(x + 2)}" cy="${f(y)}" r="2.6" fill="#1d2430"/>`;
  }).join("");
  return b.svg + prises;
}

// --- Réseaux extérieurs -----------------------------------------------------------

function globe(c: string): string {
  return [
    `<circle cx="32" cy="32" r="24" fill="${c}"${bord(c)}/>`,
    `<g fill="none" stroke="${BLANC}" stroke-opacity="0.8" stroke-width="1.6">`,
    `<ellipse cx="32" cy="32" rx="10" ry="24"/><path d="M8 32h48M12 20h40M12 44h40M32 8v48"/></g>`,
    `<path d="M14 18A24 24 0 0 1 36 8.4A26 26 0 0 0 14 18Z" fill="${BLANC}" fill-opacity="0.25"/>`,
  ].join("");
}

const NUAGE =
  "M18 48C10 48 6 43 6 37.5S10 28 16.5 28C17 19 24 13 32.5 13C40 13 46 18 47.5 25C54 25.5 58 30.5 58 36.5C58 43 53.5 48 47 48Z";

function nuage(c: string, signe?: string): string {
  return [
    `<path d="${NUAGE}" fill="${c}"${bord(c)}/>`,
    `<path d="M16.5 28C17 19 24 13 32.5 13C40 13 46 18 47.5 25C40 21 25 21 16.5 28Z" fill="${BLANC}" fill-opacity="0.2"/>`,
    signe ? glyphe(signe, 32, 36, 14) : "",
  ].join("");
}

function immeuble(c: string): string {
  const b = boite(16, 14, 28, 44, 10, c);
  const fenetres = [0, 1, 2, 3, 4]
    .flatMap((ligne) =>
      [0, 1, 2].map((col) => {
        const [x, y] = b.devant([0.12 + col * 0.29, 0.07 + ligne * 0.18]);
        return rect(x, y, 28 * 0.18, 44 * 0.1, teinte(c, 0.6), ' rx="0.6"');
      }),
    )
    .join("");
  return b.svg + fenetres;
}

/** Illustration de chaque pictogramme du catalogue ; à défaut, un bloc portant le pictogramme. */
const ILLUSTRATIONS: Record<string, (c: string) => string> = {
  "kyfrev-routeur": (c) => routeur(c),
  "kyfrev-switch": (c) => switchReseau(c),
  "brick-wall": parefeu,
  router: (c) => boitier(c, 2),
  "kyfrev-modem": (c) => boitier(c, 1),
  waypoints: (c) => routeur(c, "cloud"),
  "lock-keyhole": (c) => routeur(c, "lock-keyhole"),
  "kyfrev-point-acces": pointAcces,
  "wifi-cog": (c) => appliance(c, "wifi"),
  split: repartiteur,
  network: (c) => appliance(c, "network"),
  server: (c) => tour(c),
  "server-cog": hyperviseur,
  boxes: grappe,
  "kyfrev-vm": (c) => tour(c, 18, 16, 24, 38, "monitor", true),
  container: conteneurDocker,
  box: (c) => cube(c),
  "ship-wheel": (c) => heptagone(c, true),
  hexagon: (c) => heptagone(c, false),
  "hard-drive": nas,
  database: disques,
  cpu: (c) => appliance(c, "cpu"),
  "archive-restore": (c) => tour(c, 18, 14, 24, 42, "archive-restore"),
  "cassette-tape": bandes,
  monitor: pc,
  laptop: portable,
  "tv-minimal": clientLeger,
  "kyfrev-telephone": telephone,
  "tablet-smartphone": mobile,
  printer: imprimante,
  cctv: camera,
  thermometer: capteur,
  "rows-3": baie,
  "kyfrev-panneau": panneau,
  "ethernet-port": prise,
  "battery-charging": onduleur,
  "plug-zap": pdu,
  globe,
  "radio-tower": (c) => nuage(c, "radio-tower"),
  house: immeuble,
  cloud: (c) => nuage(c),
};

/** L'illustration d'un équipement (carré de 64 × 64), à la couleur de sa famille. */
export function illustration(nom: string, couleur: string): string {
  return (ILLUSTRATIONS[nom] ?? ((c: string) => cube(c, nom)))(couleur);
}

/** Pictogrammes illustrés (pour vérifier que le catalogue est couvert). */
export const ILLUSTRES = new Set(Object.keys(ILLUSTRATIONS));

/** Définitions partagées : la dalle des écrans (dégradé bleu). */
export const DEFINITIONS_ILLUSTRATIONS =
  '<linearGradient id="dalle" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#7cc0f0"/><stop offset="1" stop-color="#2f6fb7"/></linearGradient>';

/**
 * Emprise de chaque illustration dans son carré de 64 (x0, y0, x1, y1),
 * mesurée sur le dessin (boîte englobante, à un demi-point près) : les câbles
 * s'arrêtent à son bord, le nom se place juste dessous (D50).
 */
const EMPRISES: Record<string, [number, number, number, number]> = {
  "archive-restore": [17, 7, 52, 57],
  "battery-charging": [17, 7, 54, 57],
  box: [15, 17, 57, 53],
  boxes: [7, 3, 60, 57],
  "brick-wall": [9, 14, 58, 59],
  "cassette-tape": [5, 14, 63, 53],
  cctv: [7, 21, 55, 56],
  cloud: [5, 12, 59, 49],
  container: [5, 20, 63, 49],
  cpu: [7, 20, 61, 49],
  database: [11, 11, 53, 58],
  "ethernet-port": [13, 11, 51, 53],
  globe: [7, 7, 57, 57],
  "hard-drive": [13, 12, 57, 55],
  hexagon: [8, 8, 56, 56],
  house: [15, 6, 55, 59],
  "kyfrev-modem": [9, 11, 61, 46],
  "kyfrev-panneau": [3, 23, 63, 43],
  "kyfrev-point-acces": [9, 5, 55, 52],
  "kyfrev-routeur": [7, 15, 57, 52],
  "kyfrev-switch": [4, 21, 65, 50],
  "kyfrev-telephone": [7, 17, 59, 51],
  "kyfrev-vm": [14, 6, 55, 58],
  laptop: [5, 11, 59, 51],
  "lock-keyhole": [7, 15, 57, 52],
  monitor: [5, 9, 59, 58],
  network: [7, 20, 61, 49],
  "plug-zap": [3, 23, 63, 42],
  printer: [7, 10, 61, 49],
  "radio-tower": [5, 12, 59, 49],
  router: [9, 11, 61, 46],
  "rows-3": [17, 3, 54, 59],
  server: [17, 7, 52, 57],
  "server-cog": [15, 7, 54, 57],
  "ship-wheel": [8, 8, 56, 56],
  split: [4, 21, 65, 50],
  "tablet-smartphone": [18, 5, 46, 59],
  thermometer: [14, 9, 50, 54],
  "tv-minimal": [5, 13, 59, 53],
  waypoints: [7, 15, 57, 52],
  "wifi-cog": [7, 20, 61, 49],
};

export const emprise = (nom: string): [number, number, number, number] => EMPRISES[nom] ?? [15, 17, 57, 53];
