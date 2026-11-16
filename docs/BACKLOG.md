# BACKLOG — Nova Control Center

Prioridade: **P0** bloqueante · **P1** essencial v1.0 · **P2** desejável · **P3** futuro
Status: ⬜ a fazer · 🔄 em andamento · ✅ feito · ⛔ bloqueado (aguarda validação)

## Sprint 01 — Diagnóstico do hardware
| ID | Item | Prio | Status |
|---|---|---|---|
| S1-01 | Instalar .NET 8 SDK e validar `dotnet --info` | P0 | ✅ |
| S1-02 | Criar solução, `Directory.Build.props`, `.editorconfig`, `git init` | P0 | ✅ |
| S1-03 | Ferramenta `Probe` (console, só leitura): listar HID `VID 0x3537`, interfaces, usage pages, tamanhos de relatório | P0 | ✅ |
| S1-04 | `Probe`: listar slots XInput + `GetCapabilitiesEx` (VID/PID por slot) + `GetBatteryInformation` | P0 | ✅ |
| S1-05 | `Probe`: `Windows.Gaming.Input` — controladores e `TryGetBatteryReport` | P0 | ✅ |
| S1-06 | `Probe`: dump passivo de relatórios de entrada da interface vendor (`MI_02`) para CSV | P0 | ✅ |
| S1-07 | Sessões de teste com o controle: modo XInput, bateria cheia vs. baixa, carregando, desligado com receptor plugado | P0 | ✅ |
| S1-08 | Verificar suporte do software oficial ao T4 Nova Lite no PC; se houver, capturar tráfego com USBPcap/Wireshark | P1 | ⬜ |
| S1-09 | Consolidar resultados em `HARDWARE_DIAGNOSTICS.md` e decidir go/no-go da bateria | P0 | ✅ NO-GO |
| S1-10 | Validar se o BatteryReport do WinRT (1000/1000 mWh) é real ou sintetizado (`battery-log` com bateria baixa e carregando) | P0 | ✅ sintetizado |
| S1-11 | Mover o projeto para um caminho curto (o caminho atual passa de 260 caracteres e impede executar binários e testes) | P1 | ✅ |

## Sprint 02 — Comunicação
| ID | Item | Prio | Status |
|---|---|---|---|
| S2-01 | Modelos Core (`ControllerInfo`, `ControllerState`, `ConnectionSnapshot`) + testes (`BatteryStatus` foi para S3-02) | P0 | ✅ |
| S2-02 | `XInputReader` (P/Invoke) atrás de `IInputReader` | P0 | ✅ |
| S2-03 | `HidDevicePresence` + `KnownDevices` (VID/PID validados) | P0 | ✅ |
| S2-04 | Casamento slot XInput ↔ dispositivo físico; filtro de pads virtuais | P1 | ✅ |
| S2-05 | Detecção de conexão/desconexão + reconexão automática | P0 | ✅ |
| S2-06 | Estado de conexão em 3 níveis (receptor ausente / 2106 controle desligado / 1040 conectado) com debounce de ~2 s para a re-enumeração | P0 | ✅ |

## Sprint 03 — Monitoramento
| ID | Item | Prio | Status |
|---|---|---|---|
| S3-01 | `ControllerMonitorService` (thread dedicada, snapshots) | P0 | ✅ |
| S3-02 | Cadeia `IBatteryProvider` — v1.0 retorna `Unavailable` com motivo; ponto de extensão para provider vendor | P0 | ✅ |
| S3-03 | `BatteryAlertService` (limite configurável, histerese, sem alertas repetidos) | P1 | ⛔ sem fonte de bateria |
| S3-04 | Teste de vibração (motores L/R) | P2 | ✅ (só 1 motor sentido) |

## Sprint 04 — Interface
| ID | Item | Prio | Status |
|---|---|---|---|
| S4-01 | Shell: janela, navegação lateral, tema escuro, estados vazio/desconectado | P0 | 🔄 em validação |
| S4-02 | Dashboard | P0 | 🔄 em validação |
| S4-03 | Tela de Monitoramento (layout do controle, sticks 2D, gatilhos, valores brutos) | P0 | 🔄 em validação |
| S4-04 | Tela Sobre (versão, licenças, caminho dos logs) | P2 | 🔄 em validação |
| S4-06 | Teste manual de botões (cobertura "X de 15") e de vibração por motor na tela de Monitoramento | P1 | 🔄 em validação |
| S4-07 | Modernização visual: janela própria, desenho do controle, novos componentes | P1 | ✅ |
| S4-08 | Validar convivência com outro controle (DS4 via Bluetooth, com e sem DS4Windows/ViGEm) | P2 | ✅ (sem DS4Windows) |
| S4-09 | Reduzir a poluição visual: abas na página PS4, detalhes técnicos recolhidos | P1 | ✅ |
| S4-05 | Notificação de bateria baixa (toast do Windows + ícone na bandeja) | P1 | ⛔ sem fonte de bateria |

## Épico DS4 — Suporte ao DualShock 4 (prioridade sobre a Sprint 05, por decisão do usuário)
| ID | Item | Prio | Status |
|---|---|---|---|
| D4-01 | Investigação somente leitura: HID, relatórios, drivers (HARDWARE_DIAGNOSTICS §3.10) | P0 | ✅ |
| D4-02 | `Ds4Protocol`: parser `0x01`/`0x11`, bateria, construtor da saída `0x11` + CRC-32, com testes | P0 | ✅ |
| D4-03 | `nova-probe ds4`: ler entradas e bateria, testar a luz com o controle real | P0 | ✅ USB: entradas, bateria, luz e vibração · BT: entradas e bateria |
| D4-04 | Refatorar para multi-dispositivo (`IControllerDevice`, seletor de controle na UI) | P0 | 🔄 parcial: página dedicada, sem refatoração multi-dispositivo |
| D4-05 | UI do DS4: bateria aproximada, cor da luz, interruptor "Recursos avançados" | P1 | ✅ (USB) · BT: luz/vibração pendentes (D4-08) |
| D4-06 | Modo XInput (ViGEm): 360 virtual, repasse de entradas e vibração, aviso de duplicidade | P1 | ✅ validado: entradas no Xbox virtual + vibração do jogo → PS4 |
| D4-07 | Detectar o HidHide (sem instalar) e mostrar o estado | P3 | ⬜ |
| D4-08 | Bluetooth: luz e vibração não chegam ao controle do usuário (USB funciona). Investigar caminho de saída BT no Windows | P2 | ⬜ |
| D4-09 | Silhueta própria do DS4 (analógicos simétricos embaixo) | P3 | ⬜ |
| D4-10 | Persistir "Recursos avançados" e a última cor entre sessões | P2 | ✅ (+ modo de reconhecimento e formato de envio) |
| D4-11 | Tocar som de teste no alto-falante do DS4 (exige controle com saída de áudio USB; o do usuário não tem) | P3 | ✅ implementado (tom + medidor de microfone); mecanismo validado com o áudio do PC; aguarda DS4 original para validação final |
| D4-12 | Touchpad e sensores de movimento (giroscópio/acelerômetro) na página PS4 | P1 | ✅ |
| D4-13 | Calibração de fábrica do giroscópio (feature 0x02/0x05) para mostrar °/s reais | P3 | ⬜ |

## Sprint 05 — Configurações e perfis
| ID | Item | Prio | Status |
|---|---|---|---|
| S5-01 | `InputProcessor`: deadzone radial/axial, curva de resposta, faixa de gatilho + testes | P1 | ✅ |
| S5-02 | Tela de Configurações com rótulo 🖥️ Software / 🎮 Firmware e pré-visualização ao vivo | P1 | ✅ |
| S5-03 | `JsonProfileRepository` (%AppData%), escrita atômica, versão de schema | P1 | ✅ (no settings.json, ADR-25) |
| S5-04 | Tela de Perfis: criar, renomear, excluir, duplicar, ativar; perfis padrão | P1 | ✅ |
| S5-05 | Remapeamento de botões (software) | P2 | ⬜ |
| S5-06 | Aplicar perfis ao GameSir nos jogos (pad virtual + ocultar o original com HidHide) | P3 | ⬜ |
| S5-07 | Validar com o usuário a sensação dos perfis num jogo (PS4 no modo Xbox) | P1 | ⬜ |

## Sprint 06 — Qualidade / v1.0
| ID | Item | Prio | Status |
|---|---|---|---|
| S6-01 | Rodada de testes manuais (checklist) + correções | P0 | 🔄 automação ✅; checklist manual em docs/TEST_CHECKLIST.md |
| S6-02 | Revisão de UX/acessibilidade (contraste, teclado, escala de DPI) | P1 | 🔄 contraste AA ✅, nomes p/ leitor de tela ✅, foco visível ✅; Tab e DPI no checklist manual |
| S6-03 | Publicação self-contained single-file + README | P1 | ✅ v1.0.0 publicada (GitHub Releases) |

## Futuro / Em investigação
| ID | Item | Prio |
|---|---|---|
| F-01 | Pad virtual (ViGEm + HidHide) para aplicar ajustes de software nos jogos | P3 |
| F-02 | Configurações de firmware (somente se protocolo documentado) | P3 |
| F-03 | Iniciar com o Windows / minimizar na bandeja | P2 · ✅ (bandeja, iniciar com o Windows, iniciar minimizado) |
| F-04 | Troca automática de perfil por jogo (processo em foco) | P3 |

| F-05 | Capturar o protocolo do software oficial (USBPcap) para tentar ler a bateria pela interface vendor `0xFF7A` | P2 |
