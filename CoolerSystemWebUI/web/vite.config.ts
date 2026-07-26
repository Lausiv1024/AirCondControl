import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

// ビルド成果物は ASP.NET (CoolerSystemWebApi) の wwwroot に直接出力し、同一オリジンで配信する。
// 開発時は vite dev server から /api を ASP.NET (5080) にプロキシするので、本番と同じ相対パスで書ける。
export default defineConfig({
  plugins: [svelte()],
  build: {
    outDir: '../server/CoolerSystemWebApi/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
});
