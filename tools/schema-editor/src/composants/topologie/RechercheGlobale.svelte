<script lang="ts">
  // La recherche dans tous les schémas (31E) : sa place, sous la barre de recherche de l'accueil ; son contenu vient à part.
  const props: { texte: string; onOuvrir: (schemaId: string, pageId?: string, objetId?: string) => void } = $props();
</script>

<section class="recherche-globale" aria-label={`Dans tous les schémas : « ${props.texte} »`} hidden></section>
