import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'

import { useAuth } from '../../../auth/AuthContext'

import {
  describeTitanFailure,
} from '../../../assistant/api/titanAssistantApi'

import type { ChatMessage } from './Fiorella'
import type { FiorellaAction } from './types'
import { fiorellaApi } from './fiorellaApi'

/*
 * Fiorella dentro de TitanMDM habla con el gateway empresarial
 * real (`/api/ponches/fiorella/chat`). El backend .NET valida la
 * identidad y delega solo las operaciones de Ponches autorizadas
 * al agente FastAPI; el navegador nunca conoce la clave interna.
 */

const FIORELLA_CHAT_IDLE_MS = 30 * 60 * 1000
const FIORELLA_MAX_LOCAL_MESSAGES = 30

type ActiveUser = {
  id: string
  name: string
}

type UseFiorellaChatOptions = {
  getActiveModule: () => string
  onAnimation: (action: FiorellaAction) => void
}

function normalizeAnimation(value: string): FiorellaAction {
  const allowed: FiorellaAction[] = [
    'idle',
    'walk',
    'jump',
    'point',
    'wave',
    'celebrate',
    'surprised',
    'sleep',
    'think',
    'alert',
    'peek',
    'hide',
  ]

  return allowed.includes(value as FiorellaAction)
    ? value as FiorellaAction
    : 'point'
}

function buildGreeting(
  user: ActiveUser,
  activeModule: string,
): ChatMessage {
  return {
    id: `init-${Date.now()}`,
    sender: 'fiorella',
    text:
      `¡Hola ${user.name}! ` +
      'Estoy lista para ayudarte. ' +
      `Estás en ${activeModule}.`,
  }
}

const sessionKey = (id: string) =>
  `fiorella_session_${id}`

const activityKey = (id: string) =>
  `fiorella_last_activity_${id}`

const conversationKey = (id: string) =>
  `fiorella_conversation_${id}`

export function useFiorellaChat({
  getActiveModule,
  onAnimation,
}: UseFiorellaChatOptions) {
  const { user } = useAuth()

  const currentUser = useMemo<ActiveUser | null>(
    () =>
      user
        ? {
            id: user.id,
            name:
              `${user.firstName} ${user.lastName}`.trim() ||
              user.email,
          }
        : null,
    [user],
  )

  const [messages, setMessages] =
    useState<ChatMessage[]>([])

  const [loading, setLoading] =
    useState(false)

  const [connection, setConnection] =
    useState<'unknown' | 'connected' | 'error'>('unknown')

  const [provider, setProvider] =
    useState('Ponches')

  const [conversationId, setConversationId] =
    useState<string | null>(null)

  const chatIdleTimer =
    useRef<number | null>(null)

  const requestController =
    useRef<AbortController | null>(null)

  const cancelIdleTimer = useCallback(() => {
    if (chatIdleTimer.current !== null) {
      window.clearTimeout(chatIdleTimer.current)
      chatIdleTimer.current = null
    }
  }, [])

  const removeStoredConversation = useCallback(
    (userId: string) => {
      localStorage.removeItem(sessionKey(userId))
      localStorage.removeItem(activityKey(userId))
      localStorage.removeItem(conversationKey(userId))
    },
    [],
  )

  const clearChat = useCallback(() => {
    requestController.current?.abort()
    requestController.current = null
    cancelIdleTimer()

    if (currentUser) {
      removeStoredConversation(currentUser.id)

      setMessages([
        buildGreeting(
          currentUser,
          getActiveModule(),
        ),
      ])
    } else {
      setMessages([])
    }

    setConversationId(null)
    setLoading(false)
    onAnimation('idle')
  }, [
    cancelIdleTimer,
    currentUser,
    getActiveModule,
    onAnimation,
    removeStoredConversation,
  ])

  const cancelRequest = useCallback(() => {
    requestController.current?.abort()
    requestController.current = null
  }, [])

  useEffect(
    () => () => requestController.current?.abort(),
    [],
  )

  const scheduleIdleCleanup = useCallback(
    (activeUser: ActiveUser) => {
      cancelIdleTimer()

      localStorage.setItem(
        activityKey(activeUser.id),
        String(Date.now()),
      )

      chatIdleTimer.current = window.setTimeout(
        () => {
          removeStoredConversation(activeUser.id)
          setConversationId(null)
          setLoading(false)

          setMessages([
            buildGreeting(
              activeUser,
              getActiveModule(),
            ),
          ])

          onAnimation('idle')
          chatIdleTimer.current = null
        },
        FIORELLA_CHAT_IDLE_MS,
      )
    },
    [
      cancelIdleTimer,
      getActiveModule,
      onAnimation,
      removeStoredConversation,
    ],
  )

  useEffect(() => {
    if (!currentUser) {
      return
    }

    const storedHistory = localStorage.getItem(
      sessionKey(currentUser.id),
    )

    const lastActivityRaw = localStorage.getItem(
      activityKey(currentUser.id),
    )

    const lastActivity = lastActivityRaw
      ? Number(lastActivityRaw)
      : 0

    const sessionExpired =
      lastActivity > 0 &&
      Date.now() - lastActivity >=
        FIORELLA_CHAT_IDLE_MS

    let parsedMessages: ChatMessage[] = []

    if (storedHistory && !sessionExpired) {
      try {
        const parsed = JSON.parse(storedHistory)

        if (Array.isArray(parsed)) {
          parsedMessages = parsed
        }
      } catch {
        parsedMessages = []
      }
    }

    if (parsedMessages.length > 0) {
      setMessages(parsedMessages)
    } else {
      if (sessionExpired) {
        removeStoredConversation(currentUser.id)
      }

      setMessages([
        buildGreeting(
          currentUser,
          getActiveModule(),
        ),
      ])
    }

    const storedConversation = localStorage.getItem(
      conversationKey(currentUser.id),
    )

    if (storedConversation && !sessionExpired) {
      setConversationId(storedConversation)
    }

    if (!sessionExpired) {
      scheduleIdleCleanup(currentUser)
    }

    return cancelIdleTimer
  }, [
    cancelIdleTimer,
    currentUser,
    getActiveModule,
    removeStoredConversation,
    scheduleIdleCleanup,
  ])

  useEffect(() => {
    if (!currentUser || messages.length === 0) {
      return
    }

    localStorage.setItem(
      sessionKey(currentUser.id),
      JSON.stringify(
        messages.slice(-FIORELLA_MAX_LOCAL_MESSAGES),
      ),
    )
  }, [currentUser, messages])

  useEffect(() => {
    if (!currentUser) return

    const key = conversationKey(currentUser.id)

    if (conversationId) {
      localStorage.setItem(key, conversationId)
    } else {
      localStorage.removeItem(key)
    }
  }, [conversationId, currentUser])

  const sendMessage = useCallback(
    async (text: string) => {
      const trimmed = text.trim()

      if (!trimmed || loading) {
        return
      }

      if (!currentUser) {
        setMessages((previous) => [
          ...previous,
          {
            id: `${Date.now()}-auth-error`,
            sender: 'fiorella',
            text:
              'Tu sesión no está disponible. ' +
              'Inicia sesión nuevamente.',
          },
        ])

        onAnimation('alert')
        return
      }

      scheduleIdleCleanup(currentUser)

      const userMessage: ChatMessage = {
        id: `${Date.now()}-user`,
        sender: 'user',
        text: trimmed,
      }

      setMessages((previous) => [
        ...previous,
        userMessage,
      ])

      setLoading(true)
      onAnimation('think')

      const controller = new AbortController()
      requestController.current = controller

      try {
        const response =
          await fiorellaApi.sendMessage({
            message: trimmed,
            activeModule: getActiveModule(),
            conversationId,
          }, controller.signal)

        setConnection('connected')
        setProvider(
          response.model
            ? `${response.provider} · ${response.model}`
            : response.provider,
        )

        if (response.conversationId) {
          setConversationId(response.conversationId)
        }

        const responseText =
          response.answer.trim()
            ? response.answer
            : 'Fiorella respondió sin contenido.'

        setMessages((previous) => [
          ...previous,
          {
            id: `${Date.now()}-fiorella`,
            sender: 'fiorella',
            text: responseText,
          },
        ])

        onAnimation(normalizeAnimation(response.animation))
      } catch (error) {
        const failure = describeTitanFailure(error)

        if (failure.kind !== 'cancelled') {
          setConnection('error')
        }

        setMessages((previous) => [
          ...previous,
          {
            id: `${Date.now()}-error`,
            sender: 'fiorella',
            text: failure.message,
          },
        ])

        onAnimation('alert')
      } finally {
        if (requestController.current === controller) {
          requestController.current = null
        }
        setLoading(false)
      }
    },
    [
      conversationId,
      currentUser,
      getActiveModule,
      loading,
      onAnimation,
      scheduleIdleCleanup,
    ],
  )

  return {
    currentUser,
    messages,
    loading,
    connection,
    provider,
    conversationId,
    clearChat,
    cancelRequest,
    sendMessage,
  }
}
