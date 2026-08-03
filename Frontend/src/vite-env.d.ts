/// <reference types="vite/client" />

/**
 * Settings that come from the .env files.
 * Declaring them here means a typo in `import.meta.env.VITE_...` is a build error.
 */
interface ImportMetaEnv {
  readonly VITE_API_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
