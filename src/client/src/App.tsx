import { useEffect, useMemo, useState } from 'react'
import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet'
import * as L from 'leaflet'
import 'leaflet/dist/leaflet.css'

// ---------- Types ----------
type RoadClosure = {
  id: number
  roadName: string
  status: 'OPEN' | 'PARTIAL' | 'CLOSED' | string
  note?: string
  updatedAt: string
  lat?: number
  lng?: number
}

// ---------- API helpers ----------
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

// ---------- Leaflet marker icon fix (Vite) ----------
const markerIcon = L.icon({
  iconUrl: new URL('leaflet/dist/images/marker-icon.png', import.meta.url).toString(),
  iconRetinaUrl: new URL('leaflet/dist/images/marker-icon-2x.png', import.meta.url).toString(),
  shadowUrl: new URL('leaflet/dist/images/marker-shadow.png', import.meta.url).toString(),
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
})

// ---------- Map helpers ----------
const DEFAULT_CENTER: [number, number] = [35.0527, -78.8784] // Fayetteville / Cumberland County
const DEFAULT_ZOOM = 11

function FitBoundsOnData({ points }: { points: Array<[number, number]> }) {
  const map = useMap()
  useEffect(() => {
    if (!points.length) return
    if (points.length === 1) {
      map.setView(points[0], 13)
      return
    }
    const bounds = L.latLngBounds(points)
    map.fitBounds(bounds, { padding: [30, 30] })
  }, [points, map])
  return null
}

// ---------- Main App ----------
export default function App() {
  const [closures, setClosures] = useState<RoadClosure[]>([])
  const [status, setStatus] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setError(null)
    getClosures(status).then(setClosures).catch(e => setError(e.message))
  }, [status])

  const points = useMemo(
    () =>
      closures
        .filter(c => typeof c.lat === 'number' && typeof c.lng === 'number')
        .map(c => [c.lat as number, c.lng as number] as [number, number]),
    [closures]
  )

  return (
    <main className="container">
      <header className="page-header">
        <h1>Cumberland Storm Status</h1>
      </header>

      {/* Closures list + filter */}
      <section className="card" aria-labelledby="closures-heading">
        <div className="card-header">
          <h2 id="closures-heading">Road Closures</h2>
          <div className="filters">
            <label className="select">
              <span>Status</span>
              <select value={status} onChange={e => setStatus(e.target.value)}>
                <option value="">All</option>
                <option value="OPEN">OPEN</option>
                <option value="PARTIAL">PARTIAL</option>
                <option value="CLOSED">CLOSED</option>
              </select>
            </label>
          </div>
        </div>

        {error && <p className="error">{error}</p>}

        {closures.length === 0 ? (
          <p className="muted">No records.</p>
        ) : (
          <ul className="list">
            {closures.map(c => (
              <li key={c.id} className="list-item">
                <div className="row">
                  <strong className="name">{c.roadName}</strong>
                  <span className={`badge ${c.status.toLowerCase()}`}>{c.status}</span>
                </div>
                {c.note && <div className="note">{c.note}</div>}
                <div className="subtle">
                  Updated {new Date(c.updatedAt).toLocaleString()}
                  {typeof c.lat === 'number' && typeof c.lng === 'number'
                    ? ` · (${c.lat.toFixed(4)}, ${c.lng.toFixed(4)})`
                    : ''}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* Map BELOW the list (no title) */}
      <section className="card map-card" aria-label="Map">
        <MapContainer
          center={DEFAULT_CENTER}
          zoom={DEFAULT_ZOOM}
          className="map-root"
          scrollWheelZoom
        >
          <TileLayer
            // OSM tiles — attribution required
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            attribution='&copy; <a href="https://www.openstreetmap.org/">OpenStreetMap</a> contributors'
          />
          <FitBoundsOnData points={points} />
          {closures
            .filter(c => typeof c.lat === 'number' && typeof c.lng === 'number')
            .map(c => (
              <Marker key={c.id} position={[c.lat as number, c.lng as number]} icon={markerIcon}>
                <Popup>
                  <div style={{ maxWidth: 260 }}>
                    <div style={{ fontWeight: 700 }}>{c.roadName}</div>
                    <div style={{ margin: '6px 0' }}>
                      <span className={`badge ${c.status.toLowerCase()}`}>{c.status}</span>
                    </div>
                    {c.note && <div style={{ marginTop: 4 }}>{c.note}</div>}
                    <div className="subtle" style={{ marginTop: 6 }}>
                      Updated {new Date(c.updatedAt).toLocaleString()}
                    </div>
                  </div>
                </Popup>
              </Marker>
            ))}
        </MapContainer>
      </section>

      {/* Debris request form */}
      <section className="card" aria-labelledby="debris-heading">
        <h2 id="debris-heading">Request Debris Pickup</h2>
        <DebrisForm onSubmitted={id => alert(`Submitted! Ticket #${id}`)} />
      </section>
    </main>
  )
}

// ---------- Debris Form ----------
function DebrisForm({ onSubmitted }: { onSubmitted: (id: number) => void }) {
  const [fullName, setFullName] = useState('')
  const [address, setAddress] = useState('')
  const [email, setEmail] = useState('')
  const [notes, setNotes] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [okMsg, setOkMsg] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setOkMsg(null)
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
      setOkMsg(`Request submitted! Ticket #${r.id}`)
      setFullName('')
      setAddress('')
      setEmail('')
      setNotes('')
    } catch (err: any) {
      setError(err?.message || 'Submit failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="form">
      {okMsg && <div className="alert success">{okMsg}</div>}
      {error && <div className="alert error">{error}</div>}

      <label className="field">
        <span>Full name *</span>
        <input
          value={fullName}
          onChange={e => setFullName(e.target.value)}
          placeholder="Jane Doe"
          required
        />
      </label>

      <label className="field">
        <span>Address *</span>
        <input
          value={address}
          onChange={e => setAddress(e.target.value)}
          placeholder="123 Main St, Fayetteville NC"
          required
        />
      </label>

      <label className="field">
        <span>Email (optional)</span>
        <input
          type="email"
          value={email}
          onChange={e => setEmail(e.target.value)}
          placeholder="name@example.com"
        />
      </label>

      <label className="field">
        <span>Notes</span>
        <textarea
          rows={3}
          value={notes}
          onChange={e => setNotes(e.target.value)}
          placeholder="Anything we should know?"
        />
      </label>

      <div className="actions">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? 'Submitting…' : 'Submit request'}
        </button>
      </div>
    </form>
  )
}
