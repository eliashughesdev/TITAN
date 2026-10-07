import type {
  TitanPageContext,
  TitanUserContext,
} from '../context/TitanAssistantContext'

interface ContextualMessageInput {
  user:
    TitanUserContext

  page:
    TitanPageContext
}

export function getTitanContextualGreeting({
  user,
  page,
}: ContextualMessageInput):
  string {
  const name =
    user.firstName ||
    user.fullName ||
    'administrador'

  switch (page.module) {
    case 'dashboard':
      return `${name}, estoy revisando contigo el estado general de TitanMDM.`

    case 'devices':
      return `${name}, estamos en el inventario. Puedo ayudarte a revisar el estado de la flota dentro de tus permisos.`

    case 'device-detail':
      return `Estoy siguiendo el contexto de este dispositivo, ${name}. Podemos revisar su estado, seguridad, cumplimiento y comandos.`

    case 'groups':
      return `Estamos en Grupos y Flota. Mantendré este contexto para las operaciones relacionadas con dispositivos agrupados.`

    case 'enrollment':
      return `Estamos en Inscripción. Puedo ayudarte a entender el proceso de incorporación de dispositivos.`

    case 'policies':
      return `Estamos revisando Políticas. Tendré en cuenta tus permisos antes de presentar acciones administrativas.`

    case 'policy-editor':
      return `Estoy siguiendo la política que estás configurando y mantendré este contexto mientras trabajas.`

    case 'apps':
      return `Estamos en Aplicaciones. Puedo ayudarte a interpretar el inventario y el estado de las aplicaciones administradas.`

    case 'security':
      return `Estamos en Seguridad. Daré prioridad a riesgos y eventos que tu usuario tenga autorización para consultar.`

    case 'compliance':
      return `Estamos revisando Cumplimiento. Puedo ayudarte a interpretar evaluaciones y hallazgos autorizados.`

    case 'kiosk':
      return `Estamos en Kiosk. Mantendré la configuración de dispositivos dedicados como contexto activo.`

    case 'geofencing':
      return `Estamos en Geofencing. Puedo contextualizar zonas, asignaciones y eventos permitidos.`

    case 'automation':
      return `Estamos en Automatización. Puedo seguir reglas y ejecuciones disponibles para tu rol.`

    case 'remote':
      return `Estamos en Soporte remoto. Mantendré el dispositivo y la sesión actual como contexto cuando corresponda.`

    case 'reports':
      return `Estamos en Reportes. Puedo ayudarte a interpretar la información disponible para tu usuario.`

    case 'audit':
      return `Estamos en Auditoría. Solo utilizaré información dentro del alcance autorizado para tu sesión.`

    case 'users':
      return `Estamos administrando Usuarios. Respetaré los permisos de tu sesión para cualquier consulta o acción.`

    case 'roles':
      return `Estamos en Roles. Tendré en cuenta la matriz de permisos antes de cualquier operación futura.`

    case 'settings':
      return `Estamos en Configuración. Mantendré el contexto administrativo sin asumir permisos adicionales.`

    default:
      return `Estoy aquí, ${name}. Mantendré el contexto mientras navegas por TitanMDM.`
  }
}