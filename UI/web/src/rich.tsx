/**
 * Rich clip bodies for the list rows.
 *
 * A clip body is untrusted HTML copied from another application. It is normalised (CF_HTML
 * wrapper, Word/Excel mso-*, <o:p>, comments, <html><body> envelope) and then purified with a
 * strict allow-list. Images may only be inline `data:image/` payloads; every other source is
 * replaced with a drawn placeholder BEFORE the markup reaches the DOM, so the row can never
 * issue a network request. The body is fetched lazily per mounted row (see ClipList).
 */
import { useEffect, useMemo, useState } from 'react'
import DOMPurify from 'dompurify'
import type { ClipBody, ClipItem } from './host'

const TAGS = [
  'p', 'br', 'b', 'strong', 'i', 'em', 'u', 's', 'strike', 'sub', 'sup', 'span', 'div',
  'ul', 'ol', 'li', 'blockquote', 'pre', 'code', 'h1', 'h2', 'h3', 'h4',
  'table', 'thead', 'tbody', 'tr', 'td', 'th', 'a', 'img', 'hr',
]
const ATTRS = ['href', 'src', 'alt', 'title', 'colspan', 'rowspan', 'align', 'style']

/** The only inline properties a source clip may name (DESIGN-language §3: row text stays 14/20). */
const PROPS = new Set([
  'color', 'background-color', 'font-weight', 'font-style', 'text-decoration', 'text-align',
  'font-family', 'font-size', 'white-space',
])

/** A declaration value that cannot escape its property or reach the network. */
const SAFE_VALUE = /^(?!.*(?:expression|javascript:|url\s*\(|@import|behavior\s*:|[<>\\]))[\w#%.,()\s'"/+-]+$/i

/** The two row surfaces a lone source colour must survive; selected rows sit next to both. */
const SURFACES: [number, number, number][] = [
  [255, 255, 255],
  [20, 24, 33],
]

const PLACEHOLDER =
  '<span class="rich-img-ph" role="img" aria-label="image"><svg width="14" height="14" viewBox="0 0 16 16" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round"><rect x="1.75" y="2.75" width="12.5" height="10.5" rx="2"/><path d="M2.5 11.2 6 7.7l2.4 2.3 2.7-3.4 2.4 4.1"/><circle cx="10.6" cy="5.6" r="1"/></svg>image</span>'

let probe: CanvasRenderingContext2D | null = null

function rgbOf(value: string): [number, number, number] | null {
  if (typeof CSS !== 'undefined' && !CSS.supports('color', value)) return null
  probe ??= document.createElement('canvas').getContext('2d')
  if (!probe) return null
  probe.fillStyle = '#010203'
  probe.fillStyle = value
  const out = probe.fillStyle
  const match = /^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(out)
  if (!match) return null // rgba() and system colours are not a colour we can prove
  return [parseInt(match[1], 16), parseInt(match[2], 16), parseInt(match[3], 16)]
}

function luminance([r, g, b]: [number, number, number]): number {
  const channel = (value: number) => {
    const v = value / 255
    return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4
  }
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b)
}

function contrast(a: [number, number, number], b: [number, number, number]): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (hi + 0.05) / (lo + 0.05)
}

/** Word font sizes arrive in pt; the row's functional floor is 11 px and its ceiling 16 px. */
function fontSize(value: string): string | null {
  const match = /^(\d+(?:\.\d+)?)\s*(pt|px|em|rem|%)?$/i.exec(value.trim())
  if (!match) return null
  const number = parseFloat(match[1])
  const unit = (match[2] ?? 'px').toLowerCase()
  const px = unit === 'pt' ? number * (4 / 3) : unit === 'em' || unit === 'rem' ? number * 14 : unit === '%' ? (number * 14) / 100 : number
  return `${Math.round(Math.min(16, Math.max(11, px)))}px`
}

function fontWeight(value: string): string | null {
  if (/^(bold|bolder|[6-9]00)$/i.test(value)) return '600'
  if (/^(normal|lighter|[1-4]00)$/i.test(value)) return '400'
  if (value === '500') return '500'
  return null
}

/**
 * Word/Excel: keep only the allowed properties, drop mso-*, clamp the type scale, and keep a
 * source colour only when its own background proves 4.5:1 on BOTH themes (a clip copied with
 * black ink must not paint black on the dark row).
 */
function cleanStyle(el: Element): void {
  const raw = el.getAttribute('style')
  if (!raw) return
  const kept = new Map<string, string>()
  for (const declaration of raw.split(';')) {
    const at = declaration.indexOf(':')
    if (at < 0) continue
    const prop = declaration.slice(0, at).trim().toLowerCase()
    const value = declaration.slice(at + 1).trim()
    if (!PROPS.has(prop) || !value || !SAFE_VALUE.test(value)) continue
    let mapped: string | null = value
    if (prop === 'font-weight') mapped = fontWeight(value)
    else if (prop === 'font-size') mapped = fontSize(value)
    else if (prop === 'font-style') mapped = /^(italic|oblique)$/i.test(value) ? 'italic' : /^normal$/i.test(value) ? 'normal' : null
    else if (prop === 'text-align') mapped = /^(start|end|left|right|center|justify)$/i.test(value) ? value : null
    else if (prop === 'text-decoration') mapped = /^(none|underline|line-through|overline)(\s+(underline|line-through|overline))*$/i.test(value) ? value : null
    else if (prop === 'white-space') mapped = /^(normal|pre|pre-wrap|pre-line)$/i.test(value) ? value : null
    if (mapped) kept.set(prop, mapped)
  }
  const ink = kept.has('color') ? rgbOf(kept.get('color')!) : null
  const fill = kept.has('background-color') ? rgbOf(kept.get('background-color')!) : null
  kept.delete('color')
  kept.delete('background-color')
  if (ink && fill && contrast(ink, fill) >= 4.5) {
    kept.set('color', `rgb(${ink.join(', ')})`)
    kept.set('background-color', `rgb(${fill.join(', ')})`)
  } else if (ink && SURFACES.every((surface) => contrast(ink, surface) >= 4.5)) {
    kept.set('color', `rgb(${ink.join(', ')})`)
  }
  if (kept.size === 0) el.removeAttribute('style')
  else el.setAttribute('style', [...kept].map(([prop, value]) => `${prop}: ${value}`).join('; '))
}

/** Windows puts a CF_HTML header in front of copied HTML; its offsets are UTF-8 byte offsets. */
export function cfHtmlFragment(raw: string): string {
  if (!/^Version:\d+(\.\d+)?\r?\n/.test(raw)) return raw
  const offset = (name: string) => {
    const match = new RegExp(`^${name}:(\\d+)\\s*$`, 'm').exec(raw)
    return match ? Number(match[1]) : -1
  }
  const start = offset('StartHTML')
  const end = offset('EndHTML')
  const fallback = raw.slice(Math.max(0, raw.indexOf('<')))
  if (start < 0) return fallback
  try {
    const bytes = new TextEncoder().encode(raw)
    const body = new TextDecoder('utf-8').decode(bytes.subarray(start, end > start ? end : bytes.length))
    return body.includes('<') ? body : fallback
  } catch {
    return fallback
  }
}

const BLOCKED = 'script,style,link,meta,iframe,object,embed,form,input,svg,math,template,noscript,base,audio,video,source,select,textarea,button,o\\:p,xml'

let hooked = false

/** Sanitise one clip body into row HTML. Safe to call on every render; callers memoise it. */
export function toRichHtml(raw: string): string {
  if (!raw || raw.length < 2) return ''
  const doc = new DOMParser().parseFromString(cfHtmlFragment(raw), 'text/html')

  doc.body.querySelectorAll(BLOCKED).forEach((el) => el.remove())
  const walker = doc.createTreeWalker(doc.body, NodeFilter.SHOW_COMMENT)
  const comments: ChildNode[] = []
  while (walker.nextNode()) comments.push(walker.currentNode as ChildNode)
  comments.forEach((node) => node.remove())

  for (const el of doc.body.querySelectorAll('*')) cleanStyle(el)
  // Word separates words with <span style='mso-spacerun:yes'> </span>; a span with no content
  // at all is the only one safe to drop (a space is still a word).
  for (const span of doc.body.querySelectorAll('span')) {
    if (span.children.length === 0 && span.textContent === '') span.remove()
  }

  if (!hooked) {
    hooked = true
    DOMPurify.addHook('uponSanitizeAttribute', (_node, data) => {
      if (data.attrName === 'href' && !/^https?:\/\//i.test(data.attrValue)) data.keepAttr = false
      if (data.attrName === 'src' && !/^data:image\/(?:png|jpe?g|gif|webp);base64,/i.test(data.attrValue)) data.keepAttr = false
    })
  }

  const clean = DOMPurify.sanitize(doc.body.innerHTML, {
    ALLOWED_TAGS: TAGS,
    ALLOWED_ATTR: ATTRS,
    ALLOW_DATA_ATTR: false,
    ALLOW_ARIA_ATTR: false,
    FORBID_TAGS: ['script', 'style', 'link', 'meta', 'iframe', 'object', 'embed', 'form', 'input', 'svg', 'math', 'template', 'base', 'audio', 'video', 'source', 'select', 'textarea', 'button'],
    KEEP_CONTENT: true,
  })

  // Links are text in a row: keep the readable anchor, never the navigation.
  const out = new DOMParser().parseFromString(clean, 'text/html')
  for (const anchor of out.body.querySelectorAll('a')) {
    anchor.removeAttribute('href')
    anchor.setAttribute('tabindex', '-1')
  }
  for (const img of out.body.querySelectorAll('img')) {
    const src = img.getAttribute('src') ?? ''
    if (/^data:image\/(?:png|jpe?g|gif|webp);base64,/i.test(src)) {
      img.setAttribute('loading', 'eager')
      img.setAttribute('draggable', 'false')
    } else {
      // Not even an empty <img> may remain: the browser would draw a broken-image glyph.
      const holder = out.createElement('span')
      holder.innerHTML = PLACEHOLDER
      img.replaceWith(holder.firstElementChild!)
    }
  }
  return out.body.innerHTML.trim()
}

export function pathsOf(text: string): string[] {
  return text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
}

// ---- lazy body cache --------------------------------------------------------------------
// Only mounted rows call this. The cache is per page session; entries are dropped oldest-first
// so scrolling a 1,000-row history cannot pin every body in memory.
const bodyCache = new Map<number, ClipBody | null>()
const inflight = new Map<number, Promise<ClipBody | null>>()
const CACHE_MAX = 200
type GetItem = (id: number) => Promise<ClipBody | null>

/** Mounted rows of a clip that was just edited, so the row refetches instead of keeping the old body. */
const forgotten = new Set<(id: number) => void>()

/** A saved edit replaces the stored body; drop the cached copy and tell the row to fetch it again. */
export function forgetBody(id: number): void {
  bodyCache.delete(id)
  inflight.delete(id)
  for (const listener of forgotten) listener(id)
}

export function loadBody(id: number, getItem: GetItem): Promise<ClipBody | null> {
  if (bodyCache.has(id)) return Promise.resolve(bodyCache.get(id) ?? null)
  const running = inflight.get(id)
  if (running) return running
  const request = getItem(id)
    .then((body) => {
      if (bodyCache.size >= CACHE_MAX) {
        const oldest = bodyCache.keys().next().value
        if (oldest != null) bodyCache.delete(oldest)
      }
      bodyCache.set(id, body ?? null)
      return body ?? null
    })
    .catch(() => {
      // Do not pin a transient bridge failure: the next mount may retry.
      bodyCache.delete(id)
      return null
    })
    .finally(() => inflight.delete(id))
  inflight.set(id, request)
  return request
}

export function isRich(clip: ClipItem): boolean {
  return clip.hasHtml || clip.hasRtf
}

/** The row body: rich HTML when the clip has a safe body, plain preview until it arrives. */
export function RowBody({ clip, getItem }: { clip: ClipItem; getItem: GetItem }) {
  const rich = isRich(clip)
  const [body, setBody] = useState<ClipBody | null | undefined>(() => (rich ? bodyCache.get(clip.id) : undefined))
  const [recall, setRecall] = useState(0)

  useEffect(() => {
    const listener = (id: number) => {
      if (id === clip.id) setRecall((count) => count + 1)
    }
    forgotten.add(listener)
    return () => {
      forgotten.delete(listener)
    }
  }, [clip.id])

  useEffect(() => {
    if (!rich) {
      setBody(undefined)
      return
    }
    if (bodyCache.has(clip.id)) {
      setBody(bodyCache.get(clip.id) ?? null)
      return
    }
    let alive = true
    loadBody(clip.id, getItem).then((value) => {
      if (alive) setBody(value)
    })
    return () => {
      alive = false
    }
  }, [clip.id, rich, getItem, recall])

  const safe = useMemo(() => (body?.html ? toRichHtml(body.html) : ''), [body])
  const plain = body?.text || clip.text || clip.title
  const title = plain

  if (clip.kind === 'files') {
    const paths = pathsOf(clip.text)
    return (
      <div className="row-text row-files" title={title}>
        {paths.slice(0, 2).map((path, index) => {
          const cut = Math.max(path.lastIndexOf('\\'), path.lastIndexOf('/'))
          return (
            <span className="file-path" key={`${index}:${path}`} title={path}>
              <span className="file-dir">{path.slice(0, cut + 1)}</span>
              <span className="file-name">{path.slice(cut + 1)}</span>
            </span>
          )
        })}
        {paths.length > 2 && (
          <span className="chip" title={`${paths.length - 2} more ${paths.length - 2 === 1 ? 'file' : 'files'}: ${paths.slice(2).join('\n')}`}>
            +{paths.length - 2}
            <span className="chip-unit"> {paths.length - 2 === 1 ? 'file' : 'files'}</span>
          </span>
        )}
      </div>
    )
  }

  // OCR text is stored as the clip's text (step plan 3), so the words are the row body while the
  // picture stays the payload. No OCR text (older clip, or no language pack) still shows a line.
  if (clip.hasImage) {
    const words = clip.text.trim()
    return (
      <div className="row-ocr">
        {words && <div className="ocr-mark">Text read from image</div>}
        <div className={'row-text' + (words ? '' : ' ocr-none')} title={title}>
          {words || 'No text found in this image'}
        </div>
      </div>
    )
  }

  if (rich && safe) {
    return <div className="row-text clip-rich" title={title} dangerouslySetInnerHTML={{ __html: safe }} />
  }
  return <div className="row-text" title={title}>{plain}</div>
}
