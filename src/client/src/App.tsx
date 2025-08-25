import { useEffect, useState } from 'react'

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

export default function App() {
  const [closures, setClosures] = useState<RoadClosure[]>([])
  const [status, setStatus] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setError(null)
    getClosures(status).then(setClosures).catch(e => setError(e.message))
  }, [status])

  return (
    <main style={{ maxWidth: 900, margin: '0 auto', padding: 16 }}>
      <h1>Cumberland Storm Status</h1>

      <section aria-labelledby="closures-heading">
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

      <hr style={{ margin: '24px 0' }} />

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
      const r = await createDebrisRequest({ fullName, address, email: email || undefined, notes: notes || undefined })
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
