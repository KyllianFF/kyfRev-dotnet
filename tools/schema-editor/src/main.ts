import { mount } from "svelte";
import VueSchemas from "./composants/topologie/VueSchemas.svelte";
import { api, type DepuisHote, recevoir, signaler } from "./lib/api";
import "./page.css";

const vue = (window as unknown as { chrome: { webview: EventTarget } }).chrome.webview;
vue.addEventListener("message", (evenement) => recevoir((evenement as MessageEvent<DepuisHote>).data));

const ilot = document.getElementById("ilot");
if (ilot) {
  mount(VueSchemas, {
    target: ilot,
    props: {
      onouvrirfiche: (id: string) => {
        void api.ouvrirFiche(id);
      },
    },
  });
}
signaler("ready");
