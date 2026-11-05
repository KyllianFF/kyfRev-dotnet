// Page cachée des extensions. Chaque extension active tourne dans son propre cadre « sandbox », d'origine opaque. Cette page les
// relie à l'application, et rien d'autre : un message n'est attribué à une extension que d'après le cadre qui l'a envoyé et le jeton
// donné à ce cadre seul ; et elle ne transmet que les champs attendus, jamais un message tel quel.
"use strict";

(() => {
  const cadres = new Map();
  const versHote = (message) => window.chrome.webview.postMessage(message);
  const texteOuRien = (valeur) => (typeof valeur === "string" ? valeur : null);

  function creer(id, url) {
    const cadre = document.createElement("iframe");
    cadre.setAttribute("sandbox", "allow-scripts");
    cadre.tabIndex = -1;
    cadre.src = `/cadre/${encodeURIComponent(id)}`;
    cadres.set(id, { cadre, url, jeton: crypto.randomUUID(), lance: false });
    document.body.append(cadre);
  }

  function oublier(id) {
    cadres.get(id)?.cadre.remove();
    cadres.delete(id);
  }

  function versCadre(id, message) {
    const entree = cadres.get(id);
    entree?.cadre.contentWindow?.postMessage({ ...message, jeton: entree.jeton }, "*");
  }

  function depuisCadre(id, entree, message) {
    if (message.type === "pret") {
      // Le premier message du cadre, avant qu'il n'ait son jeton. Il ne sert qu'une fois.
      if (!entree.lance) {
        entree.lance = true;
        versCadre(id, { type: "charger", url: entree.url });
      }
      return;
    }
    if (message.jeton !== entree.jeton) return;
    switch (message.type) {
      case "charge":
        versHote({ kind: "charge", id });
        break;
      case "erreur":
        versHote({ kind: "erreur", id, erreur: texteOuRien(message.erreur) });
        break;
      case "resultat":
        if (Number.isInteger(message.n)) {
          versHote({ kind: "resultat", id, n: message.n, html: texteOuRien(message.html), ok: message.ok === true, erreur: texteOuRien(message.erreur) });
        }
        break;
      case "requete":
        if (Number.isInteger(message.n) && typeof message.methode === "string") {
          versHote({ kind: "requete", id, n: message.n, methode: message.methode, args: Array.isArray(message.args) ? message.args : [] });
        }
        break;
      case "rafraichir":
        versHote({ kind: "rafraichir", id });
        break;
      default:
        break;
    }
  }

  window.addEventListener("message", (evenement) => {
    for (const [id, entree] of cadres) {
      if (entree.cadre.contentWindow === evenement.source) {
        depuisCadre(id, entree, evenement.data ?? {});
        return;
      }
    }
  });

  window.chrome.webview.addEventListener("message", ({ data }) => {
    switch (data.kind) {
      case "charger":
        creer(data.id, data.url);
        break;
      case "oublier":
        oublier(data.id);
        break;
      case "vers-cadre":
        versCadre(data.id, data.message);
        break;
      default:
        break;
    }
  });

  versHote({ kind: "pret" });
})();
