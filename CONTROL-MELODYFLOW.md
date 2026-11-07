# Control de proyecto — MelodyFlow

> Actualizado: 31 de octubre de 2026. Este documento sirve como tablero de seguimiento: separa lo que existe en el código de lo que aún hay que instalar, configurar o probar en el equipo.

## Cómo leer este control

- **Implementado en código** significa que existe en la solución; no confirma que la base de datos o los programas locales estén preparados.
- **Pendiente local** significa que falta comprobar o configurar el equipo, sus secretos o sus herramientas.
- **Fase posterior** significa que está contemplado, pero no forma parte del flujo musical actual.
- Los detalles técnicos completos siguen en el [README](README.md) y los documentos enlazados al final.

## Norma para música destinada a monetización en YouTube

- Antes de elegir y usar un modelo, herramienta, SoundFont, muestra, visual u otro recurso, comprobar sus **condiciones concretas y vigentes** para el uso comercial y la distribución previstos. Repetir la comprobación antes de publicar; guardar procedencia y evidencia de los términos consultados.
- Conservar, incluir y cumplir las atribuciones, avisos de copyright y demás avisos que exijan esas condiciones. La licencia es el conjunto de términos de uso del recurso; **no es una versión del programa ni una clave que haya que introducir**.
- Priorizar opciones gratuitas, pero no asumir que “gratis” significa coste total cero ni que concede permiso comercial. No comprar ni introducir una licencia o clave solo por la palabra “licencia”; primero identificar y revisar los términos del recurso concreto.
- Antes de publicar, consultar las reglas vigentes de YouTube y marcar la declaración de IA cuando corresponda. La guía actual de YouTube enumera la música generada con IA como contenido que requiere declaración. Cumplir esa declaración no asegura que el video o canal sea elegible para monetización: YouTube también evalúa originalidad, autenticidad y contenido reutilizado o producido en masa.
- Ningún modelo, licencia, declaración de IA ni compositor MIDI garantiza autorización de terceros, ausencia de reclamaciones o monetización. La política y los términos pueden cambiar; revisar los enlaces oficiales en [Licencias y avisos](docs/THIRD-PARTY-NOTICES.md) antes de cada publicación.

## Qué hay creado

| Área | Estado actual |
|---|---|
| Solución .NET | Están los ocho proyectos acordados: WebApp, API, Application, Domain, Infrastructure, Contracts, Worker y Tests. No existe ni se necesita `Backend.Host`. |
| Dominio y persistencia | Existen `MusicProject`, `GenerationJob` y `MusicTrack`, sus mapeos EF Core/Npgsql y tres migraciones fuente. Las migraciones **no están aplicadas** a PostgreSQL. |
| API | `MelodyFlowAPI` es el host. Hay endpoints de proyectos, trabajos, cancelación, estado de canciones y descarga MP3. Es de uso local: sin login, con CORS, límite de peticiones y validación. |
| Cola y Worker | PostgreSQL hace de cola durable. El Worker reclama trabajos con leases, informa progreso, recupera leases vencidos y aplica hasta tres intentos en total. |
| Generación musical | El código conecta LM Studio, validación del arreglo, compositor MIDI determinista, FluidSynth, FFmpeg, FFprobe y almacenamiento local de archivos. El handler musical solo se activa cuando detecta los requisitos configurados. |
| Interfaz Web | El proyecto Blazor existe, pero todavía no está conectado a la API. |
| n8n | Está elegido como orquestador local, pero no hay un workflow exportado en este repositorio ni una ejecución verificada. |
| Vídeo y publicación | Importación visual, renderizado MP4 y publicación en YouTube quedan para fases posteriores. |

## Checklist: hecho y pendiente

### Hecho en el código

- [x] Solución con los ocho proyectos acordados y sin `Backend.Host` duplicado.
- [x] API como host local con endpoints de proyectos, trabajos, cancelación y consulta/descarga de canciones.
- [x] Validación de dominio, persistencia EF Core/Npgsql y contratos v1.
- [x] Cola PostgreSQL con leases, progreso, recuperación, cancelación y máximo de tres intentos.
- [x] Pipeline musical implementado: plan JSON → MIDI → WAV → MP3 y metadatos de la canción.
- [x] Almacenamiento de audio fuera de PostgreSQL; la base conserva metadatos y clave del archivo.
- [x] Configuración de protecciones locales en el API: CORS, rate limit y validación; no hay autenticación de usuario.
- [x] LM Studio descargado, según confirma el usuario; todavía falta comprobar que abre, cargar un modelo y arrancar su servidor.
- [x] Los comandos `ffmpeg` y `ffprobe` se encuentran en `PATH`; el render real aún está pendiente.

### Falta configurar o comprobar en el equipo

- [x] Confirmar que Docker ejecuta `melodyflow-postgres`, que `melodyflow_db` acepta conexiones y que Windows llega al host port `5433` (Docker container port `5432`).
- [x] Confirmar el usuario PostgreSQL `admin`; la contraseña solo se usa desde configuración local ignorada.
- [x] Crear `appsettings.Local.json` ignorados para API y Worker con la conexión local común.
- [x] Confirmar que `Cors:AllowedOrigins` contiene los dos orígenes del WebApp en la configuración Development.
- [ ] Revisar el SQL de las tres migraciones y decidir si se aplican a la base.
- [ ] Abrir LM Studio, descargar y cargar un modelo local, completar su nombre en la configuración y comprobar sus condiciones concretas de uso comercial; guardar fuente y términos.
- [ ] Instalar/configurar FluidSynth; no se encontró el comando en `PATH`.
- [ ] Obtener el archivo `FluidR3_GM.sf2`, comprobar procedencia y permiso comercial, conservar los avisos requeridos y configurar su ruta.
- [ ] Decidir si se usa LocalAppData bajo la misma cuenta o configurar una ruta absoluta compartida en `Media:StorageRoot`.
- [ ] Ejecutar una generación corta y comprobar que el MP3 se reproduce y su duración se mide.
- [ ] Añadir y ejecutar tests enfocados para el pipeline musical y los endpoints de canciones.
- [ ] Crear y probar el workflow n8n usando solo HTTP contra la API e `Idempotency-Key`.

### Fases posteriores

- [ ] Conectar el WebApp Blazor a la API.
- [ ] Añadir importación visual y renderizado de vídeo MP4.
- [ ] Añadir publicación privada de YouTube para revisión.

## Herramientas musicales y función de cada una

La generación automática prevista funciona así: **LM Studio propone instrucciones musicales; MelodyFlow compone MIDI; FluidSynth lo interpreta con un SoundFont; FFmpeg crea el MP3; n8n coordina el proceso mediante la API.** LM Studio no sintetiza audio y n8n no compone música.

| Herramienta | Para qué sirve en MelodyFlow | Estado observado / siguiente paso |
|---|---|---|
| **LM Studio** | Ejecuta localmente un modelo que propone un plan JSON de tempo, tonalidad y acordes. No es el sintetizador ni el DAW. | El usuario confirma que ya lo descargó. URL prevista `http://localhost:1234/v1`; falta confirmar que abre, cargar un modelo y arrancar el servidor local. El nombre del modelo está vacío en `appsettings.json`. |
| **Compositor C# de MelodyFlow** | Valida el plan y genera un archivo MIDI determinista con acordes, bajo y melodía. | Implementado en Infrastructure; el Worker lo invoca cuando el handler está habilitado. |
| **FluidSynth** | Sintetiza el MIDI y produce WAV usando un SoundFont. | Es el otro programa esencial que falta: la ruta está como `fluidsynth`, pero el comando no se detectó en `PATH`; instalarlo o configurar una ruta válida. |
| **FluidR3 GM (`.sf2`)** | Banco de instrumentos que FluidSynth necesita para reproducir el MIDI. | `SoundFontPath` está vacío. Conseguir el archivo y conservar su aviso de licencia; la referencia MIT/atribución está en [THIRD-PARTY-NOTICES](docs/THIRD-PARTY-NOTICES.md). |
| **FFmpeg** | Convierte el WAV a MP3. Más adelante puede ensamblar audio y visuales en MP4. | El comando `ffmpeg` sí se detectó en `PATH`; falta probar un render real. |
| **FFprobe** | Mide la duración del archivo final y permite guardar la duración observada. | El comando `ffprobe` sí se detectó en `PATH`; falta probarlo con un MP3 generado. |
| **LMMS** | DAW opcional para edición musical manual. | No forma parte del flujo automático ni es un requisito de generación. No confundirlo con LM Studio. |
| **n8n** | Programa ejecuciones, llama a la API, consulta el estado, solicita cancelación y notifica. | La integración está diseñada para HTTP local. Falta localizar/confirmar la instalación actual y crear y ejecutar el workflow; n8n no debe conectarse directamente a PostgreSQL. |

## Configuración planeada y situación actual

### API y acceso local

| Ajuste | Valor o decisión | Situación / acción |
|---|---|---|
| Host API | HTTP `localhost:5032`; HTTPS `localhost:7158` | Está en `MelodyFlowAPI/Properties/launchSettings.json`. |
| Orígenes WebApp | `http://localhost:5073` y `https://localhost:7277` | Ya están configurados en `MelodyFlowAPI/appsettings.Development.json`. |
| Seguridad | Solo local, sin autenticación de usuario/API | Mantener el listener en loopback y no exponerlo a redes públicas o compartidas. CORS solo afecta al navegador; n8n usa HTTP de servidor a servidor. |
| Límite de peticiones | 120 por IP por minuto | Implementado en el API. |
| Host permitido | `localhost;127.0.0.1;[::1]` | Valor actual de `AllowedHosts`. |

### PostgreSQL y secretos

| Ajuste | Valor de referencia | Situación / acción |
|---|---|---|
| Contenedor | `melodyflow-postgres` | En ejecución; `pg_isready` acepta conexiones. |
| Host y puertos | `localhost:5433` → container `5432` | Mapeo confirmado por Docker; Windows usa el puerto 5433. |
| Base y usuario | `melodyflow_db`, `admin` | Confirmados en la configuración del contenedor. |
| Contraseña | No se muestra | Leída localmente desde la configuración de Docker y escrita solo en los archivos ignorados. |
| Cadena de conexión | Mismo valor para API y Worker | Configurados en sus respectivos `appsettings.Local.json`, ignorados por Git. |
| Migraciones | `InitialMusicSchema`, `AddPostgresGenerationJobQueue`, `AddGeneratedMusicTracks` | Generadas en código, ninguna aplicada. Revisar el SQL y decidir antes de actualizar la base. |

### Worker, almacenamiento y generación

| Ajuste | Valor actual / previsto | Situación / acción |
|---|---|---|
| Sondeo de cola | Cada 2.000 ms | Valor actual de `Queue:PollIntervalMilliseconds`. |
| Lease | 90 segundos | Valor actual de `Queue:LeaseDurationSeconds`; el Worker lo renueva durante la ejecución. |
| Intentos | Máximo 3 en total | Regla de dominio: intento inicial más hasta dos reintentos. |
| Servidor local | `http://localhost:1234/v1` | Valor por defecto del planificador LM Studio. |
| Modelo | `MusicGeneration:Planner:Model` | Vacío; seleccionar y cargar un modelo local en LM Studio. Revisar la licencia de ese modelo por separado. |
| Token del planificador | Opcional | Si el servidor local lo requiere, guardarlo en User Secrets, nunca en `appsettings.json`. |
| Timeout del planificador | 600 segundos | Valor actual; permite respuestas lentas del modelo local. |
| SoundFont | `MusicGeneration:SoundFontPath` | Vacío; señalar la ruta local del `.sf2`. La clave de evidencia de licencia ya apunta al aviso del repositorio. |
| FluidSynth | `fluidsynth` | No se detectó el comando en `PATH`; instalar o configurar su ruta. |
| FFmpeg / FFprobe | `ffmpeg` / `ffprobe` | Los dos comandos se detectaron en `PATH`; falta comprobar el flujo con archivos reales. |
| Timeout de herramientas de audio | 3.600 segundos por proceso | Valor actual de `ProcessTimeoutSeconds`. |
| Carpeta de medios | `Media:StorageRoot` está vacía en API y Worker | El código usa LocalAppData de la cuenta actual. Para evitar diferencias, configurar una ruta absoluta idéntica en ambos procesos y ejecutar ambos con acceso a ella. Los binarios no se guardan en PostgreSQL. |

### n8n

- Mantener n8n en el mismo equipo que el API en esta fase local. Los perfiles del API usan `localhost:5032` por HTTP y `localhost:7158` por HTTPS.
- Crear el flujo con un disparador manual o programado, selección/creación de proyecto, `POST /api/v1/generation-jobs`, sondeo limitado de `GET /api/v1/generation-jobs/{id}` y, al completar, lectura de `/track` y descarga del audio.
- Enviar un `Idempotency-Key` estable para que los reintentos HTTP no creen trabajos duplicados. Gestionar errores `400`, `404`, `409`, `429` y `503`.
- No conectar n8n a la base de datos. Si n8n corre en Docker, `localhost` apunta al contenedor n8n; habrá que preparar una ruta al host y una regla de firewall local antes de conectarlo.
- `N8N__WebhookSecret` aparece como posibilidad futura en la guía; no se usa en este flujo local sin autenticación.

## Comprobaciones locales realizadas

- Docker informa que `melodyflow-postgres` está en ejecución; `pg_isready` y la conexión desde Windows al puerto `5433` responden.
- `ffmpeg` y `ffprobe` están disponibles en `PATH`.
- `fluidsynth` y `lmstudio` no se detectaron como comandos en `PATH`. El usuario confirma que LM Studio está descargado; falta comprobar la aplicación gráfica y arrancar su servidor.
- La ruta del SoundFont y el nombre del modelo están vacíos en la configuración versionada.
- No se encontró un archivo de workflow n8n en el repositorio.
- La compilación de la solución pasó en la última revisión registrada. No se ejecutaron tests de la generación musical ni se verificó un render de audio real.
- El árbol Git tiene cambios locales sin commit; revisar `git status` y `git diff` antes de preparar un commit.

## Orden recomendado

1. Revisar el código comentado y este control; anotar cualquier cambio de producto antes de ajustar el flujo.
2. Con la conexión local ya configurada, generar y revisar el SQL de las tres migraciones. Aplicarlas solo cuando el usuario decida actualizar la base.
3. Elegir/cargar el modelo de LM Studio, instalar FluidSynth, obtener el SoundFont con su aviso de licencia y fijar la misma carpeta de medios para API y Worker.
5. Arrancar API y Worker y hacer una generación corta; comprobar que el trabajo termina y que la API entrega un MP3 medible.
6. Después de verificar el ciclo API → PostgreSQL → Worker → audio, crear y ejecutar el workflow n8n contra la API local.
7. Más adelante, añadir importación visual, renderizado MP4, publicación privada de YouTube y conexión del WebApp.

## Documentos fuente

- [README general](README.md)
- [Mapa de documentación](docs/DOCUMENTATION-MAP.md)
- [Arquitectura](docs/ARCHITECTURE.md)
- [Decisiones](docs/DECISIONS.md)
- [Configuración local y secretos](docs/SETUP-DEPLOYMENT.md)
- [Base de datos y migraciones](docs/DATABASE-EFCORE.md)
- [Contratos de API](docs/API-CONTRACTS.md)
- [Pipeline musical](docs/MUSIC-PIPELINE.md)
- [Integración n8n](docs/N8N-INTEGRATION.md)
- [Licencias y avisos](docs/THIRD-PARTY-NOTICES.md)
- [Plan por fases](docs/ROADMAP.md)

## Descargas gratuitas previstas (sin MCP obligatorio)

Para el flujo automático de MelodyFlow no hace falta instalar ningún MCP: n8n hablará con la API por HTTP. MCP queda como una integración opcional futura.

| Qué necesitas | ¿Gratis? | Qué hacer |
|---|---|---|
| **LM Studio** | Sin coste por llamada para inferencia local; hardware, electricidad y modelo pueden implicar otros costes o condiciones. | Ya lo descargaste. Comprueba que arranca y baja un modelo compatible desde su catálogo local. Revisa las condiciones del modelo elegido para uso comercial antes de utilizarlo; no se presupone permiso por ser gratuito. [Descarga oficial](https://lmstudio.ai/download) · [Guía para cargar modelos](https://lmstudio.ai/docs/app/basics). |
| **FluidSynth** | Sí, es software libre con licencia LGPL. | Es el programa que falta para sintetizar el MIDI. Usa la página de [descargas oficiales para Windows](https://www.fluidsynth.org/download/) y conserva los avisos de la versión elegida. |
| **FluidR3 GM (`.sf2`)** | La referencia upstream indica MIT con atribución; verifica el archivo obtenido y sus términos. | Es un archivo de banco de sonidos separado de FluidSynth; hay que obtenerlo y guardar su aviso. El [archivo de sonidos de MuseScore](https://ftp.osuosl.org/pub/musescore/soundfont/fluid-soundfont.tar.gz) es un espejo de descarga; al extraerlo, configura en MelodyFlow la ruta real del `.sf2`. Revisa también el [aviso de licencia y atribución](https://github.com/musescore/MuseScore/blob/main/share/sound/FluidR3Mono_License.md). |
| **FFmpeg y FFprobe** | Sí, el proyecto es software libre; la licencia concreta depende de cómo se haya compilado el binario. | Ya aparecen en `PATH`, así que no los vuelvas a descargar ahora. [Descargas oficiales e indicación de compilaciones para Windows](https://www.ffmpeg.org/download.html) · [licencias](https://ffmpeg.org/legal.html). |
| **n8n Community autoalojado** | Sí, para crear y ejecutar tus workflows internos bajo su Sustainable Use License; n8n Cloud es un plan distinto. | Si ya tienes una instalación local, no necesitas descargar otra. Falta crear el workflow de MelodyFlow. [Guía de autoalojamiento](https://docs.n8n.io/hosting/) · [condiciones de uso](https://support.n8n.io/article/can-i-use-your-license-for-my-use-case). |
| **LMMS** | No hace falta descargarlo para este flujo. | Es un DAW opcional para editar arreglos manualmente; no confundirlo con LM Studio ni FluidSynth. |

El modelo se ejecuta localmente y evita pagar una API por cada generación; el coste práctico depende del ordenador y la electricidad. No se ha elegido todavía un modelo concreto. OpenRouter y MCP no forman parte de la configuración gratuita seleccionada.

## Recomendaciones de versiones estables (comprobadas el 31-10-2026)

Estas versiones son referencias estables para empezar, no requisitos ni una orden de instalar la versión más reciente. Se recomienda evitar versiones beta y volver a comprobar las fuentes y condiciones del recurso antes de elegirlo o publicar.

- **LM Studio para Windows: 0.4.26 estable.** [Descarga oficial para Windows](https://lmstudio.ai/download?os=windows) y [registro oficial de cambios](https://lmstudio.ai/changelog/lmstudio). Instalar LM Studio por sí solo no activa el planificador de IA: hay que descargar aparte un modelo, cargarlo en la aplicación y arrancar el servidor local compatible con la API. Como modelo pequeño para empezar se recomienda **Qwen3-4B-Instruct-2507, cuantización Q4_K_M**: [ficha oficial del modelo y licencia](https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507) · [GGUF Q4_K_M para LM Studio](https://huggingface.co/lmstudio-community/Qwen3-4B-Instruct-2507-GGUF). Revisa las condiciones del modelo concreto para el uso previsto. Sin un modelo cargado y el servidor local activo, el planner actual no puede generar; si falta algún requisito local de configuración, el handler no se registra y los trabajos quedan en cola hasta completarlo o sustituir el planner. Si el servidor deja de estar disponible después del registro, las solicitudes del planner fallan hasta que vuelva a estar activo.
- **FluidSynth: 2.6.1.** Referencia de versión estable en [releases oficiales](https://github.com/FluidSynth/fluidsynth/releases); consulta también las [descargas oficiales](https://www.fluidsynth.org/download/). No se ha verificado que esté instalado localmente.
- **SoundFont FluidR3 GM:** Debian ofrece como alternativa verificada el paquete fuente **fluid-soundfont 3.1-5.3**, que contiene `FluidR3_GM.sf2` y `COPYING` (MIT): [fuentes Debian](https://sources.debian.org/src/fluid-soundfont/3.1-5.3/). Esto no cambia el origen recomendado que ya figura arriba: el mirror de MuseScore y su [aviso de licencia/atribución](https://github.com/musescore/MuseScore/blob/main/share/sound/FluidR3Mono_License.md). No se ha confirmado que el archivo de Debian ya se haya descargado.
- **FFmpeg y FFprobe: 9.0.2 stable** es la última versión estable oficial consultada a esa fecha: [descargas y versiones](https://ffmpeg.org/download.html). En el entorno ya se detectó **FFmpeg 9.0.1-full_build de Gyan**, con `ffprobe` incluido; basta para seguir con las pruebas actuales y no hace falta reinstalarlo ahora.
- **n8n Community autoalojado: 2.45.7 stable**, la versión estable más reciente consultada en la fecha indicada: [release oficial 2.45.7](https://github.com/n8n-io/n8n/releases/tag/n8n%402.45.7). Instalarlo o actualizarlo solo cuando toque crear/probar el workflow; no es necesario para la generación musical local actual.
