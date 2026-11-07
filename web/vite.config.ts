import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  define: {
    // デバッグパネルに出すビルド日時
    'import.meta.env.VITE_BUILD_TIME': JSON.stringify(new Date().toLocaleString('ja-JP', { timeZone: 'Asia/Tokyo' })),
  },
})
