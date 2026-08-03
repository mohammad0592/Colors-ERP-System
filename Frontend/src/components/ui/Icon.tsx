import type { ReactElement } from 'react';

/**
 * Line icons, drawn inline.
 *
 * No icon library: a factory ERP needs about twenty icons, and an inline set has no
 * download, works with no internet, and cannot break when a package updates.
 * Each is a 24×24 stroke path that takes its colour from the surrounding text.
 */

const paths: Record<string, string> = {
  dashboard: 'M3 3h8v8H3zM13 3h8v5h-8zM13 10h8v11h-8zM3 13h8v8H3z',
  inventory: 'M3 7l9-4 9 4v10l-9 4-9-4zM3 7l9 4 9-4M12 11v10',
  receive: 'M12 3v12m0 0l-4-4m4 4l4-4M4 17v2a2 2 0 002 2h12a2 2 0 002-2v-2',
  issue: 'M12 21V9m0 0l-4 4m4-4l4 4M4 7V5a2 2 0 012-2h12a2 2 0 012 2v2',
  roll: 'M4 7c0-2 3.6-3 8-3s8 1 8 3-3.6 3-8 3-8-1-8-3zM4 7v10c0 2 3.6 3 8 3s8-1 8-3V7',
  test: 'M9 3h6M10 3v6l-5 9a2 2 0 002 3h10a2 2 0 002-3l-5-9V3',
  thermo: 'M6 4h12v6a6 6 0 01-6 6 6 6 0 01-6-6zM4 20h16M8 16v4M16 16v4',
  pallet: 'M3 15h18M3 19h18M6 15V9h5v6M13 15V9h5v6M8 9V5h8v4',
  packaging: 'M3 8l9-5 9 5v8l-9 5-9-5zM3 8l9 5 9-5M12 13v9M7.5 5.5l9 5',
  recycler:
    'M7 19H4.5a2 2 0 01-1.7-3l2.4-4M17 19l2.2-3.8a2 2 0 00-.1-2.2L16.5 9M9 5l2.3-3.9a2 2 0 013.4 0L17 5M7 19l2 3M7 19l3-1M17 19l-3 1M17 19l-1 3',
  reports: 'M4 20V10M10 20V4M16 20v-8M22 20H2',
  recipe: 'M5 3h11l4 4v14H5zM16 3v4h4M9 12h7M9 16h7M9 8h3',
  shift: 'M12 21a9 9 0 100-18 9 9 0 000 18zM12 7v5l3 2',
  settings:
    'M12 15a3 3 0 100-6 3 3 0 000 6zM19.4 15a1.6 1.6 0 00.3 1.8l.1.1a2 2 0 01-2.8 2.8l-.1-.1a1.6 1.6 0 00-2.7 1.1v.3a2 2 0 01-4 0V21a1.6 1.6 0 00-2.7-1.1l-.1.1a2 2 0 01-2.8-2.8l.1-.1A1.6 1.6 0 003 15a2 2 0 010-4 1.6 1.6 0 001.1-2.7l-.1-.1a2 2 0 012.8-2.8l.1.1A1.6 1.6 0 009 4.6V4a2 2 0 014 0v.1A1.6 1.6 0 0016.6 5l.1-.1a2 2 0 012.8 2.8l-.1.1A1.6 1.6 0 0021 11a2 2 0 010 4z',
  users:
    'M16 20v-2a4 4 0 00-4-4H6a4 4 0 00-4 4v2M9 10a4 4 0 100-8 4 4 0 000 8zM22 20v-2a4 4 0 00-3-3.9M16 2.1a4 4 0 010 7.8',
  search: 'M11 19a8 8 0 100-16 8 8 0 000 16zM21 21l-4.3-4.3',
  bell: 'M18 8a6 6 0 10-12 0c0 7-3 9-3 9h18s-3-2-3-9M13.7 21a2 2 0 01-3.4 0',
  logout: 'M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9',
  chevronLeft: 'M15 18l-6-6 6-6',
  chevronRight: 'M9 18l6-6-6-6',
  menu: 'M3 6h18M3 12h18M3 18h18',
  close: 'M18 6L6 18M6 6l12 12',
};

interface IconProps {
  name: string;
  className?: string;
}

export function Icon({ name, className = 'size-5' }: IconProps): ReactElement | null {
  const path = paths[name];

  if (path === undefined) {
    return null;
  }

  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d={path} />
    </svg>
  );
}
