import React from 'react'
import { createRoot } from 'react-dom/client'

// ✅ Load global styles and Leaflet's CSS
import './index.css'
import 'leaflet/dist/leaflet.css'

import App from './App'

const rootEl = document.getElementById('root')!
createRoot(rootEl).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
)
