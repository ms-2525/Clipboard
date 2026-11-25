import { forwardRef, useEffect, useImperativeHandle, useLayoutEffect, useRef, useState } from 'react'
import { useVirtualizer } from '@tanstack/react-virtual'
import type { ClipBody, ClipItem } from './host'
import { formatStamp, type TimeFormat } from './format'
import { IconPin } from './icons'
import { isRich, loadBody, RowBody } from './rich'

const thumbCache = new Map<number, string | null>()
const THUMB_MAX = 200

/** Same FIFO cap as the body cache: scrolling history must not pin every encoded thumbnail. */
const cacheThumb = (id: number, url: string | null) => {
  if (thumbCache.size >= THUMB_MAX) thumbCache.delete(thumbCache.keys().next().value!)
  thumbCache.set(id, url)
}

const compact = (value: number) =>
  value >= 1000 ? `${(value / 1000).toFixed(value >= 10000 ? 0 : 1).replace(/\.0$/, '')}k` : String(value)

interface Props {
  clips: ClipItem[]
  selectedId: number | null
  timeFormat: TimeFormat
  /** The Pinned tab: every row there is pinned, so the per-row glyph is noise. */
  hidePins: boolean
  onSelect: (id: number) => void
  onCopy: (clip: ClipItem) => void
  onContextMenu: (clip: ClipItem, x: number, y: number) => void
  onTag: (id: string) => void
  getThumb: (id: number) => Promise<string | null>
  getItem: (id: number) => Promise<ClipBody | null>
  emptyTitle: string
  emptySub: string
}

/** PageUp/PageDown must move by what is actually on screen, which only the list knows. */
export interface ClipListHandle {
  pageBy: (direction: 1 | -1) => void
  /** Section jumps put their first row at the top of the viewport, not wherever minimal scroll lands. */
  revealTop: (id: number) => void
}

function Thumb({ id, getThumb }: { id: number; getThumb: (id: number) => Promise<string | null> }) {
  const [src, setSrc] = useState<string | null>(() => thumbCache.get(id) ?? null)

  useEffect(() => {
    if (thumbCache.has(id)) {
      setSrc(thumbCache.get(id) ?? null)
      return
    }
    let alive = true
    getThumb(id)
      .then((url) => {
        cacheThumb(id, url)
        if (alive) setSrc(url)
      })
      .catch(() => {
        // A missing thumbnail is not fatal: keep the styled placeholder.
        cacheThumb(id, null)
      })
    return () => {
      alive = false
    }
  }, [id, getThumb])

  if (!src) {
    return (
      <div className="thumb thumb-empty" aria-hidden="true">
        <svg width="22" height="22" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round">
          <rect x="2" y="3" width="12" height="10" rx="2" />
          <path d="M2.6 11 6 7.6l2.3 2.2L11 6.6l2.3 4" />
          <circle cx="10.4" cy="5.4" r="0.9" />
        </svg>
      </div>
    )
  }
  return <img className="thumb" src={src} alt="" draggable={false} />
}

export const ClipList = forwardRef<ClipListHandle, Props>(function ClipList(
  { clips, selectedId, timeFormat, hidePins, onSelect, onCopy, onContextMenu, onTag, getThumb, getItem, emptyTitle, emptySub },
  ref,
) {
  const scrollRef = useRef<HTMLDivElement>(null)
  const wrapRef = useRef<HTMLDivElement>(null)
  const rowEls = useRef(new Map<number, HTMLDivElement | null>())
  const hadFocus = useRef(false)
  const anchor = useRef<{ id: number; offset: number } | null>(null)
  const previous = useRef<{ first: number | null; length: number }>({ first: null, length: 0 })
  const dataMoved = useRef(false)

  const virt = useVirtualizer({
    count: clips.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => 84,
    overscan: 4,
    getItemKey: (i) => clips[i].id,
  })

  // Live capture: remember which row is at the top of the viewport, so a prepend can be
  // absorbed without moving what the user is reading.
  const rememberAnchor = () => {
    const el = scrollRef.current
    const top = virt.getVirtualItems()[0]
    if (!el || !top) return
    const clip = clips[top.index]
    if (clip) anchor.current = { id: clip.id, offset: top.start - el.scrollTop }
  }

  useLayoutEffect(() => {
    const el = scrollRef.current
    const before = previous.current
    const first = clips[0]?.id ?? null
    previous.current = { first, length: clips.length }
    const singlePrepend = before.first != null && clips.length === before.length + 1 && clips[1]?.id === before.first
    // A re-copied row moves from somewhere in the list to index 0 without changing the count.
    const reordered =
      before.first != null &&
      clips.length === before.length &&
      clips[0]?.id !== before.first &&
      clips[1]?.id === before.first
    dataMoved.current = singlePrepend || reordered
    if (!el || !(singlePrepend || reordered) || el.scrollTop <= 8) return
    const saved = anchor.current
    if (!saved) return
    // The anchor itself is the row that moved to the top; leave the viewport where it is.
    if (saved.id === clips[0]?.id) return
    const index = clips.findIndex((clip) => clip.id === saved.id)
    if (index < 0) return
    const target = virt.getOffsetForIndex(index, 'start')
    // saved.offset is the anchor's top relative to the viewport top (negative when it starts
    // above it); keep that same visual position after the new row pushes it down.
    if (!target) return
    const place = () => {
      el.scrollTop = target[0] - saved.offset
    }
    place()
    // The new row is measured after this commit; re-apply once the sizes have settled.
    const frame = requestAnimationFrame(place)
    return () => cancelAnimationFrame(frame)
  }, [clips, virt])

  useEffect(() => {
    if (selectedId == null) return
    // A capture (prepend or reorder) must not drag the list to the selection; a filter or tab
    // change must.
    if (dataMoved.current) {
      dataMoved.current = false
      return
    }
    const idx = clips.findIndex((clip) => clip.id === selectedId)
    if (idx < 0) return
    const viewport = scrollRef.current?.clientHeight ?? 0
    const offset = virt.scrollOffset ?? 0
    const item = virt.getVirtualItems().find((entry) => entry.index === idx)
    if (item && viewport > 0) {
      // A row taller than the viewport can never be fully shown; show its beginning, not its end.
      if (item.size > viewport) {
        virt.scrollToIndex(idx, { align: 'start' })
        return
      }
      // Moving inside what is already on screen must not move the list.
      if (item.start >= offset && item.end <= offset + viewport) return
    }
    virt.scrollToIndex(idx, { align: 'auto' })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedId, clips])

  // The selected row is fetched even before the virtualizer measures it, so selection never
  // shows a plain preview that an on-screen row would already have replaced.
  useEffect(() => {
    const clip = clips.find((entry) => entry.id === selectedId)
    if (clip && isRich(clip)) void loadBody(clip.id, getItem)
  }, [clips, selectedId, getItem])

  // hadFocus = "the focused element is inside the list". Read from focusin rather than
  // focusout: a virtualized row can unmount and drop focus to <body> with a null
  // relatedTarget, and that is exactly when the next selected row must take focus back.
  useEffect(() => {
    const onFocusIn = (event: FocusEvent) => {
      hadFocus.current = !!wrapRef.current?.contains(event.target as Node)
    }
    document.addEventListener('focusin', onFocusIn)
    return () => document.removeEventListener('focusin', onFocusIn)
  }, [])

  // Focus and selection are the same thing: the row that is highlighted is the row
  // that owns focus, so a keyboard user can never hit Enter on a clip they cannot see.
  useEffect(() => {
    if (!hadFocus.current || selectedId == null) return
    const existing = rowEls.current.get(selectedId)
    if (existing) {
      existing.focus({ preventScroll: true })
      return
    }
    let frame = 0
    let tries = 0
    const step = () => {
      const el = rowEls.current.get(selectedId)
      if (el) {
        el.focus({ preventScroll: true })
        return
      }
      if (tries++ < 8) frame = requestAnimationFrame(step)
    }
    frame = requestAnimationFrame(step)
    return () => cancelAnimationFrame(frame)
  }, [selectedId, clips])

  useImperativeHandle(
    ref,
    () => ({
      pageBy: (direction) => {
        if (clips.length === 0) return
        // The span is the measured rows actually on screen (overscan excluded), not a fixed count.
        const viewport = scrollRef.current?.clientHeight ?? 0
        const top = virt.scrollOffset ?? 0
        const shown = virt.getVirtualItems().filter((entry) => entry.end > top && entry.start < top + viewport)
        const span = shown.length > 1 ? Math.max(1, shown[shown.length - 1].index - shown[0].index) : 1
        const index = clips.findIndex((clip) => clip.id === selectedId)
        const from = index < 0 ? 0 : index
        const next = Math.max(0, Math.min(clips.length - 1, from + direction * span))
        onSelect(clips[next].id)
      },
      revealTop: (id) => {
        const index = clips.findIndex((clip) => clip.id === id)
        if (index >= 0) virt.scrollToIndex(index, { align: 'start' })
      },
    }),
    [clips, selectedId, onSelect, virt],
  )

  const selectedIndex = clips.findIndex((clip) => clip.id === selectedId)
  const tabbableId = selectedIndex >= 0 ? selectedId : clips[0]?.id ?? null

  return (
    <div className="list-wrap" ref={wrapRef}>
      <div className="list" ref={scrollRef} onScroll={rememberAnchor} role="listbox" aria-label="Clipboard history">
        {clips.length === 0 ? (
          <div className="empty">
            <div>
              <div className="t-title">{emptyTitle}</div>
              <div className="t-sec" style={{ marginTop: 4 }}>
                {emptySub}
              </div>
            </div>
          </div>
        ) : (
          <div style={{ height: virt.getTotalSize(), position: 'relative' }}>
            {virt.getVirtualItems().map((v) => {
              const clip = clips[v.index]
              const selected = clip.id === selectedId
              const tags = clip.tags
              const richOnly = clip.hasRtf && !clip.hasHtml
              // The row's tag slot keeps the 2-chip ceiling; the RTF marker takes one slot.
              const shownTags = tags.slice(0, richOnly ? 1 : 2)
              const hiddenTags = tags.length - shownTags.length
              const extra = clip.truncated ? Math.max(0, clip.remainingChars) : 0
              return (
                <div
                  key={v.key}
                  data-index={v.index}
                  data-id={clip.id}
                  ref={(el) => {
                    rowEls.current.set(clip.id, el)
                    virt.measureElement(el)
                  }}
                  role="option"
                  aria-selected={selected}
                  aria-posinset={v.index + 1}
                  aria-setsize={clips.length}
                  tabIndex={clip.id === tabbableId ? 0 : -1}
                  className={'row' + (selected ? ' selected' : '')}
                  style={{ transform: `translateY(${v.start}px)` }}
                  onClick={() => onSelect(clip.id)}
                  onFocus={() => onSelect(clip.id)}
                  onDoubleClick={() => onCopy(clip)}
                  onContextMenu={(event) => {
                    event.preventDefault()
                    onSelect(clip.id)
                    onContextMenu(clip, event.clientX, event.clientY)
                  }}
                >
                  <div className="row-index">{String(v.index + 1).padStart(2, '0')}</div>
                  <div className="row-content">
                    <div className="row-body">
                      {clip.hasImage && <Thumb id={clip.id} getThumb={getThumb} />}
                      <RowBody clip={clip} getItem={getItem} />
                    </div>
                    <div className="row-prov">
                      <span className="prov-what">
                        {clip.pinned && !hidePins && (
                          <span className="pin" title="Pinned">
                            <IconPin />
                          </span>
                        )}
                        <span className="prov-app">{clip.app}</span> · {clip.title}
                      </span>
                      <span className="prov-time">{formatStamp(clip.time, timeFormat)}</span>
                      {richOnly && (
                        <span className="chip" title="Rich text (RTF) — the list shows its plain text">
                          Rich text
                        </span>
                      )}
                      {shownTags.map((id) => (
                        <button
                          key={id}
                          type="button"
                          className="chip tag"
                          tabIndex={-1}
                          title={`Filter the list by ${id}`}
                          onMouseDown={(event) => event.preventDefault()}
                          onClick={(event) => {
                            event.stopPropagation()
                            onTag(id)
                          }}
                        >
                          {id}
                        </button>
                      ))}
                      {hiddenTags > 0 && (
                        <span className="chip" title={`${hiddenTags} more ${hiddenTags === 1 ? 'tag' : 'tags'}: ${tags.slice(shownTags.length).join(', ')}`}>
                          +{hiddenTags}
                          <span className="chip-unit"> {hiddenTags === 1 ? 'tag' : 'tags'}</span>
                        </span>
                      )}
                      {extra > 0 && (
                        <span className="chip facts-chip" title={`${clip.remainingChars.toLocaleString()} more characters`}>
                          +{compact(extra)}
                          <span className="chip-unit"> chars</span>
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </div>
    </div>
  )
})
