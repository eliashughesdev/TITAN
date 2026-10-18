# AI-2 - Configuracion de proveedores

## Principios

- Las credenciales permanecen en backend.
- No se escriben claves en SQL Server ni se devuelven al navegador.
- Para on-premise se recomiendan variables de entorno, User Secrets en desarrollo o un secret store del orquestador.
- La pantalla `/settings/ai-providers` requiere `settings.view` y solo muestra valores no secretos y health sanitizado.

## OpenRouter .NET

Seccion oficial: `AI:OpenRouter`.

| Variable de entorno .NET | Proposito | Default |
|---|---|---|
| `AI__OpenRouter__Enabled` | Activa el transporte OpenRouter | `true` |
| `AI__OpenRouter__ApiKey` | Credencial privada | vacia |
| `AI__OpenRouter__BaseUrl` | API base | `https://openrouter.ai/api/v1` |
| `AI__OpenRouter__AssistantModel` | Modelo Titan/assistant | usa `Model` |
| `AI__OpenRouter__Model` | Modelo general | `openrouter/free` |
| `AI__OpenRouter__ClassificationModel` | Clasificacion Helpdesk | usa `Model` |
| `AI__OpenRouter__AutomationModel` | Automatizaciones | usa `Model` |
| `AI__OpenRouter__TimeoutSeconds` | Timeout HTTP | `60` |
| `AI__OpenRouter__MaxRetries` | Reintentos despues del primer intento | `3` |
| `AI__OpenRouter__RetryBaseDelayMilliseconds` | Base de backoff | `750` |
| `AI__OpenRouter__EnableAssistant` | Habilita uso por Titan | `true` |

La convencion .NET anterior es equivalente a `OPENROUTER_API_KEY`, `OPENROUTER_MODEL` y `OPENROUTER_BASE_URL`, sin duplicar la configuracion oficial del API central.

El health de OpenRouter ejecuta `GET models` con bearer server-side y timeout de 10 segundos. Solo retorna disponible/no disponible, modelo, fecha, latencia y un detalle sanitizado.

## Ollama .NET

Seccion oficial: `Assistant:Ollama`.

| Variable | Proposito | Default |
|---|---|---|
| `Assistant__Ollama__Enabled` | Activa Ollama | `false` |
| `Assistant__Ollama__BaseUrl` | Endpoint local | `http://localhost:11434` |
| `Assistant__Ollama__Model` | Modelo local | `llama3.1` |
| `Assistant__Ollama__TimeoutSeconds` | Timeout | `90` |
| `Assistant__Ollama__MaxTokens` | Limite local configurado | `700` |

Estas variables son equivalentes a `OLLAMA_BASE_URL` y `OLLAMA_MODEL`. La comprobacion real usa `GET api/tags`; las conversaciones usan `POST api/chat` sin streaming.

## Politica Titan

| Variable | Descripcion | Default |
|---|---|---|
| `Assistant__Enabled` | Habilita el orquestador | `true` |
| `Assistant__Provider` | `Auto`, `OpenRouter` u `Ollama` | `Auto` |
| `Assistant__RequestTimeoutSeconds` | Timeout total del turno | `60` |
| `Assistant__MaxTokens` | Tokens por completion | `700` |
| `Assistant__MaxToolCallsPerTurn` | Limite de tools por turno | `4` |
| `Assistant__MaxHistoryMessages` | Contexto de conversacion | `20` |
| `Assistant__MaxTurnsPerWindow` | Cuota por usuario | `30` |
| `Assistant__QuotaWindowSeconds` | Ventana de cuota | `60` |
| `Assistant__MaxActiveSessionsPerUser` | Sesiones en memoria | `20` |

En `Auto`, el registry prueba OpenRouter y despues Ollama. La indisponibilidad se informa en el chat; no se inventa respuesta. La cancelacion del navegador se propaga al request y el timeout del orquestador cancela el turno.

## Fiorella FastAPI

La integracion Ponches conserva su convencion existente:

| Variable | Descripcion |
|---|---|
| `OPENROUTER_API_KEY` | Credencial de Fiorella en FastAPI |
| `OPENROUTER_BASE_URL` | URL OpenRouter compatible OpenAI |
| `FIORELLA_MODEL` | Modelo especializado |
| `TITAN_PONCHES_INTEGRATION_KEY` | Clave maquina .NET -> FastAPI, minimo 32 caracteres |

La misma clave de integracion debe configurarse en la seccion `Ponches` del API .NET mediante el mecanismo on-premise seguro existente. Nunca debe prefijarse con `VITE_`.

## Ejemplo PowerShell de desarrollo

```powershell
$env:AI__OpenRouter__ApiKey = '<secret>'
$env:AI__OpenRouter__AssistantModel = 'openrouter/free'
$env:Assistant__Provider = 'Auto'
$env:Assistant__Ollama__Enabled = 'true'
$env:Assistant__Ollama__BaseUrl = 'http://localhost:11434'
$env:Assistant__Ollama__Model = 'gemma4:26b'
```

No colocar valores reales en archivos versionados.

## Google Maps

`VITE_GOOGLE_MAPS_API_KEY` es una clave publica de navegador, no una credencial de servidor. Debe restringirse por origen y limitarse a las APIs necesarias. Google Cloud, billing y cuotas deben configurarse explicitamente.

## Limitacion administrativa actual

La consola IA implementada verifica conexion y muestra politica efectiva. No permite escribir secretos ni persiste cambios de proveedor/modelo/timeout. Habilitar edicion requiere un almacenamiento SQL de configuracion no secreta, auditoria, recarga dinamica y una integracion separada con secret store; queda pendiente para no simular persistencia ni guardar claves en texto plano.
