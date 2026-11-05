// Généré depuis VisualMessages.cs : ne pas modifier à la main.

export type VersEditeur =
  | { kind: "load"; text: string; folder: string }
  | { kind: "theme"; variables: Record<string, string> }
  | { kind: "sync"; id: number }
  | { kind: "mark"; mark: "bold" | "italic" | "underline" | "strike" | "superscript" | "subscript" | "formula" }
  | { kind: "heading"; level: number }
  | { kind: "list"; list: "bullets" | "numbers" | "tasks" }
  | { kind: "case"; style: "sentence" | "lower" | "upper" | "words" | "invert" | "alternate" | null }
  | { kind: "table"; rows: number; columns: number }
  | { kind: "tableAction"; action: string }
  | { kind: "code"; language: string }
  | { kind: "rule" }
  | { kind: "askFormula" }
  | { kind: "formula"; tex: string; block: boolean; position: number | null }
  | { kind: "askLink" }
  | { kind: "link"; label: string; address: string }
  | { kind: "unlink" }
  | { kind: "clearFormat" }
  | { kind: "color"; color: string | null }
  | { kind: "size"; size: string | null }
  | { kind: "align"; align: "left" | "center" | "right" | "justify" }
  | { kind: "image"; link: string; alternative: string; position: number | null }
  | { kind: "sheetLink"; label: string; address: string }
  | { kind: "replaceWord"; start: number; end: number; text: string }
  | { kind: "undo" }
  | { kind: "redo" }
  | { kind: "focus" }
  | { kind: "goToHeading"; title: string; occurrence: number }
  | { kind: "clipboard"; action: string }
  | { kind: "listOpen"; open: boolean }
  | { kind: "insertDiagram"; language: string; code: string; position: number | null }
  | { kind: "diagram"; language: string; code: string; svg: string | null; image: string | null; editable: boolean }
  | { kind: "deleteBlock"; position: number }
  | { kind: "showBlockCode"; position: number };

export type DepuisEditeur =
  | { kind: "ready" }
  | { kind: "edited" }
  | { kind: "text"; id: number | null; markdown: string }
  | { kind: "save" }
  | { kind: "tools"; bold: boolean; italic: boolean; underline: boolean; strike: boolean; superscript: boolean; subscript: boolean; link: boolean; heading: number; inCode: boolean; list: "bullets" | "numbers" | "tasks" | null; table: boolean; color: string | null; size: string | null; align: "left" | "center" | "right" | "justify"; canUndo: boolean; canRedo: boolean }
  | { kind: "askLink"; label: string; address: string }
  | { kind: "askFormula"; position: number | null; tex: string; block: boolean }
  | { kind: "linkInput"; query: string | null; start: number; x: number; y: number }
  | { kind: "listKey"; key: string }
  | { kind: "menu"; word: string | null; wordStart: number; hasSelection: boolean; inCode: boolean; inTable: boolean; onLink: boolean; drawingPosition: number | null; drawingLanguage: string | null; drawingCode: string | null }
  | { kind: "dropped"; name: string; base64: string | null; position: number | null }
  | { kind: "askDiagram"; language: string; code: string }
  | { kind: "editDiagram"; language: string; code: string; position: number };
