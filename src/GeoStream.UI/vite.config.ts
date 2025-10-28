import { defineConfig } from 'vite';
import aurelia from '@aurelia/vite-plugin';

export default defineConfig({
  plugins: [
    aurelia()
  ],
  server: {
    port: 5173,
    host: '0.0.0.0',
    proxy: {
      '/api': {
        target: 'http://api:5000',
        changeOrigin: true
      },
      '/hubs': {
        target: 'http://api:5000',
        changeOrigin: true,
        ws: true
      }
    }
  }
});
