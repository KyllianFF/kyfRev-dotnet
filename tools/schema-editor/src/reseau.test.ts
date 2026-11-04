// @vitest-environment node
import { describe, expect, it, vi } from "vitest";

vi.stubGlobal("window", {});
const { dessinerReseau } = await import("./reseau");

const SCHEMA = JSON.stringify({ format: 2, id: "s_x", titre: "Réseau", equipements: [], liens: [] });

describe("le dessin pour les fiches", () => {
  it("rend un SVG, dessiné aux couleurs données, et le défaut pour les autres", () => {
    const svg = dessinerReseau(SCHEMA, { "--texte": "#123456" });
    expect(svg).toMatch(/^<svg/);
    expect(svg).not.toContain("var(--");
  });

  it("refuse ce qui n'est pas un schéma", () => {
    expect(() => dessinerReseau("{ cassé", {})).toThrow();
  });
});
