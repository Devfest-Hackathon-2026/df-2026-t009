import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './theme-holo.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)

// 本番の Web 版だけ Service Worker を登録する（ホーム画面に追加して、アプリとして使えるように）。
// Android アプリ版は中にファイルを持っているので不要。
const isNativeApp = Boolean((window as unknown as { Capacitor?: { isNativePlatform?: () => boolean } }).Capacitor?.isNativePlatform?.())
if (import.meta.env.PROD && !isNativeApp && 'serviceWorker' in navigator) {
  // 新しい版の Service Worker に切り替わったら、1回だけ読み直して最新の画面にする
  let reloaded = false
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (reloaded) return
    reloaded = true
    location.reload()
  })
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      /* 登録できなくても、普通の Web ページとして動く */
    })
  })
}
