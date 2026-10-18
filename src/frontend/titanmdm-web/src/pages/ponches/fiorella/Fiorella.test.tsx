import {
  fireEvent,
  render,
  screen,
} from '@testing-library/react'

import {
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import { Fiorella } from './Fiorella'

describe('Fiorella', () => {
  it('muestra el globo de diálogo cuando hay línea', () => {
    render(
      <Fiorella line="Hola, estoy lista." />,
    )

    expect(
      screen.getByText('Hola, estoy lista.'),
    ).toBeInTheDocument()
  })

  it('no muestra mensajes cuando el chat está vacío', () => {
    render(
      <Fiorella
        isChatOpen
        messages={[]}
        activeModule="/ponches"
      />,
    )

    expect(
      screen.getByText('Sin mensajes en esta sesión.'),
    ).toBeInTheDocument()
  })

  it('renderiza los mensajes del usuario y de Fiorella', () => {
    render(
      <Fiorella
        isChatOpen
        activeModule="/ponches"
        messages={[
          {
            id: '1',
            sender: 'user',
            text: 'Hola Fiorella',
          },
          {
            id: '2',
            sender: 'fiorella',
            text: 'Estoy aquí para ayudarte.',
          },
        ]}
      />,
    )

    expect(
      screen.getByText('Hola Fiorella'),
    ).toBeInTheDocument()

    expect(
      screen.getByText('Estoy aquí para ayudarte.'),
    ).toBeInTheDocument()
  })

  it('notifica el clic sobre el sprite', () => {
    const onSpriteClick = vi.fn()

    render(
      <Fiorella
        line="Hola"
        onSpriteClick={onSpriteClick}
      />,
    )

    fireEvent.click(
      screen.getByTitle('Hablar con Fiorella'),
    )

    expect(onSpriteClick).toHaveBeenCalledTimes(1)
  })

  it('permite enviar un mensaje desde el chat', () => {
    const onSendMessage = vi.fn()

    render(
      <Fiorella
        isChatOpen
        activeModule="/ponches"
        onSendMessage={onSendMessage}
      />,
    )

    fireEvent.change(
      screen.getByPlaceholderText(
        'Escribe un comando...',
      ),
      { target: { value: '  estado de relojes  ' } },
    )

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Enviar',
      }),
    )

    expect(onSendMessage).toHaveBeenCalledWith(
      'estado de relojes',
    )
  })
})
