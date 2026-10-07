# TitanMDM Enterprise - Artifact Policy

## Objetivo

Definir qué artefactos pueden almacenarse en Git y cuáles deben generarse,
firmarse, publicarse o conservarse fuera del repositorio fuente.

## Principios

El repositorio Git contiene código fuente, configuración no sensible,
documentación, migraciones y automatización reproducible.

No debe utilizarse como repositorio de binarios de producción.

## No permitido en Git

- Ejecutables `.exe`
- Instaladores `.msi` / `.msix`
- DLL compiladas
- Símbolos `.pdb`
- Paquetes ZIP generados
- Builds frontend `dist`
- `node_modules`
- Entornos virtuales Python
- Bases de datos
- Backups
- Logs
- Exportaciones XLS/XLSX/CSV
- Certificados privados
- Claves privadas
- Secrets
- Service-account credentials
- Archivos de configuración locales
- Metadata de IDE

## Artefactos de release

Los artefactos finales deberán producirse mediante pipelines versionados.

Cada release deberá generar cuando corresponda:

- TitanMDM Server
- TitanMDM Agent
- TitanMDM RemoteHost
- Titan Edge Connector
- Titan Ponches Service
- Android Agent
- instaladores
- hashes SHA-256
- SBOM
- changelog
- manifest de versiones

## Integridad

Todo binario distribuido debe tener:

1. versión;
2. hash SHA-256;
3. origen de build identificable;
4. commit Git asociado;
5. firma digital cuando aplique;
6. manifiesto de release.

## Secretos

Los secretos nunca deben almacenarse en el repositorio.

En Development se utilizarán:

- .NET User Secrets;
- variables de entorno;
- archivos locales ignorados.

En producción se utilizará un `ISecretProvider` compatible con:

- proveedor local seguro;
- Windows/DPAPI o vault corporativo;
- Azure Key Vault para escenarios híbridos/cloud.

## Retención

Los paquetes de producción deberán almacenarse en el repositorio corporativo
de artefactos definido para TitanMDM y no dentro del historial Git.
