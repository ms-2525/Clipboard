import { useEffect, useRef, useState } from 'react'
import { useVirtualizer } from '@tanstack/react-virtual'
import type { ClipItem } from './host'
import { formatTime, type TimeFormat } from './format'
import { IconFile, IconPin } from './icons'

const ROW = 96
const thumbCache = new Map<number, string | null>()

const compact = (value: number) =>
  value >= 1000 ? `${(value / 1000).toFixed(value >= 10000 ? 0 : 1).replace(/\.0$/, '')}k` : String(value)

interface Props {
  clips: ClipItem[]
  selectedId: number | null
  timeFormat: TimeFormat
  onSelect: (id: number) => void
  onCopy: (clip: ClipItem) => void
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

function Preview({ clip }: { clip: ClipItem }) {
  if (clip.kind === 'files') {
    const files = clip.text.split('\n').filter(Boolean).slice(0, 3)
    return (
      <div className="preview">
        {files.map((file) => (
          <div className="file-line" key={file}>
            <IconFile />
            <span>{file}</span>
          </div>
        ))}
      </div>
    )
  }
  return <div className="preview">{clip.text}</div>
}

export function ClipList({ clips, selectedId, timeFormat, onSelect, onCopy, getThumb, emptyTitle, emptySub }: Props) {
  const scrollRef = useRef<HTMLDivElement>(null)

  const virt = useVirtualizer({
    count: clips.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => ROW,
    overscan: 5,
    getItemKey: (i) => clips[i].id,
  })

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: 0 })
  }, [clips])

  useEffect(() => {
    if (selectedId == null) return
    const idx = clips.findIndex((clip) => clip.id === selectedId)
    if (idx < 0) return
    const el = scrollRef.current
    if (!el) return
    const top = idx * ROW
    if (top < el.scrollTop || top + ROW > el.scrollTop + el.clientHeight) {
      virt.scrollToIndex(idx, { align: 'auto' })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedId, clips])

  return (
    <div className="list-wrap">
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
              return (
                <div
                  key={v.key}
                  data-row={v.index}
                  role="option"
                  aria-selected={selected}
                  tabIndex={0}
                  className={'row' + (selected ? ' selected' : '')}
                  style={{ transform: `translateY(${v.start}px)` }}
                  onClick={() => onSelect(clip.id)}
                  onDoubleClick={() => onCopy(clip)}
                >
                  <div className="tile" title={clip.app}>
                    {(clip.app || '?').slice(0, 1)}
                  </div>
                  <div className="row-main">
                    <div className="row-title">
                      {clip.pinned && (
                        <span className="pin" title="Pinned">
                          <IconPin />
                        </span>
                      )}
                      <span>{clip.title}</span>
                    </div>
                    <Preview clip={clip} />
                  </div>
                  {clip.hasImage && <Thumb id={clip.id} getThumb={getThumb} />}
                  <div className="facts">
                    {clip.moreCount > 0 && (
                      <span className="chip" title={`${clip.moreCount.toLocaleString()} more characters`}>
                        +{compact(clip.moreCount)}
                      </span>
                    )}
                    <span className="t-meta">{formatTime(clip.time, timeFormat)}</span>
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
