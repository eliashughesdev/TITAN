import { useCallback, useEffect, useRef, useState } from 'react'

type SignatureData = {
  base64: string
  width: number
  height: number
}

export function SignaturePad({
  width = 420,
  height = 150,
  onChange,
}: {
  width?: number
  height?: number
  onChange?: (data: SignatureData) => void
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const drawing = useRef(false)
  const [hasSignature, setHasSignature] = useState(false)

  const emit = useCallback(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const dataUrl = canvas.toDataURL('image/png')
    setHasSignature(true)
    onChange?.({ base64: dataUrl.split(',')[1] ?? '', width, height })
  }, [height, onChange, width])

  const clear = useCallback(() => {
    const canvas = canvasRef.current
    const ctx = canvas?.getContext('2d')
    if (!canvas || !ctx) return
    ctx.fillStyle = '#ffffff'
    ctx.fillRect(0, 0, canvas.width, canvas.height)
    setHasSignature(false)
    onChange?.({ base64: '', width, height })
  }, [height, onChange, width])

  useEffect(() => {
    const canvas = canvasRef.current
    const ctx = canvas?.getContext('2d')
    if (!canvas || !ctx) return

    canvas.width = width
    canvas.height = height
    ctx.fillStyle = '#ffffff'
    ctx.fillRect(0, 0, width, height)
    ctx.lineCap = 'round'
    ctx.lineJoin = 'round'
    ctx.strokeStyle = '#111827'
    ctx.lineWidth = 2

    const point = (event: PointerEvent) => {
      const rect = canvas.getBoundingClientRect()
      return {
        x: ((event.clientX - rect.left) / rect.width) * canvas.width,
        y: ((event.clientY - rect.top) / rect.height) * canvas.height,
      }
    }

    const down = (event: PointerEvent) => {
      event.preventDefault()
      canvas.setPointerCapture(event.pointerId)
      drawing.current = true
      const p = point(event)
      ctx.beginPath()
      ctx.moveTo(p.x, p.y)
    }

    const move = (event: PointerEvent) => {
      if (!drawing.current) return
      event.preventDefault()
      const p = point(event)
      ctx.lineTo(p.x, p.y)
      ctx.stroke()
    }

    const up = (event: PointerEvent) => {
      if (!drawing.current) return
      drawing.current = false
      try {
        canvas.releasePointerCapture(event.pointerId)
      } catch {
        // ignore
      }
      emit()
    }

    canvas.addEventListener('pointerdown', down)
    canvas.addEventListener('pointermove', move)
    canvas.addEventListener('pointerup', up)
    canvas.addEventListener('pointerleave', up)

    return () => {
      canvas.removeEventListener('pointerdown', down)
      canvas.removeEventListener('pointermove', move)
      canvas.removeEventListener('pointerup', up)
      canvas.removeEventListener('pointerleave', up)
    }
  }, [emit, height, width])

  return (
    <div className="signature-pad">
      <canvas
        ref={canvasRef}
        width={width}
        height={height}
        style={{ background: '#fff', display: 'block', width: '100%', touchAction: 'none' }}
      />
      <div className="signature-pad__actions">
        <button type="button" className="inventory-btn" onClick={clear}>
          Limpiar
        </button>
        <span className="inventory-muted">
          {hasSignature ? 'Firma capturada' : 'Firme dentro del recuadro'}
        </span>
      </div>
    </div>
  )
}
