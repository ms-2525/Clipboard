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
    <path d="M2.5 4.5h11M2.5 8h11M2.5 11.5h11" />
    <circle cx="5.5" cy="4.5" r="1.6" fill="currentColor" />
    <circle cx="10.5" cy="8" r="1.6" fill="currentColor" />
    <circle cx="6.5" cy="11.5" r="1.6" fill="currentColor" />
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
