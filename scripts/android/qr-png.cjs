// Genera el PNG del QR de aprovisionamiento a partir del JSON exacto,
// sin pasar por generadores externos (que alteran comillas o espacios).
// Uso: node qr-png.cjs <provisioning-qr.json> <salida.png>
'use strict'

const fs = require('node:fs')
const path = require('node:path')
const { createRequire } = require('node:module')

const [, , input, output] = process.argv
if (!input || !output) {
  console.error('Uso: node qr-png.cjs <provisioning-qr.json> <salida.png>')
  process.exit(2)
}

const web = path.resolve(__dirname, '../../src/frontend/titanmdm-web/package.json')
const QRCode = createRequire(web)('qrcode')

const text = fs.readFileSync(input, 'utf8').replace(/^﻿/, '').trim()
JSON.parse(text) // debe ser JSON válido
if (/[\r\n]/.test(text)) throw new Error('El JSON debe estar en una sola línea.')

QRCode.toFile(output, text, { errorCorrectionLevel: 'M', margin: 4, width: 1024 }, error => {
  if (error) {
    console.error(error.message)
    process.exit(1)
  }
  console.log(`PNG del QR: ${output} (${text.length} caracteres)`)
})
