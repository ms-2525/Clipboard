import type { ReactNode } from 'react'

const Svg = ({ children, size = 16 }: { children: ReactNode; size?: number }) => (
  <svg width={size} height={size} viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    {children}
  </svg>
)

export const IconSearch = () => (
  <Svg>
    <circle cx="7" cy="7" r="4.25" />
    <path d="M10.2 10.2 13.5 13.5" />
  </Svg>
)
export const IconClipboard = ({ size = 16 }: { size?: number }) => (
  <Svg size={size}>
    <rect x="3.25" y="2.75" width="9.5" height="11" rx="2" />
    <path d="M6 4.5h4M6 7.5h4M6 10.5h2.5" />
  </Svg>
)
export const IconSun = () => (
  <Svg>
    <circle cx="8" cy="8" r="2.75" />
    <path d="M8 1.5v1.4M8 13.1v1.4M1.5 8h1.4M13.1 8h1.4M3.4 3.4l1 1M11.6 11.6l1 1M12.6 3.4l-1 1M4.4 11.6l-1 1" />
  </Svg>
)
export const IconMoon = () => (
  <Svg>
    <path d="M13 9.4A5.2 5.2 0 0 1 6.6 3a5.2 5.2 0 1 0 6.4 6.4Z" />
  </Svg>
)
export const IconMonitor = () => (
  <Svg>
    <rect x="2" y="3" width="12" height="8.5" rx="1.5" />
    <path d="M6 14h4M8 11.5V14" />
  </Svg>
)
export const IconSettings = () => (
  <Svg>
    {/* A cog, scaled from the 24 px outline so the teeth survive at 16 px; the stroke stays 1.5. */}
    <g transform="scale(0.6667)" strokeWidth="2.25">
      <path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z" />
      <circle cx="12" cy="12" r="3" />
    </g>
  </Svg>
)
export const IconEdit = () => (
  <Svg>
    <path d="M10.4 2.6 13.4 5.6 5.8 13.2l-3.6.6.6-3.6Z" />
    <path d="m9 4 3 3" />
  </Svg>
)
export const IconPin = () => (
  <Svg size={12}>
    <path d="M6 9.5 2.8 12.8M9 2.2l4.8 4.8-1.6.7-1 2.3-3.5-3.5L10 5l-1-2.8Z" />
  </Svg>
)
export const IconFile = () => (
  <Svg size={12}>
    <path d="M4 1.8h4.7L12 5.1v9.1H4Z" />
    <path d="M8.6 1.9v3.3H12" />
  </Svg>
)
export const IconCheck = () => (
  <Svg size={13}>
    <path d="m3 8.4 3.2 3.1L13 4.8" />
  </Svg>
)
export const IconClose = () => (
  <Svg>
    <path d="m4 4 8 8M12 4l-8 8" />
  </Svg>
)
export const IconChevron = () => (
  <Svg size={12}>
    <path d="m4.5 6.5 3.5 3.5 3.5-3.5" />
  </Svg>
)
export const IconSort = () => (
  <Svg size={13}>
    <path d="M4 3v10M4 13l-2-2M4 13l2-2M11 13V3M11 3 9 5M11 3l2 2" />
  </Svg>
)
export const IconBold = () => (
  <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
    <path d="M4.5 3.5h4.2a2.6 2.6 0 0 1 0 5.2H4.5Zm0 5.2h4.9a2.65 2.65 0 0 1 0 5.3H4.5Z" stroke="currentColor" strokeWidth="1.7" strokeLinejoin="round" />
  </svg>
)
export const IconItalic = () => (
  <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
    <path d="M6.5 3.5h6M3.5 12.5h6M9.2 3.5 6.8 12.5" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
  </svg>
)
export const IconUnderline = () => (
  <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
    <path d="M4.5 2.5v5a3.5 3.5 0 0 0 7 0v-5M3 13.5h10" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
  </svg>
)
export const IconStrike = () => (
  <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
    <path d="M5 12.2c.9.6 1.9.9 3 .9 2 0 3.2-1 3.2-2.4 0-1.2-.8-1.8-2.5-2.2M10.9 4.3C10.2 3.7 9.3 3.4 8.3 3.4c-1.8 0-3 .9-3 2.2 0 .6.2 1 .7 1.4M2.5 8h11" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
  </svg>
)
export const IconMinus = () => (
  <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
    <path d="M3.5 8h9" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
  </svg>
)
export const IconPlus = () => (
  <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
    <path d="M8 3.5v9M3.5 8h9" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
  </svg>
)
export const IconHighlight = () => (
  <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
    <path d="m9.4 2.6 3.9 3.9-5.7 5.7-4.4.6.5-4.5Z" stroke="currentColor" strokeWidth="1.4" strokeLinejoin="round" />
    <path d="M2.5 13.6h11" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" />
  </svg>
)
export const IconPalette = () => (
  <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
    <path d="M8 1.8a6.2 6.2 0 0 0 0 12.4c.8 0 1.2-.5 1.2-1.1 0-.6-.4-1-.4-1.6 0-.6.5-1 1.1-1h1.2c1.8 0 3.1-1.3 3.1-3.1A5.9 5.9 0 0 0 8 1.8Z" stroke="currentColor" strokeWidth="1.4" />
    <circle cx="5.2" cy="6" r=".9" fill="currentColor" />
    <circle cx="8" cy="4.6" r=".9" fill="currentColor" />
    <circle cx="10.6" cy="6" r=".9" fill="currentColor" />
  </svg>
)
export const IconCopy = () => (
  <Svg size={13}>
    <rect x="5.5" y="5.5" width="8" height="8" rx="1.5" />
    <path d="M10.5 3.2V2.8a1 1 0 0 0-1-1H3.8a1 1 0 0 0-1 1V9.5a1 1 0 0 0 1 1h.4" />
  </Svg>
)
export const IconWarning = () => (
  <Svg size={14}>
    <path d="M8 2.5 14 13H2Z" />
    <path d="M8 6.4v3.2M8 11.2v.1" />
  </Svg>
)
export const IconLink = () => (
  <Svg>
    <path d="M6.5 9.5 9.5 6.5" />
    <path d="M7 5.5 8.5 4a2.5 2.5 0 0 1 3.5 3.5L10.5 9" />
    <path d="M9 10.5 7.5 12A2.5 2.5 0 0 1 4 8.5L5.5 7" />
  </Svg>
)
export const IconUndo = () => (
  <Svg>
    <path d="M4 6.5h5.5a3.5 3.5 0 0 1 0 7H6" />
    <path d="M6.5 4 4 6.5l2.5 2.5" />
  </Svg>
)
export const IconRedo = () => (
  <Svg>
    <path d="M12 6.5H6.5a3.5 3.5 0 0 0 0 7H10" />
    <path d="M9.5 4 12 6.5l-2.5 2.5" />
  </Svg>
)
export const IconBulletList = () => (
  <Svg>
    <circle cx="3.4" cy="4.5" r="1" fill="currentColor" stroke="none" />
    <circle cx="3.4" cy="8" r="1" fill="currentColor" stroke="none" />
    <circle cx="3.4" cy="11.5" r="1" fill="currentColor" stroke="none" />
    <path d="M6.4 4.5h7M6.4 8h7M6.4 11.5h7" />
  </Svg>
)
export const IconOrderedList = () => (
  <Svg>
    <path d="M6.8 4.5h6.8M6.8 8h6.8M6.8 11.5h6.8" />
    <path d="M4.3 6.2V3.1L3.3 4" />
    <path d="M2.9 9.4a1.4 1.4 0 1 1 2.8 0c0 .5-.5.9-.9 1.2l-1.9 1.6h2.8" />
  </Svg>
)
export const IconInlineCode = () => (
  <Svg>
    <path d="M5.6 4.2 2.4 8l3.2 3.8M10.4 4.2 13.6 8l-3.2 3.8" />
  </Svg>
)
export const IconCodeBlock = () => (
  <Svg>
    <path d="M7.2 3.4c-1.5 0-2 .6-2 1.9v1.1c0 .8-.4 1.3-1.2 1.6.8.3 1.2.8 1.2 1.6v1.1c0 1.3.5 1.9 2 1.9" />
    <path d="M8.8 3.4c1.5 0 2 .6 2 1.9v1.1c0 .8.4 1.3 1.2 1.6-.8.3-1.2.8-1.2 1.6v1.1c0 1.3-.5 1.9-2 1.9" />
  </Svg>
)
export const IconTaskList = () => (
  <Svg>
    <rect x="2.3" y="2.9" width="3.7" height="3.7" rx="1" />
    <path d="m3.2 4.7.9.9 1.5-1.7" />
    <path d="M7.7 4.8h6M7.7 9.2h6M2.7 12.4h11" />
  </Svg>
)
export const IconQuote = () => (
  <Svg>
    <path d="M3.5 3v10" />
    <path d="M6.6 4.4h6M6.6 8h6M6.6 11.6h3.8" />
  </Svg>
)
export const IconTable = () => (
  <Svg>
    <rect x="2.5" y="3" width="11" height="10" rx="1.5" />
    <path d="M2.5 6.5h11M6.4 6.5V13M10 6.5V13" />
  </Svg>
)
export const IconAddRow = () => (
  <Svg>
    <rect x="2.2" y="2.4" width="7.6" height="3.3" rx="1" />
    <rect x="2.2" y="7.2" width="7.6" height="3.3" rx="1" />
    <path d="M12.8 7.2v5.2M10.2 9.8h5.2" />
  </Svg>
)
export const IconTrash = () => (
  <Svg>
    <path d="M2.8 4.2h10.4M6.5 4V2.8h3V4M4.3 4.2l.7 8.6a1 1 0 0 0 1 .9h3.9a1 1 0 0 0 1-.9l.7-8.6" />
    <path d="M6.8 7v4.4M9.2 7v4.4" />
  </Svg>
)
export const IconDownload = () => (
  <Svg>
    <path d="M8 2.5v7.6M5.2 7.3 8 10.1l2.8-2.8M2.8 13.5h10.4" />
  </Svg>
)
export const IconOpen = () => (
  <Svg>
    <path d="M6.5 3.5H3.5v9h9V9.5M9.2 3.5h3.3v3.3M12.3 3.7 7.6 8.4" />
  </Svg>
)
export const IconFolder = () => (
  <Svg>
    <path d="M2.5 4.6a1 1 0 0 1 1-1h2.9L8.6 5H12a1 1 0 0 1 1 1v5a1 1 0 0 1-1 1h-8.5a1 1 0 0 1-1-1Z" />
  </Svg>
)
export const IconChevronRight = () => (
  <Svg size={12}>
    <path d="m6.5 4.5 3.5 3.5-3.5 3.5" />
  </Svg>
)
