import {
  describe,
  expect,
  it,
} from 'vitest'

import {
  render,
  screen,
} from '@testing-library/react'

import {
  EmailContent,
} from './EmailContent'

import {
  hasHtmlMarkup,
  resolveCidReferences,
  sanitizeEmailHtml,
} from './emailHtml'

describe('emailContent', () => {
  it('elimina scripts y manejadores de eventos', () => {
    const clean =
      sanitizeEmailHtml(
        '<p onclick="alert(1)">Hola' +
          '<script>alert(2)</script></p>',
      )

    expect(clean).toContain('Hola')

    expect(
      clean.toLowerCase(),
    ).not.toContain('<script')

    expect(
      clean.toLowerCase(),
    ).not.toContain('onclick')
  })

  it('conserva las imágenes inline con cid', () => {
    const clean =
      sanitizeEmailHtml(
        '<img src="cid:logo@firma" alt="firma">',
      )

    expect(clean).toContain(
      'cid:logo@firma',
    )
  })

  it('elimina el esquema javascript de los enlaces', () => {
    const clean =
      sanitizeEmailHtml(
        '<a href="javascript:alert(1)">x</a>',
      )

    expect(
      clean.toLowerCase(),
    ).not.toContain('javascript:')
  })

  it('resuelve cid contra el mapa de adjuntos', () => {
    const resolved =
      resolveCidReferences(
        '<img src="cid:logo@firma">',
        (contentId) =>
          contentId ===
          'logo@firma'
            ? '/api/att/1'
            : undefined,
      )

    expect(resolved).toContain(
      '/api/att/1',
    )

    expect(resolved).not.toContain(
      'cid:',
    )
  })

  it('detecta la presencia de markup HTML', () => {
    expect(
      hasHtmlMarkup('<b>x</b>'),
    ).toBe(true)

    expect(
      hasHtmlMarkup(
        'a < b y c > d',
      ),
    ).toBe(false)
  })
})

describe('EmailContent', () => {
  it('renderiza texto plano preservando contenido', () => {
    render(
      <EmailContent
        text={'linea1\nlinea2'}
      />,
    )

    expect(
      screen.getByText(/linea1/),
    ).toBeInTheDocument()
  })

  it('renderiza HTML saneado', () => {
    render(
      <EmailContent
        text=""
        html="<p>Hola <b>mundo</b></p>"
      />,
    )

    expect(
      screen.getByText(/mundo/),
    ).toBeInTheDocument()
  })

  it('no inyecta scripts del HTML de correo', () => {
    const { container } =
      render(
        <EmailContent
          text=""
          html="<p>Ok</p><script>window.x=1</script>"
        />,
      )

    expect(
      container.querySelector(
        'script',
      ),
    ).toBeNull()
  })

  it('elimina imágenes cid sin resolver', () => {
    const { container } =
      render(
        <EmailContent
          text=""
          html="<p>Ok</p><img src='cid:missing'>"
        />,
      )

    expect(
      container.querySelector('img'),
    ).toBeNull()

    expect(
      screen.getByText('Ok'),
    ).toBeInTheDocument()
  })

  it('resuelve imágenes inline mediante cidUrls', () => {
    const { container } =
      render(
        <EmailContent
          text=""
          html="<img src='cid:logo@firma'>"
          cidUrls={{
            'logo@firma': '/api/att/1',
          }}
        />,
      )

    const image =
      container.querySelector('img')

    expect(
      image?.getAttribute('src'),
    ).toBe('/api/att/1')
  })
})
