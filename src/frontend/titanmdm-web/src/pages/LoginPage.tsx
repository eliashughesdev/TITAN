import {
  useEffect,
  useState,
  type FormEvent,
} from 'react'

import axios from 'axios'

import {
  Navigate,
  useLocation,
  useNavigate,
} from 'react-router-dom'

import {
  Eye,
  EyeOff,
  LockKeyhole,
  Monitor,
  ShieldCheck,
  Smartphone,
} from 'lucide-react'

import { useAuth } from '../auth/AuthContext'

import {
  TitanLoginRobot,
  type TitanLoginRobotMode,
} from './login/TitanLoginRobot'

import './LoginPage.css'

interface LocationState {
  from?: string
}

export function LoginPage() {
  const {
    login,
    isAuthenticated,
  } = useAuth()

  const navigate = useNavigate()
  const location = useLocation()

  const [email, setEmail] =
    useState('')

  const [password, setPassword] =
    useState('')

  const [
    showPassword,
    setShowPassword,
  ] = useState(false)

  const [error, setError] =
    useState('')

  const [isSubmitting, setIsSubmitting] =
    useState(false)

  const [
    robotMode,
    setRobotMode,
  ] =
    useState<TitanLoginRobotMode>(
      'idle',
    )

  const [
    transitionPhase,
    setTransitionPhase,
  ] = useState(false)

  const [
    postLoginAnimation,
    setPostLoginAnimation,
  ] = useState(false)

  useEffect(() => {
    document.title =
      'Iniciar sesión | TitanMDM'
  }, [])

  const redirectTo =
    (
      location.state as
        | LocationState
        | null
    )?.from || '/'

  if (
    isAuthenticated &&
    !postLoginAnimation
  ) {
    return (
      <Navigate
        to={redirectTo}
        replace
      />
    )
  }

  function backToIdle() {
    if (
      !isSubmitting &&
      !transitionPhase
    ) {
      setRobotMode('idle')
    }
  }

  const handleSubmit =
    async (
      event: FormEvent<HTMLFormElement>,
    ) => {
      event.preventDefault()

      setError('')

      const normalizedEmail =
        email
          .trim()
          .toLowerCase()

      if (
        !normalizedEmail ||
        !password
      ) {
        setError(
          'Ingresa tu correo electrónico y contraseña.',
        )

        setRobotMode('error')

        window.setTimeout(() => {
          if (
            !isSubmitting &&
            !transitionPhase
          ) {
            setRobotMode('idle')
          }
        }, 1300)

        return
      }

      setIsSubmitting(true)
      setRobotMode('authenticating')

      try {
        await login({
          email: normalizedEmail,
          password,
        })

        setPostLoginAnimation(true)
        setRobotMode('success')

        window.setTimeout(() => {
          setTransitionPhase(true)
          setRobotMode('launch')
        }, 620)

        window.setTimeout(() => {
          navigate(
            redirectTo,
            {
              replace: true,
            },
          )
        }, 1850)
      } catch (requestError) {
        if (
          axios.isAxiosError(
            requestError,
          )
        ) {
          if (
            requestError.response
              ?.status === 401
          ) {
            setError(
              'Correo electrónico o contraseña incorrectos.',
            )
          } else if (
            !requestError.response
          ) {
            setError(
              'No fue posible conectar con el servidor TitanMDM.',
            )
          } else {
            setError(
              'No fue posible iniciar sesión. Inténtalo nuevamente.',
            )
          }
        } else {
          setError(
            'Ocurrió un error inesperado.',
          )
        }

        setIsSubmitting(false)
        setRobotMode('error')

        window.setTimeout(() => {
          if (
            !transitionPhase
          ) {
            setRobotMode('idle')
          }
        }, 1800)
      }
    }

  return (
    <main
      className={
        `login-page ${
          transitionPhase
            ? 'login-page--transitioning'
            : ''
        }`
      }
    >
      <section className="login-brand">
        <div className="login-brand__content">
          <div className="brand">
            <div className="brand__mark">
              T
            </div>

            <div>
              <strong>
                TitanMDM
              </strong>

              <span>
                Enterprise
              </span>
            </div>
          </div>

          <div className="login-brand__hero">
            <span className="eyebrow">
              Unified Endpoint Management
            </span>

            <h1>
              Control empresarial.
              <br />
              Seguridad centralizada.
            </h1>

            <p>
              Administra dispositivos Android y Windows
              desde una plataforma segura, centralizada
              y preparada para crecer.
            </p>

            <div className="platforms">
              <div>
                <Smartphone size={20} />
                Android
              </div>

              <div>
                <Monitor size={20} />
                Windows
              </div>

              <div>
                <ShieldCheck size={20} />
                Seguridad
              </div>
            </div>
          </div>

          <div className="login-brand__footer">
            TitanMDM Enterprise
          </div>
        </div>
      </section>

      <section className="login-panel">
        <div className="login-scene">
          <div className="login-card-scene">
            <TitanLoginRobot
              mode={robotMode}
              emailValue={email}
              showPassword={showPassword}
            />

            <div className="login-card">
              <div className="login-card__mobile-brand">
                <div className="brand__mark">
                  T
                </div>

                <div>
                  <strong>
                    TitanMDM
                  </strong>

                  <span>
                    Enterprise
                  </span>
                </div>
              </div>

              <div className="login-card__heading">
                <div className="login-icon">
                  <LockKeyhole size={23} />
                </div>

                <h2>
                  Bienvenido
                </h2>

                <p>
                  Ingresa tus credenciales para acceder a la consola.
                </p>
              </div>

              <form
                onSubmit={handleSubmit}
                className="login-form"
              >
                <label>
                  Correo electrónico

                  <input
                    type="email"
                    value={email}
                    autoComplete="username"
                    placeholder="usuario@empresa.com"
                    disabled={
                      isSubmitting ||
                      transitionPhase
                    }
                    onFocus={() =>
                      setRobotMode('email')
                    }
                    onBlur={backToIdle}
                    onChange={(event) =>
                      setEmail(
                        event.target.value,
                      )
                    }
                  />
                </label>

                <label>
                  Contraseña

                  <div className="password-field">
                    <input
                      type={
                        showPassword
                          ? 'text'
                          : 'password'
                      }
                      value={password}
                      autoComplete="current-password"
                      placeholder="Ingresa tu contraseña"
                      disabled={
                        isSubmitting ||
                        transitionPhase
                      }
                      onFocus={() =>
                        setRobotMode('password')
                      }
                      onBlur={backToIdle}
                      onChange={(event) =>
                        setPassword(
                          event.target.value,
                        )
                      }
                    />

                    <button
                      type="button"
                      className="password-toggle"
                      aria-label={
                        showPassword
                          ? 'Ocultar contraseña'
                          : 'Mostrar contraseña'
                      }
                      disabled={
                        isSubmitting ||
                        transitionPhase
                      }
                      onClick={() => {
                        setShowPassword(
                          current => !current,
                        )

                        setRobotMode('password')
                      }}
                    >
                      {showPassword ? (
                        <EyeOff size={19} />
                      ) : (
                        <Eye size={19} />
                      )}
                    </button>
                  </div>
                </label>

                {error && (
                  <div
                    className="login-error"
                    role="alert"
                  >
                    {error}
                  </div>
                )}

                <button
                  type="submit"
                  className="login-submit"
                  disabled={
                    isSubmitting ||
                    transitionPhase
                  }
                >
                  {isSubmitting
                    ? 'Verificando...'
                    : 'Iniciar sesión'}
                </button>
              </form>
              <a
  href="/api/auth/entra/start"
  className="login-submit"
  style={{
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 12,
    textDecoration: 'none',
  }}
>
  Iniciar sesión con Microsoft
</a>

              <div className="login-security">
                <ShieldCheck size={17} />

                <span>
                  Acceso protegido por TitanMDM Security
                </span>
              </div>
            </div>

            {transitionPhase && (
  <div className="login-titan-transition">
    <div className="login-titan-transition__grid" />

    <div className="login-titan-transition__beam" />

    <div className="login-titan-transition__ring login-titan-transition__ring--1" />
    <div className="login-titan-transition__ring login-titan-transition__ring--2" />
    <div className="login-titan-transition__ring login-titan-transition__ring--3" />

    <div className="login-titan-transition__content">
      <div className="login-titan-transition__mark">
        T
      </div>

      <span>
        TITANMDM
      </span>

      <h2>
        Acceso autorizado
      </h2>

      <p>
        Inicializando consola empresarial...
      </p>

      <div className="login-titan-transition__progress">
        <span />
      </div>
    </div>
  </div>
)}
          </div>
        </div>
      </section>
    </main>
  )
}