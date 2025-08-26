// src/App.tsx
import { useEffect, useMemo, useState } from 'react'

// ✅ Leaflet & React-Leaflet
import 'leaflet/dist/leaflet.css'
import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet'
import L from 'leaflet'

// ---- Force default marker icons to load under Vite ----
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'
const defaultIcon = L.icon({
  iconRetinaUrl: markerIcon2x,
  iconUrl: markerIcon,
  shadowUrl: markerShadow,
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
})
L.Marker.prototype.options.icon = defaultIcon
// ------------------------------------------

type RoadClosure = {
  id: number
  roadName: string
  status: 'OPEN' | 'PARTIAL' | 'CLOSED'
  note?: string
  updatedAt: string
  lat?: number
  lng?: number
}

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

// ---- Fit the map to markers ----
function FitToMarkers({ points }: { points: Array<[number, number]> }) {
  const map = useMap()
  useEffect(() => {
    if (!points.length) return
    const bounds = L.latLngBounds(points.map(([a, b]) => L.latLng(a, b)))
    map.fitBounds(bounds.pad(0.2), { animate: true })
  }, [points, map])
  return null
}

export default function App() {
  const [closures, setClosures] = useState<RoadClosure[]>([])
  const [status, setStatus] = useState('')
  const [error, setError] = useState<string | null>(null)

  // Cumberland County-ish fallback center
  const fallbackCenter: [number, number] = [35.0527, -78.8784]
  const markerPoints = useMemo(
    () =>
      closures
        .filter(c => typeof c.lat === 'number' && typeof c.lng === 'number')
        .map(c => [c.lat as number, c.lng as number]) as Array<[number, number]>,
    [closures]
  )

  useEffect(() => {
    setError(null)
    getClosures(status).then(setClosures).catch(e => setError(e.message))
  }, [status])

  return (
    <main style={{ maxWidth: 1100, margin: '0 auto', padding: 16, fontFamily: 'system-ui, sans-serif' }}>
      <h1 style={{ marginBottom: 12 }}>Cumberland Storm Status</h1>

      {/* ---- Closures List + Filter (map will be below this) ---- */}
      <section aria-labelledby="closures-heading" style={{ marginBottom: 24 }}>
        <h2 id="closures-heading">Road Closures</h2>

        <label>
          Filter:{' '}
          <select value={status} onChange={e => setStatus(e.target.value)}>
            <option value="">All</option>
            <option value="OPEN">OPEN</option>
            <option value="PARTIAL">PARTIAL</option>
            <option value="CLOSED">CLOSED</option>
          </select>
        </label>

        {error && <p style={{ color: 'crimson' }}>{error}</p>}

        {closures.length === 0 ? (
          <p>No records.</p>
        ) : (
          <ul>
            {closures.map(c => (
              <li key={c.id} style={{ margin: '8px 0' }}>
                <strong>{c.roadName}</strong> — {c.status}
                {c.note ? ` · ${c.note}` : ''}{' '}
                <small style={{ color: '#666' }}>
                  (updated {new Date(c.updatedAt).toLocaleString()})
                </small>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* ---- Map BELOW the list (no title) ---- */}
      <section style={{ marginBottom: 24 }}>
        <div style={{ height: 420, border: '1px solid #ddd', borderRadius: 8, overflow: 'hidden' }}>
          <MapContainer
            center={fallbackCenter}
            zoom={11}
            style={{ height: '100%', width: '100%' }}
            scrollWheelZoom
          >
            <TileLayer
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              attribution="&copy; OpenStreetMap contributors"
            />
            <FitToMarkers points={markerPoints} />
            {closures.map(c => {
              if (typeof c.lat !== 'number' || typeof c.lng !== 'number') return null
              return (
                <Marker key={c.id} position={[c.lat, c.lng]}>
                  <Popup>
                    <strong>{c.roadName}</strong>
                    <br />
                    Status: {c.status}
                    {c.note ? (
                      <>
                        <br />
                        Note: {c.note}
                      </>
                    ) : null}
                    <br />
                    <small>Updated {new Date(c.updatedAt).toLocaleString()}</small>
                  </Popup>
                </Marker>
              )
            })}
          </MapContainer>
        </div>
      </section>

      <hr style={{ margin: '24px 0' }} />

      {/* ---- Debris Form ---- */}
      <section aria-labelledby="debris-heading">
        <h2 id="debris-heading">Request Debris Pickup</h2>
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
      setFullName(''); setAddress(''); setEmail(''); setNotes('')
    } catch (err: any) {
      setError(err.message || 'Submit failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} style={{ display: 'grid', gap: 12, maxWidth: 600 }}>
      <label>
        Full name *
        <input value={fullName} onChange={e => setFullName(e.target.value)} required />
      </label>
      <label>
        Address *
        <input value={address} onChange={e => setAddress(e.target.value)} required />
      </label>
      <label>
        Email (optional)
        <input type="email" value={email} onChange={e => setEmail(e.target.value)} />
      </label>
      <label>
        Notes
        <textarea rows={3} value={notes} onChange={e => setNotes(e.target.value)} />
      </label>
      {error && <p style={{ color: 'crimson' }} role="alert">{error}</p>}
      <button type="submit" disabled={busy}>{busy ? 'Submitting…' : 'Submit'}</button>
    </form>
  )
}
