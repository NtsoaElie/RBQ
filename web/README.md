# RBQPrep — Coach IA (React)

Vite + React + TypeScript. Ask a question about the indexed RBQ documents, read
the answer, expand the sources it was drawn from.

```
npm install
npm run dev      # http://localhost:5173  <- opens the app
npm run build    # compiles to dist/ for deployment, opens nothing
```

`/api` is proxied to the C# query pipeline on `http://localhost:5080`
(see `vite.config.ts`), so start that first.

## Design

Tokens are lifted from rbqprep.com so this feature matches the landing page:

| Token | Value |
| --- | --- |
| primary (amber) | `oklch(65% .14 55)` / `#ce742c` |
| accent (blue) | `oklch(55% .1 236)` |
| background | `oklch(98.5% .002 250)` |
| radius | `.875rem` |
| display font | Space Grotesk |
| body font | DM Sans |
| mono font | JetBrains Mono |

Light-only, matching the landing page. Copy is Quebec French (`lang="fr-CA"`).

| File | Role |
| --- | --- |
| `src/api.ts` | the only call to the backend |
| `src/App.tsx` | header, hero, answer thread, composer |
| `src/styles.css` | design tokens + layout |
| `public/favicon.svg` | the RBQPrep hard-hat mark |
