# CHANGELOG

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/). Versionamento SemVer.

## [Não lançado]

### Sprint 05 — Configurações e perfis (2026-11-13)
#### Adicionado
- `InputProfile` (analógicos: zona morta + curva; gatilhos: zona morta + ""completo em""; intensidade da vibração) e perfis iniciais **Padrão**, **FPS** e **Corrida**.
- `InputProcessor`: zona morta radial com reescala, curva de sensibilidade, gatilho curto e vibração proporcional.
- `ProfileService`: criar (a partir do ativo), duplicar, renomear, excluir (Padrão protegido), editar e ativar, com validação de nome e persistência no `settings.json`.
- **Página Configurações**: sliders por analógico, gatilhos e vibração, aviso de onde os ajustes valem e **pré-visualização ao vivo** (bruto × com perfil) usando o PS4 ou o GameSir.
- **Página Perfis**: cartões com resumo, selo ""ATIVO"", ativar, duplicar, excluir com confirmação, criar e renomear, com mensagens de erro claras.
- **O perfil ativo vale nos jogos para o PS4 no modo Xbox** (entradas e vibração).
- 29 novos testes (167 no total).
#### Corrigido
- **Erro ao fechar o app**: o `Ds4Service` era descartado duas vezes (registrado como serviço e como interface) e lançava `ObjectDisposedException`. `Dispose` agora é idempotente.

### D4-10 — Preferências salvas (2026-11-13)
#### Adicionado
- `ISettingsStore` + `JsonSettingsStore`: `%AppData%\NovaControlCenter\settings.json` com gravação atômica, versão de formato e backup `settings.json.corrompido-*` se o arquivo estiver inválido.
- O PS4 lembra **Recursos avançados**, **modo de reconhecimento** (PlayStation/Xbox), **última cor da luz** e **formato de envio**. Tudo é aplicado na inicialização, e a cor é reaplicada a cada conexão (só com recursos avançados).
- 11 novos testes (138 no total).
#### Validado
- Escolhas feitas na interface (Xbox + recursos avançados) foram gravadas e, ao reabrir o app, ele já criou o Xbox virtual sozinho.

### Épico DS4 — modo XInput e sensores (2026-11-13)
#### Adicionado
- **Modo de reconhecimento**: PlayStation (nativo) ou **Xbox (XInput emulado)** via ViGEmBus (`Nefarius.ViGEm.Client`, MIT). O app cria um Xbox 360 virtual, repassa as entradas do PS4 e devolve ao PS4 a vibração pedida pelos jogos (por cabo sempre; pelo Bluetooth só com recursos avançados). Mostra a posição XInput e avisa sobre possível duplicidade sem HidHide.
- **Touchpad**: até 2 dedos desenhados em tempo real, coordenadas e destaque do clique.
- **Giroscópio e acelerômetro**: desenho do controle que inclina junto com o real, barras de rotação, valores brutos e botão "Zerar posição".
- **Alto-falante**: detecção da saída de áudio do controle com explicação quando não existe (o controle do usuário não expõe áudio).
- `nova-probe xinput-check <slot>`: simula um jogo XInput (pede vibração e registra as entradas).
- Painel **"O que o jogo vê"** no modo Xbox: lê o Xbox virtual pelo XInput, como um jogo leria.
#### Validado com o hardware
- Modo Xbox de ponta a ponta: entradas do PS4 chegam ao Xbox virtual e a vibração do jogo volta ao PS4 (HARDWARE_DIAGNOSTICS §3.10.8).
- 22 novos testes (127 no total), incluindo a decodificação de um relatório real do controle do usuário.
#### Corrigido
- Estouro em `Math.Abs(short.MinValue)` nas barras do giroscópio (um giro muito rápido derrubaria a tela), encontrado pelos testes.

### Épico DS4 — suporte ao DualShock 4 (2026-11-13, em andamento)
#### Adicionado
- `Ds4Protocol`: leitura dos relatórios `0x01`/`0x11` (botões, D-pad, sticks com Y invertido, gatilhos, touchpad, bateria), relatório de saída com CRC-32 configurável e `Crc32` próprio.
- `Ds4Connection`: HID com leitura, modo estendido (feature 0x05), saída pelo canal de controle (`HidD_SetOutputReport`) ou de interrupção, e **verificação do link Bluetooth real via WinRT**.
- `Ds4Service`: thread própria, reconexão automática, reativação do modo estendido ao religar o controle e taxa de relatórios. **Nada é enviado sem "Recursos avançados".**
- Página **Controle PS4**: estado, desenho com rótulos PlayStation, aviso de touchpad, interruptor de recursos avançados, bateria aproximada (passos de 10%), seletor de cor com pré-definições e RGB, pulsos de vibração e seletor de formato de envio para diagnóstico.
- `nova-probe ds4 [validate|light|light-paths|rumble-paths]`.
- Suporte a DS4 por **USB**: relatório de saída `0x05`; o app prefere o USB e troca sozinho quando o cabo é conectado.
- Rodapé da barra lateral mostra o status dos **dois controles** (GameSir e PS4); o modo do PS4 indica USB, Bluetooth básico ou Bluetooth estendido.
#### Corrigido
- **Luz do PS4 (por USB):** os comandos de cor agora vão sempre com o estado dos motores (flags `0x03`), porque o controle do usuário ignora relatórios só de luz. Ver HARDWARE_DIAGNOSTICS §3.10.6.
- 35 novos testes (107 no total).
#### Descoberto
- Bateria do DS4 lida com sucesso (~40%). A luz não respondeu a 8 variantes de envio (ver HARDWARE_DIAGNOSTICS §3.10.5).
- O nó HID do DS4 continua "presente" com o controle desligado; o estado real vem do link Bluetooth.

### Validado — convivência com DualShock 4 (2026-11-13)
- Com um DS4 via Bluetooth conectado ao mesmo tempo, o app continua identificando só o GameSir (slot XInput 0, associação exata). O PS4 não ocupa slot XInput e é ignorado. Ver HARDWARE_DIAGNOSTICS §3.9.

### Sprint 04 — Modernização visual (2026-11-13)
#### Alterado
- Janela com moldura própria (`WindowChrome`): barra de título integrada, botões minimizar/maximizar/fechar e correção de margem ao maximizar; mantém arrastar, encaixar e redimensionar.
- Nova paleta com gradiente de destaque (azul → violeta), cartões com cantos maiores, selos de ícone e chips.
- Barra lateral com logotipo, indicador de página ativa e status com ponto pulsante quando conectado.
- **Dashboard:** cartão de destaque com gradiente, ilustração do controle e chips de conexão e tempo.
- **Monitoramento:** desenho do controle (`ControllerVisual`) em que os botões acendem no próprio formato; ele se adapta ao tamanho da janela.
- Interruptor no lugar da caixa de seleção, slider e barra de rolagem finos e estilizados, e transição de página com deslize suave.
- A descrição do estado "conectado" não repete mais o nome do controle.

### Sprint 04 — Interface (2026-11-13, em validação)
#### Adicionado
- App WPF com Generic Host (injeção de dependência), Serilog (logs em `%LocalAppData%\NovaControlCenter\logs`) e CommunityToolkit.Mvvm.
- Tema escuro, navegação lateral (Dashboard, Monitoramento, Configurações, Perfis, Sobre), status da conexão no rodapé, barra de título escura e transição suave entre páginas.
- **Dashboard:** estado em 3 níveis com mensagem clara, dispositivo, modelo (avisa quando é presumido), tipo de conexão, receptor VID:PID, posição XInput, tempo conectado e bateria "Não informada" com o motivo.
- **Monitoramento:** desenho do controle com botões acesos, analógicos em 2D (com destaque de L3/R3), barras de gatilho, valores brutos, taxa de atualização, teste de botões ("X de 15") e teste de vibração por motor (pulso ou contínuo).
- Configurações e Perfis aparecem como "em breve" (Sprint 05); a página Sobre mostra transparência, logs e licenças.
- Controles `StickView` e `ButtonIndicator`; `Presenter` puro para os textos; 13 novos testes (72 no total).

### Sprint 03 — Monitoramento (2026-11-13)
#### Adicionado
- `BatteryStatus` (tipo fechado: `Unavailable` / `Level` / `Percent` validado), `IBatteryProvider`, `BatteryService` (cadeia de provedores; sem fonte real, mostra o motivo) e `XInputBatteryProvider` (ignora WIRED).
- `InputMonitor`: leitura em segundo plano, publica só quando o pacote muda, mede atualizações/s e é tolerante a falhas.
- `IRumbleService` + `XInputRumble` (`XInputSetState`, intensidade limitada a 0..1).
- `nova-probe input-test [--rumble]`.
#### Descoberto
- D-pad e Home não detectados no teste automático (a confirmar); os analógicos voltam exatamente a (0,0) em repouso; os gatilhos vão de 0 a 255; a taxa é de ~67/s.
- Só um motor vibrou fisicamente, embora o `XInputSetState` aceite os dois canais.

### Sprint 02 — Comunicação (2026-11-13)
#### Adicionado
- Core: `ControllerState` (botões, analógicos normalizados, gatilhos), `ConnectionState` (3 níveis), `ControllerInfo`, `ConnectionSnapshot`, `SlotMatch`.
- Core: interfaces `IInputReader` e `IDevicePresence`.
- Core: `ConnectionResolver` (regra 1040/2106 + associação do slot XInput, ignorando pads virtuais), `ConnectionDebouncer` (espera de 2 s só para "receptor ausente") e `ConnectionMonitor` (polling a 500 ms + evento de mudança de dispositivos, tolerante a falhas, com `ConnectedSince`).
- Infrastructure: `XInputReader` (com fallback se os ordinais não documentados faltarem) e `HidDevicePresence` (HidSharp, só enumera).
- `nova-probe monitor`: validação ao vivo do monitor de conexão.
- 20 novos testes (36 no total).

#### Alterado
- O projeto foi movido para um caminho curto; a compilação e os testes agora rodam direto na pasta.
- `KnownDevices`: constantes `NovaLiteConnected` (1040) e `NovaLiteReceiverIdle` (2106).

#### Validado com o hardware
- Ciclo liga/desliga/retira o receptor/recoloca: transições corretas e sem oscilação (ver HARDWARE_DIAGNOSTICS §3.7).

### Sprint 01 — Diagnóstico

### Adicionado
- 2026-11-13 — Análise técnica inicial: `PRODUCT.md`, `ARCHITECTURE.md`, `BACKLOG.md`, `HARDWARE_DIAGNOSTICS.md`.
- 2026-11-13 — Varredura PnP inicial: identificado VID `0x3537` (GameSir), PIDs `0x1040` (composto com XInput + HID vendor) e `0x2106` (HID vendor) no histórico do Windows; ViGEmBus detectado.
- 2026-11-13 — Sprint 01: solução .NET 8 (App, Core, Infrastructure, Probe, Tests), `Directory.Build.props` (warnings como erros), repositório Git.
- 2026-11-13 — Sprint 01: ferramenta `nova-probe` (somente leitura) com os comandos `scan`, `listen`, `battery-log` e `watch`.
- 2026-11-13 — Infrastructure: P/Invoke XInput (`GetState`/`GetStateEx`/`GetCapabilities`/`GetCapabilitiesEx`/`GetBatteryInformation`), inspeção HID (HidSharp), escuta HID passiva com estatística por byte, diagnóstico Windows.Gaming.Input.
- 2026-11-13 — 14 testes unitários (parser de caminho HID, estatística de bytes, textos XInput, `UsbDeviceId`).

### Descoberto
- XInput reporta o T4 Nova Lite como `BATTERY_TYPE_WIRED`: não há bateria por XInput.
- A interface vendor `MI_02` (usage page `0xFF7A`, 65 B in/out) fica silenciosa em escuta passiva.
- O WinRT `BatteryReport` devolve 1000/1000 mWh, provavelmente sintetizado (em validação).
- PID `0x2106` (Zikway "HID zkm") = receptor sem controle; `0x1040` = controle conectado. A troca é uma re-enumeração USB com ~1 s sem interface.
- WinRT `BatteryReport` fica fixo em 1000/1000 mWh `Discharging`, inclusive durante a carga: o valor é sintetizado.
- **Decisão S1-09: NO-GO** para leitura de bateria na v1.0 pelos meios seguros.

### Alterado
- `nova-probe listen vendor` segue a interface vendor entre os PIDs 1040 e 2106 e registra eventos `attach`/`detach` no CSV.
- `KnownDevices`: PID 2106 marcado como `Confirmed`.
