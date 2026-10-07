# TitanMDM Enterprise Release Checklist

## Source

- [ ] working tree clean
- [ ] release commit identificado
- [ ] tag de versión creado
- [ ] CI verde
- [ ] dependency scan verde
- [ ] secret scan verde
- [ ] CodeQL revisado
- [ ] SBOM generado

## Backend

- [ ] Release build
- [ ] migrations verificadas
- [ ] health live correcto
- [ ] health ready correcto
- [ ] production configuration validada
- [ ] secrets externalizados

## Frontend

- [ ] production build
- [ ] routing validado
- [ ] permisos visuales validados
- [ ] errores críticos corregidos

## Windows Agent

- [ ] enrollment
- [ ] heartbeat
- [ ] inventory
- [ ] commands
- [ ] policies
- [ ] apps
- [ ] reconnect
- [ ] update
- [ ] uninstall

## Remote Support

- [ ] authentication
- [ ] authorization
- [ ] streaming
- [ ] keyboard
- [ ] mouse
- [ ] cursor
- [ ] multi-monitor
- [ ] UAC
- [ ] Secure Desktop
- [ ] disconnect
- [ ] audit

## Helpdesk

- [ ] ticket lifecycle
- [ ] mail ingestion
- [ ] assignment
- [ ] SLA
- [ ] attachments
- [ ] audit

## Ponches

- [ ] connector
- [ ] authentication
- [ ] devices
- [ ] records
- [ ] synchronization
- [ ] reports

## Android

- [ ] enterprise binding
- [ ] enrollment
- [ ] policies
- [ ] commands
- [ ] apps
- [ ] kiosk
- [ ] compliance

## Security

- [ ] RBAC tests
- [ ] scope tests
- [ ] rate limiting
- [ ] security headers
- [ ] TLS
- [ ] vulnerability scan
- [ ] penetration test
- [ ] audit reviewed

## Data Protection

- [ ] backup successful
- [ ] restore successful
- [ ] DR procedure tested

## Installer

- [ ] server installer
- [ ] Windows agent installer
- [ ] GPO deployment
- [ ] code signing
- [ ] hashes
- [ ] updater
- [ ] rollback

## Pilot

- [ ] staging validated
- [ ] pilot group validated
- [ ] rollback validated
- [ ] monitoring active
- [ ] production approval
