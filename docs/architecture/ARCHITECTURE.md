# TitanMDM Enterprise Architecture

## 1. Arquitectura general

TitanMDM utiliza una arquitectura empresarial modular
orientada inicialmente a despliegue on-premise.

Componentes principales:

```text
Users
  |
  v
IIS / Reverse Proxy
  |
  +----------------------+
  |                      |
  v                      v
React Web            TitanMDM API
                         |
       +-----------------+------------------+
       |                 |                  |
       v                 v                  v
   SQL Server        SignalR Hubs      Background Workers
       |                                    |
       |                                    |
       +-------------+----------------------+
                     |
          +----------+----------+
          |                     |
          v                     v
   Windows Agents         Android Enterprise
          |
          +--- RemoteHost

TitanMDM API
      |
      +--- Ponches Edge / Python
      |
      +--- Ollama
      |
      +--- Entra ID
