import { useEffect, useRef, useState } from 'react'
import { useVirtualizer } from '@tanstack/react-virtual'
import type { ClipItem } from './host'
import { formatStamp, type TimeFormat } from './format'
import { IconPin } from './icons'

const thumbCache = new Map<number, string | null>()

const compact = (value: number) =>
  value >= 1000 ? `${(value / 1000).toFixed(value >= 10000 ? 0 : 1).replace(/\.0$/, '')}k` : String(value)

interface Props {
  clips: ClipItem[]
  selectedId: number | null
  timeFormat: TimeFormat
  onSelect: (id: number) => void
  onCopy: (clip: ClipItem) => void
  onTag: (id: string) => void
  getThumb: (id: number) => Promise<string | null>
  emptyTitle: string
  emptySub: string
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
        thumbCache.set(id, url)
        if (alive) setSrc(url)
      })
      .catch(() => {
        // A missing thumbnail is not fatal: keep the styled placeholder.
        thumbCache.set(id, null)
      })
    return () => {
      alive = false
    }
  }, [id, getThumb])

  if (!src) return <div className="thumb" aria-hidden="true" />
  return <img className="thumb" src={src} alt="" draggable={false} />
}

export function ClipList({ clips, selectedId, timeFormat, onSelect, onCopy, onTag, getThumb, emptyTitle, emptySub }: Props) {
  const scrollRef = useRef<HTMLDivElement>(null)
  const wrapRef = useRef<HTMLDivElement>(null)
  const rowEls = useRef(new Map<number, HTMLDivElement | null>())
  const hadFocus = useRef(false)

  const virt = useVirtualizer({
    count: clips.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => 84,
    overscan: 4,
    getItemKey: (i) => clips[i].id,
  })

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: 0 })
  }, [clips])

  useEffect(() => {
    if (selectedId == null) return
    const idx = clips.findIndex((clip) => clip.id === selectedId)
    if (idx >= 0) virt.scrollToIndex(idx, { align: 'auto' })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedId, clips])

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
    let frame = 0
    let tries = 0
    const step = () => {
      const el = rowEls.current.get(selectedId)
      if (el) {
        el.focus({ preventScroll: true })
        return
      }
      if (tries++ < 4) frame = requestAnimationFrame(step)
    }
    frame = requestAnimationFrame(step)
    return () => cancelAnimationFrame(frame)
  }, [selectedId, clips])

  const selectedIndex = clips.findIndex((clip) => clip.id === selectedId)
  const tabbableId = selectedIndex >= 0 ? selectedId : clips[0]?.id ?? null

  return (
    <div className="list-wrap" ref={wrapRef}>
      <div className="list" ref={scrollRef} role="listbox" aria-label="Clipboard history">
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
                >
                  <div className="row-index">{String(v.index + 1).padStart(2, '0')}</div>
                  <div className="row-content">
                    <div className="row-body">
                      {clip.hasImage && <Thumb id={clip.id} getThumb={getThumb} />}
                      <div className="row-text">{clip.text || clip.title}</div>
                    </div>
                    <div className="row-prov">
                      <span className="prov-what">
                        {clip.pinned && (
                          <span className="pin" title="Pinned">
                            <IconPin />
                          </span>
                        )}
                        <span className="prov-app">{clip.app}</span> · {clip.title}
                      </span>
                      <span className="prov-time">{formatStamp(clip.time, timeFormat)}</span>
                      {tags.slice(0, 2).map((id) => (
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
                      {tags.length > 2 && (
                        <span className="chip" title={tags.slice(2).join(', ')}>
                          +{tags.length - 2}
                        </span>
                      )}
                      {extra > 0 && (
                        <span className="chip facts-chip" title={`${clip.remainingChars.toLocaleString()} more characters`}>
                          +{compact(extra)}
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
}
