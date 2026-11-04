declare module "*.svelte" {
  import type { Component } from "svelte";
  const composant: Component<Record<string, unknown>>;
  export default composant;
}
