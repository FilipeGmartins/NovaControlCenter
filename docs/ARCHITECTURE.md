# ARCHITECTURE — Nova Control Center

## 1. Visão geral

Clean Architecture em 3 camadas + testes. A dependência aponta sempre para dentro (`App → Core ← Infrastructure`).

```
┌──────────────────────────── NovaControlCenter.App (WPF, MVVM) ───────────────────────────┐
│ Views ─ ViewModels ─ Converters ─ Resources(Tema escuro)    Composition Root (Generic Host)│
└───────────────────────────────────────┬──────────────────────────────────────────────────┘
                                        │ depende de interfaces
┌───────────────────────────── NovaControlCenter.Core (sem dependência de UI/hardware) ─────┐
│ Models: ControllerInfo, ControllerState, BatteryStatus, Profile, StickSettings ...         │
│ Interfaces: IControllerDiscovery, IInputReader, IBatteryProvider, IRumbleService,          │
│             IProfileRepository, IInputProcessor, IClock                                    │
│ Services: ControllerMonitorService (polling/eventos), InputProcessor (deadzone/curvas),    │
│           BatteryAlertService, ProfileService                                              │
└───────────────────────────────────────▲──────────────────────────────────────────────────┘
                                        │ implementa
┌───────────────────────────── NovaControlCenter.Infrastructure ────────────────────────────┐
│ XInput/          P/Invoke xinput1_4 (GetState, SetState, GetBatteryInformation,            │
│                  GetCapabilitiesEx #108 p/ VID/PID por slot)                               │
│ Hid/             HidSharp: enumeração, descriptors, leitura passiva de relatórios          │
│ DeviceDetection/ Notificação de plug/unplug (HidSharp DeviceList.Changed)                  │
│ Configuration/   Perfis/configurações JSON em %AppData%\NovaControlCenter                  │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

## 2. Decisões técnicas

| # | Decisão | Motivo |
|---|---|---|
| ADR-01 | .NET 8 (LTS) + WPF, `net8.0-windows10.0.19041.0` | Requisito; TFM com SDK do Windows libera WinRT (`Windows.Gaming.Input`, toasts) sem pacotes extras |
| ADR-02 | MVVM com **CommunityToolkit.Mvvm** | Source generators, zero reflexão, MIT |
| ADR-03 | DI/config/log via **Microsoft.Extensions.Hosting** | Padrão .NET, sem framework extra |
| ADR-04 | XInput por **P/Invoke próprio** (sem SharpDX — descontinuado) | 5 funções; evita dependência morta |
| ADR-05 | HID via **HidSharp** (Apache-2.0) | Multiplataforma, leitura de descriptor, eventos de mudança |
| ADR-06 | Polling de entrada em thread dedicada (`PeriodicTimer`, ~250 Hz no Monitor, ~2 Hz no Dashboard); UI recebe snapshots via `Dispatcher` com *coalescing* | Não bloquear a UI; XInput não tem eventos |
| ADR-07 | `BatteryStatus` é um tipo discriminado: `Unavailable(motivo)`, `Wired`, `Level(Empty/Low/Medium/Full)`, `Percent(n)` (este só se uma fonte real fornecer) | Impede por construção a exibição de % fictícia |
| ADR-08 | Configurações carregam a origem: `ConfigScope.Software` / `ConfigScope.Firmware` | Requisito de transparência; a UI rotula pela origem |
| ADR-09 | Nenhuma escrita HID (`Write`/`SetFeature`) no código de produção até haver protocolo documentado em `HARDWARE_DIAGNOSTICS.md` | Segurança do dispositivo |
| ADR-10 | Log com **Serilog** (arquivo rotativo em `%LocalAppData%\NovaControlCenter\logs`) | Diagnóstico em máquinas de usuário |
| ADR-11 | Diagnóstico de hardware vive em `Infrastructure` (XInput/Hid/GamingInput) e é consumido pelo `nova-probe`; as mesmas primitivas serão reaproveitadas pelos serviços da Sprint 02 | Evita código descartável |
| ADR-12 | O `LibraryImport` exige `AllowUnsafeBlocks` em Infrastructure; o `unsafe` fica restrito ao código gerado | P/Invoke sem marshalling em tempo de execução |
| ADR-13 | Tempo via `TimeProvider` (.NET 8) em vez de `DateTime.Now`; testes usam `FakeTimeProvider` | Debounce e `ConnectedSince` testáveis de forma determinística |
| ADR-14 | Logs com `[LoggerMessage]` (source generator) | Exigido pelo analisador CA1848; sem alocação em caminho quente |
| ADR-15 | Conexão em 3 níveis derivada do PID do receptor (1040/2106) + espera de 2 s só para o estado ausente | O receptor se re-enumera em cada liga/desliga, com ~1 s sem interface |
| ADR-16 | A UI lê `InputMonitor.Latest` via `DispatcherTimer` de 16 ms, em vez de receber um evento a cada pacote | Evita inundar o Dispatcher; a tela atualiza a ~60 fps |
| ADR-17 | Botões expostos por indexador (`Buttons[A]`) que notifica `Item[]` | Um único objeto observável para os 15 botões, com XAML enxuto |
| ADR-18 | Os textos da UI ficam em `Presenter` (estático e puro); os ViewModels só orquestram | Regras de exibição testáveis sem WPF |
| ADR-19 | Moldura própria via `WindowChrome` (sem `AllowsTransparency`) | Visual moderno mantendo o encaixe e o redimensionamento nativos e a aceleração de hardware |
| ADR-20 | Silhueta do controle como `Geometry` em espaço fixo de 640x360 dentro de `Viewbox` | Uma única fonte da forma (Dashboard e Monitoramento), escalável sem perda |
| ADR-21 | Suporte multi-dispositivo: cada controle vira um `IControllerDevice` (GameSir via XInput, DS4 via HID), descoberto por provedores independentes | Adicionar o DS4 sem acoplar regras do GameSir |
| ADR-22 | Protocolo do DS4 isolado em `Infrastructure/Ds4` (parser de entrada, construtor do relatório de saída, CRC-32 próprio), testado byte a byte | O protocolo vem de fontes públicas (kernel Linux/SDL); os testes protegem contra regressões |
| ADR-23 | O modo estendido do DS4 só é ativado por ação explícita do usuário | Evita quebrar jogos DirectInput sem aviso (PRODUCT, decisão 1) |
| ADR-24 | Modo XInput do DS4 via `Nefarius.ViGEm.Client` (MIT) sobre o ViGEmBus já instalado; o app não instala drivers | Sem dependência nova de driver; o risco de manutenção (projeto arquivado) está documentado |
| ADR-25 | Preferências em `%AppData%\NovaControlCenter\settings.json` via `ISettingsStore` (Core) / `JsonSettingsStore` (Infrastructure): gravação atômica (temp + troca), campo `Version` para migração e backup automático se o arquivo estiver corrompido | Persistência simples, sem banco, resistente a quedas; o Core continua sem dependências |
| ADR-26 | A cor salva do PS4 é reaplicada a cada conexão **só** com "Recursos avançados" ligado | Mantém a decisão 1 do PRODUCT: nada é enviado ao controle sem permissão explícita |
| ADR-27 | `InputProcessor` puro no Core: zona morta **radial com reescala** (sem degrau na borda), curva de potência, gatilho com zona morta e "completo em" | Matemática testável; o mesmo código serve à prévia e ao controle virtual |
| ADR-28 | `ProfileService` no Core concentra as regras (nome obrigatório, único, até 40 caracteres; Padrão não exclui) e persiste via `ISettingsStore` | Regras num lugar só; as telas só exibem o resultado |
| ADR-29 | Perfis só são aplicados onde o app gera a saída (PS4 → Xbox virtual). A página Configurações deixa claro o que é pré-visualização | Coerente com a regra de não apresentar ajuste de software como efeito real quando não é |
| ADR-30 | Sliders gravam com atraso de 300 ms após o último ajuste; a prévia usa o rascunho na hora | Evita gravar o arquivo dezenas de vezes por segundo ao arrastar |

## 3. Identificação do controle

1. HidSharp enumera dispositivos com `VID=0x3537` → obtém PID, nome do produto, caminho, interfaces.
2. XInput: para cada slot 0–3 conectado, `XInputGetCapabilitiesEx` (ordinal 108, não documentado mas estável desde Win8) devolve VID/PID → **casa slot XInput ↔ dispositivo físico**.
3. Slots com `045E:028E` e sem dispositivo USB correspondente são marcados como **virtuais (ViGEm)** e ignorados.
4. Fallback se #108 falhar: assume o slot pela correlação de conexão (o slot que apareceu quando o receptor GameSir foi plugado) e exibe o aviso "identificação aproximada".

Tabela de PIDs conhecidos fica em `Infrastructure/DeviceDetection/KnownDevices.cs`, alimentada pelas descobertas da Sprint 01.

## 3.1 Fluxo de conexão (implementado na Sprint 02)

```
HidDevicePresence ──(PIDs presentes)──┐
                                      ├─> ConnectionResolver ──> ConnectionDebouncer ──> ConnectionSnapshot
XInputReader ──(slots + VID/PID)──────┘        (regra pura)       (2 s só p/ ausente)     (evento SnapshotChanged)
          ▲                                                                                  │
          └──────── ConnectionMonitor: loop a 500 ms, acordado antes pelo DeviceList.Changed ─┘
```

- `SnapshotChanged` dispara na thread do monitor; a UI despacha para o `Dispatcher`.
- Falhas de consulta ao hardware são registradas no log e mantêm o último estado.

## 4. Fontes de bateria (cadeia de responsabilidade)

`IBatteryProvider` com implementações consultadas em ordem; a primeira que retornar dado *real* vence:

1. `HidVendorBatteryProvider` — **só existe se** a Sprint 01 encontrar o byte de bateria em relatórios de entrada de `MI_02` (leitura passiva).
2. `WinRtBatteryProvider` — `Windows.Gaming.Input.RawGameController.TryGetBatteryReport()`.
3. `XInputBatteryProvider` — `XInputGetBatteryInformation`. `BATTERY_TYPE_WIRED` / `DISCONNECTED` ⇒ não há leitura útil.
4. Nenhuma ⇒ `BatteryStatus.Unavailable("O receptor não informa a bateria")`.

## 5. Configurações de software — como afetam jogos

Ajustes (deadzone, curva, remapeamento) feitos só no app **não alteram o que o jogo lê** do controle físico. Caminhos possíveis:

| Opção | Efeito | Custo/risco |
|---|---|---|
| A. Apenas visualização/teste | Ajustes servem para calibrar e testar no próprio app | Nenhum |
| B. Pad virtual (ViGEmBus, já instalado na máquina) + ocultar o físico (HidHide) | Jogos leem o pad virtual com ajustes aplicados | ViGEmBus foi arquivado pelo autor (funciona, sem manutenção); exige drivers de terceiros e ~1–4 ms de latência |
| C. Firmware | Ajuste vive no controle | Só se o protocolo proprietário for descoberto e validado |

Plano: v1.0 entrega **A** com arquitetura pronta para **B** (`IVirtualPadSink`), atrás de opção explícita. **C** permanece em investigação.

## 6. Tratamento de erros e desconexão
- Toda chamada de hardware retorna `Result`/estado, nunca lança para a UI.
- `ERROR_DEVICE_NOT_CONNECTED` do XInput e `IOException` do HidSharp ⇒ estado `Disconnected`, reconexão automática via `DeviceList.Changed` + polling leve de slots.
- Exceções inesperadas: log + mensagem amigável; o loop de polling se recupera.

## 7. Estrutura de pastas

```
NovaControlCenter/
├── docs/            PRODUCT, ARCHITECTURE, BACKLOG, CHANGELOG, HARDWARE_DIAGNOSTICS
├── src/
│   ├── NovaControlCenter.App/            Views, ViewModels, Resources, Converters, App.xaml(.cs)
│   ├── NovaControlCenter.Core/           Models, Interfaces, Services
│   └── NovaControlCenter.Infrastructure/ Hid, XInput, DeviceDetection, Configuration, Battery
├── tools/
│   └── NovaControlCenter.Probe/          Console de diagnóstico (Sprint 01), só leitura
├── tests/
│   └── NovaControlCenter.Tests/          xUnit + NSubstitute
├── NovaControlCenter.sln
├── Directory.Build.props                 Nullable, TreatWarningsAsErrors, LangVersion
└── .editorconfig
```

## 8. Dependências

| Pacote | Projeto | Licença |
|---|---|---|
| CommunityToolkit.Mvvm | App | MIT |
| Microsoft.Extensions.Hosting | App | MIT |
| Serilog.Extensions.Hosting, Serilog.Sinks.File | App | Apache-2.0 |
| HidSharp | Infrastructure, Probe | Apache-2.0 |
| xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk | Tests | Apache-2.0 / MIT |
| NSubstitute | Tests | BSD-3 |

Evitados: SharpDX (descontinuado), FluentAssertions ≥ 8 (licença comercial), Moq (histórico de telemetria).
Opcional pós-v1.0: Nefarius.ViGEm.Client (MIT) para a opção B.
