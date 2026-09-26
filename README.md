# 🚲 Plataforma de Incidencias Operativas - CompartiBici
**Examen Parcial: Arquitectura de Servicios Distribuidos, Control de Versiones Git y Despliegue en Render**

---

## 📌 1. Información General del Proyecto
* **Repositorio GitHub:** [https://github.com/gerfhy/compartibici](https://github.com/gerfhy/compartibici)
* **Stack Tecnológico:** ASP.NET Core (.NET 10) + EF Core SQLite + ASP.NET Identity con Roles
* **Servicios Distribuidos Integrados:**
  1. **Algolia Search:** Búsqueda en servidor sobre índice `incidencias` con filtrado de registros abiertos en base de datos.
  2. **Redis Cloud:** Caché distribuida de 60 segundos con invalidación reactiva tras el cierre de incidencias y telemetría de `CACHE HIT` / `CACHE MISS`.
  3. **PieHost (PieSocket WebSockets):** Transmisión de eventos `IncidenciaActualizada` en tiempo real tras la persistencia en base de datos, con actualización reactiva sin recargar la página y sincronización al reconectar.
* **Infraestructura de Despliegue:** Render.com (Web Service Dockerizado con disco persistente `/var/data` para SQLite).

---

## 🔐 2. Credenciales del Sistema y Usuario Supervisor
| Rol / Acceso | Usuario / Identificador | Contraseña / Clave |
| :--- | :--- | :--- |
| **Supervisor Operaciones** | `supervisor@compartibici.com` | `Admin123!` |
| **Ruta del Panel Operativo** | `/Operaciones/Incidencias` | Acceso con rol Supervisor |

---

## 🌿 3. Control de Versiones: Ramas, Pull Requests y Resolución de Conflictos

### A. Ramas y Pull Requests Independientes
Todas las ramas (`feature/busqueda-algolia`, `feature/cache-redis`, `feature/websocket-piehost`) nacieron del **mismo commit inicial de `main`** (`59ab7f8`) sin trabajo directo sobre `main`.

| Pregunta / Feature | Rama de Desarrollo | Enlace de Pull Request | Commit de Implementación |
| :--- | :--- | :--- | :--- |
| **Pregunta 1: Búsqueda Algolia** | `feature/busqueda-algolia` | [Abrir / Ver PR #1](https://github.com/gerfhy/compartibici/pull/new/feature/busqueda-algolia) | `28fdce5` |
| **Pregunta 2: Caché Redis** | `feature/cache-redis` | [Abrir / Ver PR #2](https://github.com/gerfhy/compartibici/pull/new/feature/cache-redis) | `99b4490` |
| **Pregunta 3: WebSocket PieHost** | `feature/websocket-piehost` | [Abrir / Ver PR #3](https://github.com/gerfhy/compartibici/pull/new/feature/websocket-piehost) | `182fc9a` |

---

### B. Historial de Git (`git log --graph --oneline --all`)
```text
*   c14cf24 Merge pull request #3 from gerfhy/feature/websocket-piehost
|\  
| *   a5b2729 Merge branch 'main' into feature/websocket-piehost: resolver conflicto integrando busqueda Algolia, cache Redis y tiempo real PieHost
| |\  
| |/  
|/|   
* |   ccaef41 Merge pull request #2 from gerfhy/feature/cache-redis
|\ \  
| * \   25ae79b Merge branch 'main' into feature/cache-redis: resolver conflicto integrando busqueda Algolia y cache Redis
| |\ \  
| |/ /  
|/| |   
* | |   13a7e2c Merge pull request #1 from gerfhy/feature/busqueda-algolia
|\ \ \  
| * | | 28fdce5 feat(pregunta-1): busqueda de incidencias con algolia y filtrado de abiertas
|/ / /  
| * / 99b4490 feat(pregunta-2): implementacion de cache distribuida con redis por 60s e invalidacion reactiva
|/ /  
| * 182fc9a feat(pregunta-3): sincronizacion en tiempo real con websockets de piehost y actualizacion sin recarga
|/  
* 59ab7f8 feat: proyecto base de plataforma de incidencias con SQLite e Identity
```

---

### C. Explicación Técnica de las Resoluciones de Conflicto

#### 1. Primer Conflicto: Fusión de `main` en `feature/cache-redis` (Commit `25ae79b`)
* **Línea en conflicto (Título compartido en `Incidencias.cshtml`):**
  * `main` traía: `<h1>Incidencias abiertas encontradas</h1>` (Algolia)
  * `feature/cache-redis` tenía: `<h1>Incidencias abiertas con consulta rápida</h1>` (Redis)
  * **Resolución:** Se unificó a `<h1>Incidencias abiertas encontradas con consulta rápida</h1>`.
* **Conflicto en `Controllers/OperacionesController.cs`:**
  * Se respetó la regla: *«La búsqueda con texto de Algolia se consultará directamente, sin usar esta caché»*. Si `q` contiene texto, se consulta Algolia de forma directa sin Redis; si `q` es vacío, se consulta el listado general cacheado por 60 segundos en Redis.
* **Conflicto en `Program.cs`:** Se mantuvieron registrados ambos servicios (`IAlgoliaSearchService` y `AddStackExchangeRedisCache`).

#### 2. Segundo Conflicto: Fusión de `main` en `feature/websocket-piehost` (Commit `a5b2729`)
* **Línea en conflicto (Título compartido en `Incidencias.cshtml`):**
  * `main` traía: `<h1>Incidencias abiertas encontradas con consulta rápida</h1>` (Algolia + Redis)
  * `feature/websocket-piehost` tenía: `<h1>Incidencias abiertas en tiempo real</h1>` (PieHost)
  * **Resolución:** Se unificó a `<h1>Incidencias abiertas (Búsqueda Algolia, Caché Redis y Tiempo Real PieHost)</h1>`.
* **Conflicto en `Controllers/OperacionesController.cs` (Secuencia Estricta de Cierre):**
  * Se implementó rigurosamente la secuencia requerida por la rúbrica:
    1. **Persistencia:** Actualizar estado en la base de datos SQLite (`Estado = "Cerrada"`).
    2. **Invalidación:** Remover la clave del listado general en Redis (`_cache.RemoveAsync("incidencias_abiertas_listado")`).
    3. **Notificación:** Publicar evento `IncidenciaActualizada` con `Id` y `Estado` en el canal de PieHost.
  * **Garantía Algolia:** Al persistir el estado cerrado en base de datos, el filtro del servidor descarta automáticamente registros cerrados, impidiendo que vuelvan a aparecer en búsquedas futuras.
* **Conflicto en `Program.cs`:** Se inyectaron todos los servicios concurrentemente sin duplicaciones.

---

## ⚡ 4. Servicios Configurados y Variables de Entorno

### Configuración para Render (Copiar y Pegar en Environment Variables)
```env
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Data Source=/var/data/app.db
Redis__ConnectionString=redis://default:RYce7mVimFAZpuRaM5KQ6CNO8WIV61Lk@dapper-teaberry-sidewalk-15613.db.redis.io:10824
PieSocket__ClusterId=free.blr2
PieSocket__ApiKey=DNHnT7FNMDzgRPwTvT482A1mnvsx1t99rlRDX8oh
PieSocket__Secret=OqV8j8cJzfn4TWJbqbwMAyhXLoQRckqU
PieSocket__RoomId=incidencias_canal
Algolia__ApplicationId=YKB1BBI5WH
Algolia__SearchApiKey=538818a359ee8e43b705e51c3c6f34c5
Algolia__ApiKey=538818a359ee8e43b705e51c3c6f34c5
Algolia__WriteApiKey=1a491ca1eeeff6a0f903b9858939c70d
Algolia__IndexName=incidencias
```

---

## 🧪 5. Guía de Pruebas y Evidencia de Evaluación

### A. Prueba de Algolia (Pregunta 1)
1. Ingresar a `/Operaciones/Incidencias`.
2. Escribir `Miraflores` o `freno` en el buscador y pulsar **Buscar**.
3. El servidor consulta la API de Algolia y retorna exclusivamente las incidencias abiertas que coincidan.
4. Si se busca `Amistad` (incidencia cerrada #6), el sistema no la presenta.

### B. Prueba de Redis Cloud (Pregunta 2)
1. Al acceder a `/Operaciones/Incidencias` sin término de búsqueda:
   * La primera petición genera un log `>>> [CACHE MISS]` y muestra la insignia `🗄️ BASE DE DATOS (CACHÉ MISS)`.
   * Las peticiones subsecuentes dentro de los 60 segundos generan logs `>>> [CACHE HIT]` y muestran la insignia `⚡ REDIS (CACHÉ HIT - TTL 60s)`.
2. Al pulsar **Cerrar Incidencia**:
   * El log emite `>>> [2. REDIS INVALIDATION]` y la clave es removida inmediatamente.
   * La recarga posterior resulta en un nuevo `CACHE MISS` con los datos actualizados.

### C. Prueba de WebSockets con PieHost (Pregunta 3)
1. Abrir dos navegadores o una ventana normal y otra en modo incógnito en `/Operaciones/Incidencias`.
2. Ambos mostrarán la insignia `🟢 PieHost En Vivo (WebSocket)`.
3. En la sesión A, pulsar **Cerrar Incidencia** en una avería.
4. En la sesión B, sin recargar la pantalla:
   * La fila correspondiente se desvanece suavemente con animación roja y desaparece.
   * El contador de incidencias abiertas disminuye en tiempo real.
   * Aparece una alerta interactiva indicando el ID de la incidencia cerrada.
5. Si se simula desconexión de red y reconexión, la pantalla invoca automáticamente `/Operaciones/ObtenerIncidenciasJson` y sincroniza el estado vigente.

---

## 🚀 6. Despliegue en Render (Pregunta 5)
1. Conectar el repositorio `https://github.com/gerfhy/compartibici` en Render.
2. Crear un **Web Service** seleccionando **Docker**.
3. Agregar un disco persistente montado en `/var/data` (1 GB) para persistencia SQLite.
4. Configurar las variables de entorno detalladas en la Sección 4.
5. El servicio compilará automáticamente mediante el `Dockerfile` optimizado para .NET 10 y expondrá el puerto `$PORT` asignado dinámicamente.
