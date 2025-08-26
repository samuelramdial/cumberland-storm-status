import { useEffect, useMemo, useState } from 'react'
import './index.css'

/* Leaflet */
import 'leaflet/dist/leaflet.css'
import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet'
import L from 'leaflet'
import marker2x from 'leaflet/dist/images/marker-icon-2x.png'
import marker1x from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'

// fix default marker icons (Vite asset paths)
L.Icon.Default.mergeOptions({
  iconRetinaUrl: marker2x,
  iconUrl: marker1x,
  shadowUrl: markerShadow,
})

/* Types */
type ClosureStatus = 'OPEN' | 'PARTIAL' | 'CLOSED'
type RoadClosure = {
  id: number
  roadName: string
  status: ClosureStatus | string
  note?: string
  updatedAt: string
  lat?: number
  lng?: number
}

/* API helpers */
async function getClosures(status?: string) {
  const q = status ? `?status=${encodeURIComponent(status)}` : ''
  const r = await fetch(`/api/closures${q}`)
  if (!r.ok) throw new Error(await r.text())
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

/* Fit map to markers */
function FitBounds({ points }: { points: [number, number][] }) {
  const map = useMap()
  useEffect(() => {
    if (points.length > 0) {
      map.fitBounds(points, { padding: [28, 28] })
    }
  }, [points, map])
  return null
}

export default function App() {
  const [closures, setClosures] = useState<RoadClosure[]>([])
  const [status, setStatus] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setError(null)
    getClosures(status).then(setClosures).catch(e => setError(e.message || 'Failed to load closures'))
  }, [status])

  const points = useMemo(
    () =>
      closures
        .filter(c => typeof c.lat === 'number' && typeof c.lng === 'number')
        .map(c => [c.lat as number, c.lng as number] as [number, number]),
    [closures]
  )

  const center: [number, number] = points.length
    ? points[0]
    : ([35.0527, -78.8784] as [number, number]) // Fayetteville-ish fallback

  return (
    <main className="container">
      <h1 className="app-title">Cumberland Storm Status</h1>

      {/* Closures */}
      <section className="card" aria-labelledby="closures-heading">
        <div className="card-header">
          <h2 id="closures-heading">Road Closures</h2>
          <label className="filter">
            <span>Filter:</span>
            <select value={status} onChange={e => setStatus(e.target.value)}>
              <option value="">All</option>
              <option value="OPEN">OPEN</option>
              <option value="PARTIAL">PARTIAL</option>
              <option value="CLOSED">CLOSED</option>
            </select>
          </label>
        </div>

        {error && (
          <div className="alert error" role="alert">
            {error}
          </div>
        )}

        {closures.length === 0 ? (
          <p className="muted">No records.</p>
        ) : (
          <ul className="list">
            {closures.map(c => (
              <li key={c.id} className="list-row">
                <div className="row-main">
                  <div className="row-title">{c.roadName}</div>
                  <div className={`pill ${String(c.status).toLowerCase()}`}>{String(c.status)}</div>
                </div>
                <div className="row-sub">
                  {c.note ? <span className="note">{c.note}</span> : <span className="muted">No details</span>}
                  <span className="dot" />
                  <span className="muted">updated {new Date(c.updatedAt).toLocaleString()}</span>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* Map BELOW the list */}
      <section className="card">
        <div className="map-wrap" role="region" aria-label="Closures map">
          <MapContainer center={center} zoom={11} className="map-root" scrollWheelZoom>
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/">OpenStreetMap</a> contributors'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            {points.length > 0 && <FitBounds points={points} />}

            {closures
              .filter(c => typeof c.lat === 'number' && typeof c.lng === 'number')
              .map(c => (
                <Marker key={`m-${c.id}`} position={[c.lat as number, c.lng as number]}>
                  <Popup>
                    <strong>{c.roadName}</strong>
                    <div className="popup-line">
                      <span className={`pill tiny ${String(c.status).toLowerCase()}`}>{String(c.status)}</span>
                    </div>
                    {c.note && <div className="popup-line">{c.note}</div>}
                    <div className="popup-line muted">Updated {new Date(c.updatedAt).toLocaleString()}</div>
                  </Popup>
                </Marker>
              ))}
          </MapContainer>
        </div>
      </section>

      {/* Debris form */}
      <section className="card" aria-labelledby="debris-heading">
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
  const [success, setSuccess] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setSuccess(null)

    const nameOk = fullName.trim().length >= 2
    const addrOk = address.trim().length >= 5
    if (!nameOk || !addrOk) {
      setError('Please enter your full name and a valid address.')
      return
    }

    try {
      setBusy(true)
      const r = await createDebrisRequest({
        fullName: fullName.trim(),
        address: address.trim(),
        email: email.trim() || undefined,
        notes: notes.trim() || undefined,
      })
      onSubmitted(r.id)
      setSuccess(`Thanks! Your request was submitted. Ticket #${r.id}.`)
      setFullName('')
      setAddress('')
      setEmail('')
      setNotes('')
    } catch (err: any) {
      setError(err?.message || 'Submit failed. Please try again.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="form-card" onSubmit={submit} noValidate>
      {error && (
        <div className="alert error" role="alert" aria-live="polite">
          {error}
        </div>
      )}
      {success && (
        <div className="alert success" role="status" aria-live="polite">
          {success}
        </div>
      )}

      <div className="form-grid">
        <label className="field">
          <span className="label">
            Full name <span className="req">*</span>
          </span>
          <input
            className="input"
            value={fullName}
            onChange={e => setFullName(e.target.value)}
            placeholder="Jane Doe"
            required
            autoComplete="name"
          />
        </label>

        <label className="field">
          <span className="label">
            Email <span className="opt">(optional)</span>
          </span>
          <input
            className="input"
            type="email"
            value={email}
            onChange={e => setEmail(e.target.value)}
            placeholder="you@example.com"
            autoComplete="email"
            inputMode="email"
          />
        </label>

        <label className="field field-full">
          <span className="label">
            Address <span className="req">*</span>
          </span>
          <input
            className="input"
            value={address}
            onChange={e => setAddress(e.target.value)}
            placeholder="123 Main St, Fayetteville NC"
            required
            autoComplete="street-address"
          />
        </label>

        <label className="field field-full">
          <span className="label">Notes</span>
          <textarea
            className="textarea"
            rows={4}
            value={notes}
            onChange={e => setNotes(e.target.value)}
            placeholder="Anything we should know (downed limbs, location on property, gate code, etc.)"
          />
        </label>
      </div>

      <div className="form-actions">
        <button type="submit" className={`btn ${busy ? 'loading' : ''}`} disabled={busy}>
          {busy ? <span className="spinner" aria-hidden /> : null}
          {busy ? 'Submitting…' : 'Submit request'}
        </button>
      </div>
      <p className="fineprint">By submitting, you consent to be contacted about your pickup.</p>
    </form>
  )
}

