import {
  X,
} from 'lucide-react'

interface TitanSpeechBubbleProps {
  message: string

  onClose: () => void
}

export function TitanSpeechBubble({
  message,
  onClose,
}: TitanSpeechBubbleProps) {
  return (
    <div
      className="titan-speech"
      role="status"
      aria-live="polite"
    >
      <div className="titan-speech__content">
        {message}
      </div>

      <button
        type="button"
        className="titan-speech__close"
        aria-label="Cerrar comentario"
        onClick={onClose}
      >
        <X size={13} />
      </button>
    </div>
  )
}