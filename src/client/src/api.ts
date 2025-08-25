export type RoadClosure = {
  id: number
  roadName: string
  status: 'OPEN' | 'PARTIAL' | 'CLOSED'
  note?: string
  updatedAt: string
  lat?: number
  lng?: number
}

export async function getClosures(status?: string) {
  const q = status ? `?status=${encodeURIComponent(status)}` : ''
  const r = await fetch(`/api/closures${q}`)
  if (!r.ok) throw new Error('Failed to load closures')
  return (await r.json()) as RoadClosure[]
}

export async function createDebrisRequest(body: {
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
  if (!r.ok) {
    const text = await r.text()
    throw new Error(text || 'Submit failed')
  }
  return (await r.json()) as { id: number }
}
