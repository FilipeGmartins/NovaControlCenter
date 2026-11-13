# Diagnóstico de Hardware — GameSir T4 Nova Lite

> Documento vivo. Registra **apenas fatos observados** e hipóteses explicitamente marcadas como tal.
> Legenda: ✅ confirmado · 🟡 hipótese (a validar) · ❌ descartado

## 1. Ambiente de diagnóstico

| Item | Valor |
|---|---|
| SO | Windows 10 Home 10.0.19045 |
| `xinput1_4.dll` | Presente (System32) |
| .NET | SDK 8.0.428 (instalado via winget em 2026-11-13) |
| Git | 2.54 |
| Drivers relevantes já instalados | **Nefarius Virtual Gamepad Emulation Bus (ViGEmBus)** — ativo |

## 2. Varredura PnP — 2026-11-13 (controle NÃO conectado no momento)

Nenhum controle estava presente. Porém o histórico PnP do Windows (dispositivos já vistos, status `Unknown`) revelou:

### 2.1 VID `0x3537` — candidato GameSir ✅ (fabricante) / 🟡 (modelo)

O VID `0x3537` é registrado para a GameSir. Dois PIDs foram vistos:

**PID `0x1040`** — dispositivo USB composto (visto em 3 portas diferentes):

| Interface | Classe Windows | Interpretação |
|---|---|---|
| `MI_00` | `XnaComposite` — "Controlador XBOX 360 para Windows" | Interface **XInput** (driver `xusb22`). É o que jogos enxergam. ✅ |
| `MI_01&COL01` | Teclado HID | Provavelmente macros/atalhos do receptor 🟡 |
| `MI_01&COL02` | Consumer Control HID | Teclas de mídia/volume 🟡 |
| `MI_02` | **HID definido pelo fornecedor** | Forte candidato a **canal proprietário de configuração/telemetria** (onde a bateria poderia trafegar) 🟡 |
| `IG_00` / `IG_01` | Controlador de jogo HID | Interfaces HID-gamepad (modo DInput/alternativo) 🟡 |

**PID `0x2106`** — apenas interface HID definida pelo fornecedor (sem XInput).
Hipóteses 🟡: outro modo do receptor, modo de atualização de firmware/bootloader, ou outro produto GameSir. **Não enviar nada a este dispositivo** até identificá-lo.

### 2.2 Outros dispositivos de jogo no histórico (para filtrar / não confundir)

| VID:PID | Identificação | Relevância |
|---|---|---|
| `045E:028E` serial `0000001` | Xbox 360 **virtual** criado pelo ViGEmBus | ⚠️ Ocupa slot XInput; o app precisa distinguir pad virtual de físico |
| `2DC8:3106` | 8BitDo (receptor 2.4G, modo XInput) | Outro controle do usuário |
| `054C:05C4` / `054C:09CC` (Bluetooth) | Sony DualShock 4 | Outro controle do usuário |
| `25A7:FA7B`, `258A:0016`, `0B05:1866` | Receptores de teclado/mouse e teclado ASUS | Ignorar — não são controles |

## 3. Varredura com `nova-probe scan` — 2026-11-13 08:01 (receptor conectado, controle ligado)

### 3.1 HID — VID 0x3537 PID 0x1040 ✅

| # | Interface | Usage | Relatórios (in / out / feature) | Strings |
|---|---|---|---|---|
| 0 | `IG_00` (XInput) | `0x01:0x05` Gamepad | 15 B / — / — | Produto "Controller (XBOX 360 For Windows)" |
| 1 | `MI_01 COL01` | `0x01:0x06` Keyboard | ID1 9 B / ID1 2 B (LEDs) / — | Fabricante **"2.4G"**, Produto "XBOX 360 For Windows" |
| 2 | `MI_01 COL02` | `0x0C:0x01` Consumer Control | ID3 3 B / — / — | idem |
| 3 | `MI_02` | **`0xFF7A:0x0001` Vendor-defined** | **65 B / 65 B / nenhum** | idem |

- Nenhuma interface expõe serial.
- Nenhuma interface usa as páginas padrão de bateria (`0x84` Power Device / `0x85` Battery System) ❌.
- `MI_02` tem relatórios de entrada e saída do mesmo tamanho (64 B + report ID 0) e nenhum feature report. É o formato típico de um **protocolo comando/resposta** 🟡.

### 3.2 XInput ✅
- Slot 0: `3537:1040` (o ordinal #108 funcionou e devolveu VID/PID), SubType GAMEPAD, Flags 0x0010.
- `XInputGetBatteryInformation` → **`BATTERY_TYPE_WIRED`** ✅. O receptor se apresenta como um 360 **com fio**: **XInput não fornece bateria** para este controle.

### 3.3 Windows.Gaming.Input ⚠️
- "Controlador XBOX 360 para Windows" `3537:1040`, `IsWireless=False`, 14 botões, 6 eixos.
- `TryGetBatteryReport()` → `Discharging`, **restante 1000 mWh / total 1000 mWh**, taxa `null`.
- 🟡 **Hipótese forte: valor sintetizado pelo Windows, não leitura real.** Um controle sem fio não teria capacidade exata de 1000 mWh e 100% ao mesmo tempo, e o dispositivo se declara com fio.
- **Não usar esse valor até validar com `battery-log`:** se continuar em 1000/1000 com a bateria baixa (LED piscando), fica confirmado que é falso.

### 3.4 Escuta passiva de `MI_02` (20 s, controle ligado e parado) ✅
- **0 relatórios recebidos** (20 timeouts de 1 s).
- Conclusão 🟡: `MI_02` não transmite telemetria espontaneamente; responde só quando consultado. Ler a bateria por ele exigiria **enviar um comando desconhecido**, o que a regra do projeto proíbe sem documentação.
- Pendente: repetir a escuta enquanto o usuário mexe no controle, liga/desliga e conecta o carregador (alguns receptores emitem eventos nessas transições).

### 3.5 Troca de identidade do receptor — 2026-11-13 ~08:04 ✅ (causa 🟡)
Poucos minutos depois do scan, o `3537:1040` sumiu e o receptor reapareceu como **`3537:2106`**:
- Uma única interface HID, **`0xFF7A:0x0001` Vendor-defined**, 65 B in/out, sem feature. É a mesma usage page do `MI_02`.
- Fabricante **"Zikway"**, produto **"HID zkm"**. Zikway é, ao que tudo indica, quem fabrica o chip ou firmware do receptor.
- XInput sem nenhum slot ocupado; WinRT sem nenhum controlador.
- ✅ **Confirmado pelo usuário: `2106` = receptor plugado com o controle desligado**, e **`1040` = controle conectado**.
- Impacto no produto: o app consegue diferenciar três estados: "receptor ausente", "receptor conectado, controle desligado" (`2106`) e "controle conectado" (`1040`).

### 3.6 Sessão guiada de 5 min — 2026-11-13 08:07:37–08:12:37 ✅
Roteiro pedido ao usuário: desligado → liga → mexe → parado → cabo de carga 1 min → tira o cabo → desliga → liga. A escuta (`listen vendor`) seguiu a interface vendor do PID que estivesse ativo, e o `battery-log` rodou em paralelo, com uma amostra por minuto.

**Transições observadas (tempo relativo):**

| t | Evento |
|---|---|
| 00:00 | `1040` ativo |
| 00:57 → 00:58 | `1040` sai, `2106` entra (controle desligou) |
| 01:02 → 01:04 | `2106` sai, `1040` entra (controle ligou) |
| 01:48 → 01:51 | `1040` → `2106` → `1040` (2106 durou ~1 s) |
| 04:21 → 04:27 | `1040` → `2106` → `1040` |

- A troca de PID é uma **re-enumeração USB completa**: há um intervalo de cerca de 1 s sem nenhuma interface. A detecção de conexão do app deve tolerar esse intervalo (debounce de cerca de 2 s) para não piscar "receptor ausente".
- **0 relatórios** nas interfaces vendor de `1040` e de `2106`, em todos os estados: parado, em uso, ligando, desligando e carregando. ❌ Não há telemetria espontânea.

**Bateria (`battery-log`):**

| Amostra | XInput | WinRT |
|---|---|---|
| 08:06:45 … 08:11:47 (6 amostras) | sempre `WIRED` | sempre `Discharging`, 1000/1000 mWh, taxa `null` |

- A amostra das 08:09:46 caiu dentro da janela do cabo de carga, prevista entre 08:09:37 e 08:10:37 se o roteiro foi seguido. Mesmo assim o WinRT continuou em **`Discharging`**.
- ❌ **Conclusão: o `BatteryReport` do WinRT é sintetizado pelo Windows (valor fixo) e NÃO reflete o controle.** Não pode ser exibido como bateria.
- Ressalva: o horário exato do cabo depende de o usuário ter seguido o roteiro. A confirmação final virá do `battery-log` longo (bateria baixa com o LED piscando).

### 3.7 Validação do ConnectionMonitor (Sprint 02) — 2026-11-13 08:24–08:28 ✅
Rodado com `nova-probe monitor --seconds 240`. Roteiro: liga → desliga → liga → retira o receptor → recoloca.

| t (s) | Estado publicado | Interpretação |
|---|---|---|
| 0,1 | ControllerOff | Receptor plugado, controle desligado |
| 82,7 | Connected, slot 0 (Exact) | Controle ligado; o slot XInput foi associado pelo VID/PID |
| 103,1 | ControllerOff | Controle desligado. **Sem passar por "não encontrado"** (o debounce funcionou) |
| 109,5 | Connected, slot 0 (Exact) | Controle religado. `ConnectedSince` foi renovado |
| 114,4 | ReceiverNotFound | Receptor retirado por volta de 112,4 s; o estado foi publicado após os 2 s de espera |
| 114,9 | ControllerOff | Receptor recolocado: **enumera primeiro como 2106** |
| 116,0 | Connected, slot 0 (Exact) | O controle reconectou sozinho ~1,2 s depois |

- ✅ Três estados distinguidos corretamente; nenhum falso "receptor ausente" nas trocas 1040↔2106.
- ✅ Associação exata do slot XInput via ordinal #108; o pad virtual do ViGEm não interferiu.
- ✅ `XInputReader.Read` devolve o estado do slot 0 (valores neutros com o controle parado).
- 🟡 A leitura com o controle em movimento ainda não foi registrada em arquivo. Será validada na tela de Monitoramento (Sprint 03/04).
- Observação: ao ser plugado, o receptor sempre passa pelo 2106 antes do 1040.

### 3.8 Teste de entradas e vibração (Sprint 03) — 2026-11-13 08:38 ✅/🟡
Rodado com `nova-probe input-test --seconds 120 --rumble`.

**Entradas (7.758 amostras):**
| Item | Resultado |
|---|---|
| A, B, X, Y, LB, RB, Start, Back, L3, R3 | ✅ detectados |
| D-pad (4 direções) | ✗ não detectado. 🟡 Falta saber se o usuário apertou; conferir na tela de Monitoramento |
| Home/Guide (via `XInputGetStateEx`) | ✗ não detectado. 🟡 Em vários receptores o Home é reservado para funções internas (modo/turbo) e não chega ao XInput |
| Analógico esquerdo | X −31926…32767, Y −32201…32767 (faixa quase completa) |
| Analógico direito | X −32768…32767, Y −32074…32767 |
| Gatilhos L2/R2 | 0…255 (curso completo, analógico) |
| Repouso | exatamente (0,0) nos dois analógicos. 🟡 O receptor provavelmente já aplica uma zona morta de fábrica |
| Taxa | ~67 atualizações/s no `nova-probe` (timer padrão de ~15,6 ms). Com o app WPF aberto, o Windows usa um timer mais fino e a tela mostrou **~125 atualizações/s**: a taxa real do receptor é de pelo menos 125 Hz |

**Vibração:** `XInputSetState` retornou sucesso para o motor esquerdo, o direito e os dois juntos, mas **o usuário sentiu apenas um motor vibrar**. 🟡 Hipóteses: o controle tem um único motor físico, ou o receptor repassa só um canal. A tela de Monitoramento permite testar cada lado separadamente.

### 3.9 Convivência com DualShock 4 via Bluetooth (S4-08) — 2026-11-13 09:00–09:02 ✅
Sem DS4Windows/Steam em execução e sem pad virtual ViGEm presente.

| Fonte | Somente PS4 (GameSir desligado) | PS4 + GameSir ligado |
|---|---|---|
| HID | `054C:09CC` (DS4 2ª geração) via Bluetooth, "Controlador de jogo compatível com HID" | idem, mais as 4 interfaces do `3537:1040` |
| XInput | nenhum slot | **só o slot 0 = `3537:1040`**. O PS4 não ocupa slot XInput |
| WinRT | `054C:09CC` como RawGameController (não Gamepad), sem fio, **BatteryReport indisponível** | GameSir (1000/1000 mWh sintetizado) + PS4 (indisponível) |
| ConnectionMonitor | `ControllerOff` (2106) ✅ | `Connected`, slot 0, associação **exata** ✅ |

- ✅ O PS4 é ignorado: o app só considera o VID `0x3537` e associa o slot XInput pelo VID/PID exato.
- Observação: o WinRT também não dá bateria do DS4 por Bluetooth, embora o próprio DS4 a envie no relatório HID. Ler isso exigiria suporte específico ao DS4, que está fora do escopo do produto.
- Pendente (opcional): repetir com o DS4Windows ativo. Ele cria um `045E:028E` virtual que ocupa um slot XInput; esse caso já é coberto por testes unitários (`ConnectedPid_WithMatchingSlot_IsExactMatch` e `ConnectedPid_NeverPicksVirtualPad`).

## 3.10 DualShock 4 (054C:09CC) via Bluetooth — investigação para suporte — 2026-11-13 09:06

### 3.10.1 Observado nesta máquina ✅
| Item | Valor |
|---|---|
| Interface HID | Uma só, usage `0x01:0x05` Gamepad; fabricante "Sony Computer Entertainment", produto "Wireless Controller"; serial = endereço Bluetooth |
| Relatórios de entrada | IDs `0x01` e `0x11`–`0x19` (máx. 547 B) |
| Relatórios de saída | IDs `0x11`–`0x19` |
| Feature reports | 34 IDs, entre eles `0x02` (calibração), `0x05`, `0xA3` |
| Escuta passiva, 5 s com o controle parado | 0 relatórios. 🟡 No modo básico (`0x01`) o DS4 tende a enviar só quando algo muda; a confirmar com movimento |
| WinRT | RawGameController, sem Gamepad e sem BatteryReport |
| XInput | Não aparece (sem DS4Windows/Steam convertendo) |
| ViGEmBus | **Instalado e ativo** (driver 1.21.442, pacote 1.22.0). Projeto arquivado pelo autor; funciona no Windows 10 |
| HidHide | **Não instalado** |
| Steam | Instalado (o Steam Input pode assumir o DS4 quando o Steam está aberto) |

### 3.10.2 Protocolo (fontes públicas, não é engenharia reversa nossa)
Referências: driver `hid-playstation` do kernel Linux (`drivers/hid/hid-playstation.c`), SDL (`SDL_hidapi_ps4.c`) e a documentação comunitária do DS4.
- **Modo básico vs. estendido (Bluetooth):** ao conectar, o DS4 envia `0x01` reduzido (sticks, botões, gatilhos), **sem bateria**. Ao ler o feature report de calibração (`0x05`/`0x02`) ou receber um relatório de saída `0x11`, ele passa a enviar `0x11` completo (78 B) **até ser desligado**.
- **Bateria (`0x11`):** byte 32 (= offset 30 do layout USB + 2 de cabeçalho). Bits 0–3 = nível; bit 4 = cabo conectado. Sem cabo: 0–10 (×10 = %). Com cabo: 0–10 carregando, 11 = carga completa. **A resolução real é de 10%**, então a interface deve mostrar o valor como aproximado.
- **Saída `0x11` (78 B) — luz e vibração:** `[0]=0x11`, `[1]=0xC0` (HID + CRC), `[2]=0x00`, `[3]` = flags válidas (`0x01` motor, `0x02` luz, `0x04` piscar), `[6]` = motor direito (leve), `[7]` = motor esquerdo (pesado), `[8..10]` = R, G, B, `[11..12]` = tempos de piscar, `[74..77]` = **CRC-32** (padrão, little-endian) calculado sobre `0xA2` + bytes `[0..73]`.

### 3.10.3 Implicações ⚠️
1. **Bateria e luz exigem o modo estendido.** Depois de ativado, o DS4 para de enviar `0x01`. Jogos que leem o DS4 como **DirectInput genérico** perdem as entradas até o controle ser religado. Jogos com suporte nativo a PlayStation (Steam/SDL) e o modo XInput emulado não são afetados.
2. **Modo XInput emulado** = o app cria um Xbox 360 virtual no ViGEmBus e repassa as entradas (e a vibração dos jogos de volta ao DS4). 🖥️ Só funciona com o app aberto.
3. **Sem o HidHide**, os jogos podem ver **dois controles** no modo XInput (o DS4 original e o 360 virtual). Instalar o HidHide é decisão do usuário (driver de sistema); o app não instala drivers.

### 3.10.4 Primeira tentativa de validação — 2026-11-13 ~09:15 ⚠️
- Resultado: 0 relatórios, feature 0x05 falhou e a escrita de saída deu **timeout (3 s)**.
- Causa confirmada: **o DS4 estava desligado.** O nó `HID\{00001124…}` continua "presente" no Windows (status OK), mas o dispositivo Bluetooth mostra `conectado=False` (propriedade `{83DA6326-…} 15`).
- Consequência de projeto: **presença HID não indica DS4 ligado.** O estado real vem de `BluetoothDevice.FromBluetoothAddressAsync(serial).ConnectionStatus` (WinRT); o serial HID é o endereço Bluetooth. Timeouts de escrita e de feature report agora são tratados (retornam falha em vez de derrubar o processo).

### 3.10.5 Validação do DS4 — leitura, bateria e luz — 2026-11-13 09:16–09:28
| Item | Resultado |
|---|---|
| Espera pelo link Bluetooth (WinRT) | ✅ Detectou o controle ao apertar PS |
| Leitura no modo básico `0x01` (15 s) | ✅ 2.303 relatórios (~150/s). Vistos: D-pad, L1, R1, PS, ✕, ○, □ (o restante provavelmente não foi pressionado a tempo) |
| Feature 0x05 → modo estendido | ✅ Aceito; o controle passou a enviar `0x11` (1.223 relatórios em 10 s) |
| **Bateria** | ✅ **~40%** (nível bruto 4, sem cabo) |
| **Luz** — 8 variantes (canal de controle/interrupção; 78/547 B; CRC nos bytes 74–77 ou no fim; sementes 0xA2/0x52; cabeçalhos 0xC0/0xC4; flags 0x02/0x03) | ❌ O Windows aceitou todas, **mas a cor não mudou** |
| Vibração (4 variantes: Linux e DS4Windows, controle/interrupção) | 🟡 Sem retorno do usuário. Agora testável pela interface |

Observações:
- O descritor que o HidSharp mostra no Windows é **reconstruído** a partir dos caps do HIDP: o preenchimento até 547 B é artefato da reconstrução. O `0x11` real tem 78 B.
- Hipóteses em aberto para a luz: **(A)** os relatórios de saída não chegam ao controle por algum detalhe do caminho Bluetooth do Windows; **(B)** controle paralelo/clone (endereço `20:24:01:…`, sem prefixo conhecido da Sony) que aceita leitura mas ignora saída. O teste de vibração diferencia (A) de (B).
- Teste manual pela página **Controle PS4** (formatos A–D): **nem a luz nem a vibração funcionaram**. A hipótese "formato errado" perde força; restam (A) o caminho de saída Bluetooth do Windows e (B) um controle que ignora a saída.
- Próximos testes para separar (A) de (B):
  1. **Cabo USB:** o app passa a preferir USB automaticamente (relatório `0x05`, 32 B, sem CRC, sem pilha Bluetooth). Se funcionar por USB → problema no caminho Bluetooth. Se não → indício forte de (B).
  2. **Steam (referência externa):** se o Steam Input também não mudar a luz nem vibrar → limitação do controle.
- **Resultado por USB (2026-11-13):** ✅ **a vibração funciona**, ❌ **a cor não muda**. O controle recebe e aplica o relatório `0x05` (motores), mas ignora os bytes de cor. Última variação em teste: flags `0xF7` (padrão DS4Windows) nos formatos C/D. Se também falhar, a conclusão é que **este controle não permite que o PC controle a cor da luz** (comportamento típico de controles paralelos), e a luz fica como "não suportada por este controle".
- Pendente: entender por que a vibração não chegou pelo Bluetooth, já que por USB ela funciona.

### 3.10.6 Causa da luz encontrada (USB) — 2026-11-13 ~09:50 ✅
- Observação do usuário: **a cor só muda quando a vibração é enviada junto.**
- Interpretação: este controle só aplica o relatório quando a flag de motor (`0x01`) está presente. Com só a flag de luz (`0x02`), ele ignora o relatório inteiro. Um DS4 original trata as flags de forma independente.
- Identidade: pelo cabo o controle se apresenta como `054C:05C4` (DS4 1ª geração); pelo Bluetooth, como `054C:09CC` (2ª geração). Um controle original não muda de PID conforme a conexão, o que reforça a hipótese de controle paralelo.
- **Correção (compatível com originais):** todo envio leva luz + estado atual dos motores (flags `0x03`, motores em 0 quando parados). Implementado em `Ds4Service`.
- Bluetooth: segue sem resposta à luz e à vibração. A investigar à parte.

### 3.10.7 Touchpad, sensores de movimento e alto-falante — 2026-11-13 ~10:05
**Layout (relatório completo, offsets no layout USB; Bluetooth `0x11` = +2):** giroscópio `[13..18]` e acelerômetro `[19..24]` (3 × int16 LE cada); status/bateria `[30]`; quantidade de pacotes de toque `[33]`; 1º pacote `[34]` = timestamp, seguido de 2 pontos de 4 bytes (`[35..38]`, `[39..42]`). Em cada ponto, o byte 0 tem bit 7 = 1 quando **não** há toque e bits 0–6 = id; X e Y têm 12 bits cada (X 0–1919, Y 0–941). Fonte: `hid-playstation` (Linux).

| Item | Resultado no controle do usuário (cabo USB) |
|---|---|
| Giroscópio | ✅ valores coerentes em repouso: (20, −54, −41) e, em outra leitura, (17, −57, −41) |
| Acelerômetro | ✅ (316, 15992, 2237): gravidade no eixo Y com o controle deitado. A escala bruta sugere ~16384 por g (o original costuma ter ~8192), mas a inclinação é calculada pela direção, então independe da escala |
| Touchpad | ✅ campos presentes; em repouso, "sem toque" (`0x80`) nos dois pontos. Movimento real a confirmar pelo usuário |
| **Alto-falante** | ❌ **Não acessível.** Pelo cabo o controle aparece **só como HID** (`USB\VID_054C&PID_05C4`, sem interface de áudio); o Windows não lista saída "Wireless Controller". No DS4 original o USB é composto, com áudio. Pelo Bluetooth o áudio do DS4 usa um fluxo proprietário que o Windows não suporta |

Decisão: o cartão "Alto-falante" **detecta** a saída de áudio do controle (WinRT `DeviceInformation`, classe AudioRender) e explica quando ela não existe. Tocar um som de teste fica no backlog (D4-11), para quando houver um controle com essa saída, porque não é possível validar aqui.

### 3.10.8 Validação do modo Xbox (XInput emulado) — 2026-11-13 ✅
| Item | Resultado |
|---|---|
| Criação do Xbox 360 virtual (ViGEmBus 1.21.442) | ✅ `045E:028E` no slot XInput 0; o WinRT o lista como "Controlador XBOX 360 para Windows" |
| Vibração pedida por um "jogo" (`XInputSetState` no slot virtual) → PS4 | ✅ o controle vibrou (cabo USB) |
| Entradas do PS4 → Xbox virtual | ✅ confirmado pelo usuário no painel "O que o jogo vê" (lê o slot virtual via XInput). O teste anterior com hora marcada (0 de 15 botões) foi atribuído a botões apertados fora da janela de 25 s |
| Duplicidade | ⚠️ como esperado sem HidHide, o WinRT lista o DS4 **e** o Xbox virtual |

### 3.10.9 Áudio do controle (alto-falante e microfone) — preparado para DS4 original — 2026-11-13
- **Implementação:** WASAPI via NAudio (MIT). O app procura os dispositivos ativos cujo nome contém "Wireless Controller" ou "DUALSHOCK" (saída e entrada), toca um tom de 440 Hz e mede o pico do microfone.
- **Validação do mecanismo** com `nova-probe audio-test --name Realtek` (dispositivos do PC): ✅ tom reproduzido até o fim no "Alto-falantes (Realtek(R) Audio)" (0,4 s, 801 ms incluindo a abertura); ✅ captura do "Grupo de microfones (Realtek(R) Audio)" funcionando (ruído ambiente ~0,003).
- **Controle do usuário:** `nova-probe audio-test` → nenhuma saída nem entrada do controle (coerente com §3.10.7). Na interface, os botões ficam desabilitados e o motivo é explicado.
- ⚠️ **Não validado em hardware real:** a detecção pelos nomes "Wireless Controller"/"DUALSHOCK" num DS4 original. Ela é coberta por testes automatizados (busca, maiúsculas/minúsculas, ausência) e a parte de tocar/captar foi validada em outros dispositivos.

## 4. Conclusão sobre a bateria (decisão S1-09)

| Fonte | Resultado |
|---|---|
| XInput | ❌ `WIRED`, sem dado |
| HID padrão (páginas 0x84/0x85) | ❌ não existe |
| WinRT `BatteryReport` | ❌ valor fixo 1000/1000 mWh, não muda ao carregar |
| HID vendor (`1040 MI_02` e `2106`), escuta passiva | ❌ silencioso em todos os estados |
| HID vendor com comando | 🟡 é a única possibilidade restante, mas o protocolo é desconhecido. Só com captura do software oficial (USBPcap) |

**Decisão: NO-GO para leitura de bateria na v1.0 pelos meios seguros.** O app exibirá **"Nível de bateria não informado pelo receptor"**, com uma explicação curta. A arquitetura mantém `IBatteryProvider`, para que um `HidVendorBatteryProvider` seja adicionado se o protocolo for documentado no futuro.

**O que o app PODE oferecer de forma honesta, como alternativa:**
- Estado de conexão em três níveis: receptor ausente, controle desligado, controle conectado.
- Tempo de uso desde a última conexão (medido pelo app, rotulado como tal, **sem** estimar porcentagem).

## 5. Como rodar o `nova-probe`

> Nesta máquina o projeto está num caminho com mais de 260 caracteres, e o Windows recusa executar binários a partir dele. Publique numa pasta curta:
> `dotnet publish tools/NovaControlCenter.Probe -c Release -o C:\nova-probe`

```
nova-probe scan                         # HID + XInput + WinRT, salva JSON
nova-probe listen 3 --seconds 120       # escuta passiva da interface [3] (MI_02)
nova-probe battery-log --minutes 60 --interval 30
nova-probe watch                        # estado XInput em tempo real
```

## 6. Pendências de validação (Sprint 01)

- [x] Confirmar enumeração: `3537:1040` composto com XInput + teclado + consumer + vendor.
- [ ] **Usuário confirmar** que o controle conectado é o T4 Nova Lite (e não outro GameSir).
- [ ] Ver se o PID muda conforme o modo do controle (combinações de botões de modo) e documentar a tabela de modos.
- [x] Ler os HID Report Descriptors.
- [x] Escuta passiva das interfaces vendor em todos os estados: silenciosas.
- [x] `XInputGetBatteryInformation` → WIRED.
- [x] `Windows.Gaming.Input` dá valor fixo e não muda ao carregar. Confirmação extra com bateria baixa: `battery-log` longo em andamento.
- [x] Significado do PID `0x2106`: receptor sem controle.
- [ ] Confirmar na interface: D-pad, Home e qual motor vibra (esquerdo, direito ou ambos).
- [ ] Verificar se o software oficial da GameSir suporta este modelo no PC; se sim, capturar o tráfego USB dele com USBPcap (captura passiva) para estudar o protocolo de `MI_02`.
