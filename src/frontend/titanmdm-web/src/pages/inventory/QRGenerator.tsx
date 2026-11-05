import { useEffect, useState } from 'react'
import * as QRCode from 'qrcode'

export function QRGenerator({
  assetTag,
  assetId,
}: {
  assetTag: string
  assetId: string
}) {
  const payload = `CISA-TI|${assetTag}|${assetId}`
  const [dataUrl, setDataUrl] = useState<string>('')
  const [error, setError] = useState<string>('')

  useEffect(() => {
    let active = true
    QRCode.toDataURL(payload, {
      width: 320,
      margin: 2,
      errorCorrectionLevel: 'M',
      color: { dark: '#0f172a', light: '#ffffff' },
    })
      .then(url => {
        if (active) {
          setDataUrl(url)
          setError('')
        }
      })
      .catch(err => {
        if (active) setError(String(err))
      })

    return () => {
      active = false
    }
  }, [payload])

  const download = () => {
    if (!dataUrl) return
    const link = document.createElement('a')
    link.href = dataUrl
    link.download = `qr-${assetTag}.png`
    link.click()
  }

  return (
    <div className="qr-generator">
      <h3>Código QR del activo</h3>
      <p>
        Payload: <code className="code-block">{payload}</code>
      </p>

      {error && <p className="inventory-muted">No se pudo generar el QR: {error}</p>}

      {dataUrl && (
        <div className="qr-preview">
          <img src={dataUrl} alt={`QR ${assetTag}`} style={{ maxWidth: 260, height: 'auto' }} />
          <button type="button" className="inventory-btn inventory-btn--primary" onClick={download}>
            Descargar PNG
          </button>
        </div>
      )}
    </div>
  )
}
