import { useEffect, useMemo, useState } from 'react'
import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet'
import * as L from 'leaflet'

// ✅ Ensure Leaflet marker icons work with Vite/Vercel bundling
import marker2x from 'leaflet/dist/images/marker-icon-2x.png'
import marker from 'leaflet/dist/images/marker-icon.png'
import shadow from 'leaflet/dist/images/marker-shadow.png'
L.Icon.Default.mergeOptions({
  iconRetinaUrl: marker2x,
  iconUrl: marker,
  shadowUrl: shadow,
})

// Types
type RoadClosure = {
  id: number
  roadName: string
  status: 'OPEN' | 'PARTIAL' | 'CLOSED'
  note?: string
  updatedAt: string
  lat?: number | null
  lng?: number | null
}

// API helpers (relative paths; Vercel rewrite will proxy in prod)
async function getClosures(status?: string) {
  const q = status ? `?status=${encodeURIComponent(status)}` : ''
  const r = await fetch(`/api/closures${q}`)
  if (!r.ok) throw new Error('Failed to load closures')
  return (await r.json()) as RoadClosure[]
}

async function createDebrisRequest(body: {
  fullName: string
  address: string
  email?: string
  phone?: string
  zoneId?: number
  notes?: string
  lat?: number
  lng?: number
}) {
  const r = await fetch('/api/debris-requests', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!r.ok) throw new Error(await r.text())
  return (await r.json()) as { id: number }
}

// Small helper to force Leaflet to recalc size after first paint
function MapResizeFix() {
  const map = useMap()
  useEffect(() => {
    setTimeout(() => map.invalidateSize(), 0)
  }, [map])
  return null
}

export default function App() {
  const [closures, setClosures] = useState<RoadClosure[]>([])
  const [status, setStatus] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setError(null)
    getClosures(status).then(setClosures).catch(e => setError(e.message))
  }, [status])

  // Default map center = Cumberland County / Fayetteville, NC
  const center = useMemo<[number, number]>(() => [35.0527, -78.8784], [])

  // Only plot items with valid numeric coords
  const points = useMemo(
    () =>
      closures.filter(
        c => typeof c.lat === 'number' && typeof c.lng === 'number'
      ),
    [closures]
  )

  return (
    <main className="container">
      <header className="header">
        <h1 className="title">Cumberland Storm Status</h1>
      </header>

      <section className="panel section" aria-labelledby="closures-heading">
        <div className="controls">
          <h2 id="closures-heading" style={{ margin: 0 }}>Road Closures</h2>
          <span className="badge">
            <strong>Total:</strong>&nbsp;{closures.length}
          </span>
          <label style={{ marginLeft: 'auto' }}>
            <span className="small" style={{ marginRight: 6 }}>Filter</span>
            <select
              className="select"
              value={status}
              onChange={e => setStatus(e.target.value)}
            >
              <option value="">All</option>
              <option value="OPEN">OPEN</option>
              <option value="PARTIAL">PARTIAL</option>
              <option value="CLOSED">CLOSED</option>
            </select>
          </label>
        </div>

        {error && <p className="small" style={{ color: 'crimson' }}>{error}</p>}

        {closures.length === 0 ? (
          <p className="small">No records.</p>
        ) : (
          <ul className="list">
            {closures.map(c => (
              <li key={c.id}>
                <strong>{c.roadName}</strong>{' '}
                <span
                  className={
                    c.status === 'CLOSED'
                      ? 'badge status-closed'
                      : c.status === 'PARTIAL'
                      ? 'badge status-partial'
                      : 'badge status-open'
                  }
                  title={c.status}
                >
                  {c.status}
                </span>
                {c.note ? <span> — {c.note}</span> : null}{' '}
                <span className="small">
                  (updated {new Date(c.updatedAt).toLocaleString()})
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* Map below the list */}
      <section className="section">
        <div className="panel">
          <MapContainer
            center={center}
            zoom={11}
            className="map-root"
            style={{ height: 420, width: '100%' }} // hard guarantee for prod
            scrollWheelZoom
          >
            <MapResizeFix />
            <TileLayer
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              attribution='&copy; <a href="https://www.openstreetmap.org/">OpenStreetMap</a> contributors'
            />
            {points.map((c) => (
              <Marker key={c.id} position={[c.lat as number, c.lng as number]}>
                <Popup>
                  <div style={{ maxWidth: 260 }}>
                    <strong>{c.roadName}</strong>
                    <div className="small" style={{ margin: '4px 0' }}>
                      Status: <b>{c.status}</b>
                    </div>
                    {c.note && <div style={{ marginTop: 6 }}>{c.note}</div>}
                    <div className="small" style={{ marginTop: 6 }}>
                      Updated {new Date(c.updatedAt).toLocaleString()}
                    </div>
                  </div>
                </Popup>
              </Marker>
            ))}
          </MapContainer>
        </div>
      </section>

      <section className="panel section" aria-labelledby="debris-heading">
        <h2 id="debris-heading" style={{ marginTop: 0 }}>Request Debris Pickup</h2>
        <DebrisForm onSubmitted={id => alert(`Submitted! Ticket #${id}`)} />
      </section>
    </main>
  )
}

function DebrisForm({ onSubmitted }: { onSubmitted: (id: number) => void }) {
  const [fullName, setFullName] = useState('')
  const [address, setAddress] = useState('')
  const [email, setEmail] = useState('')
  const [notes, setNotes] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    if (!fullName.trim() || !address.trim()) {
      setError('Full name and address are required.')
      return
    }
    try {
      setBusy(true)
      const r = await createDebrisRequest({
        fullName,
        address,
        email: email || undefined,
        notes: notes || undefined,
      })
      onSubmitted(r.id)
      setFullName('')
      setAddress('')
      setEmail('')
      setNotes('')
    } catch (err: any) {
      setError(err.message || 'Submit failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="form">
      <label>
        <span>Full name *</span>
        <input value={fullName} onChange={e => setFullName(e.target.value)} required />
      </label>
      <label>
        <span>Address *</span>
        <input value={address} onChange={e => setAddress(e.target.value)} required />
      </label>
      <label>
        <span>Email (optional)</span>
        <input type="email" value={email} onChange={e => setEmail(e.target.value)} />
      </label>
      <label>
        <span>Notes</span>
        <textarea rows={3} value={notes} onChange={e => setNotes(e.target.value)} />
      </label>
      {error && (
        <p className="error" role="alert" style={{ marginTop: 6 }}>
          {error}
        </p>
      )}
      <div className="actions">
        <button type="submit" className="button primary" disabled={busy}>
          {busy ? 'Submitting…' : 'Submit'}
        </button>
      </div>
    </form>
  )
}
