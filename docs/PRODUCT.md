# PRODUCT — Nova Control Center

## Visão
Aplicativo desktop Windows, independente do software oficial da GameSir, para **monitorar** e, dentro dos limites reais do hardware, **personalizar** o controle GameSir T4 Nova Lite conectado via receptor USB 2.4 GHz.

## Público
Jogadores de PC com o T4 Nova Lite que querem ver o estado do controle (conexão, bateria, entradas) e ajustar comportamento sem instalar o software do fabricante.

## Princípios de produto
1. **Honestidade de dados** — nunca exibir informação inventada. Se a bateria não puder ser lida, a UI diz isso explicitamente. XInput fornece *níveis* (vazio/baixo/médio/cheio), não porcentagem — não converter nível em %.
2. **Transparência de onde a configuração vive** — cada ajuste é rotulado como:
   - 🖥️ **Software (Windows)**: aplicado pelo app; some ao fechar o app ou em outro PC.
   - 🎮 **Firmware**: gravado no controle. Só existirá se o protocolo for documentado e validado.
3. **Segurança do dispositivo** — nenhum comando HID desconhecido é enviado. Nada de escrita em firmware sem protocolo validado.
4. **Offline-first** — funciona sem internet.
5. **Windows 10 como alvo primário.**

## Escopo v1.0
| Área | Escopo | Viabilidade |
|---|---|---|
| Dashboard | Nome/modelo, receptor (VID/PID), tipo de conexão, estado em 3 níveis (receptor ausente / controle desligado / conectado) | ✅ |
| Bateria | Mensagem clara "não informada pelo receptor"; tempo de uso desde a conexão (medido pelo app) | ❌ leitura real impossível pelos meios seguros (ver HARDWARE_DIAGNOSTICS §4); alertas adiados |
| Monitoramento | Botões, analógicos (visual 2D), gatilhos, valores brutos, taxa de atualização | ✅ via XInput |
| Vibração | Teste dos motores esquerdo/direito | ✅ via XInput |
| Configurações de software | Zona morta, curva de sensibilidade, zona morta e curso dos gatilhos, intensidade da vibração | ✅ **valem nos jogos para o PS4 no modo Xbox** (o app gera o controle virtual); GameSir: só pré-visualização (o jogo lê o controle direto); remapeamento ainda não implementado |
| Configurações de firmware | — | ❌ fora do v1.0 salvo descoberta de protocolo |
| Perfis | Criar, duplicar, renomear, excluir (com confirmação; o Padrão é protegido), ativar; perfis iniciais Padrão, FPS e Corrida | ✅ |

## Expansão de escopo — DualShock 4 (decidido em 2026-11-13)
Pedido do usuário: suportar também o controle de PS4 (DS4 via Bluetooth), apenas o básico.

| Recurso | Escopo | Como |
|---|---|---|
| Detecção e estado | DS4 conectado/desconectado, nome, conexão Bluetooth | HID `054C:05C4`/`09CC` |
| Monitoramento | Botões, analógicos e gatilhos na mesma tela | Relatório `0x01` (básico) ou `0x11` (estendido) |
| Bateria | Nível com **resolução de 10%** (exibido como aproximado), carregando / carga completa | Relatório `0x11`. **Só com "Recursos avançados" ligado** |
| Luz (lightbar) | Escolher a cor; desligar | Relatório de saída `0x11` com CRC-32. **Só com "Recursos avançados" ligado** |
| Modo de reconhecimento | **PS4 (nativo)** ou **XInput (Xbox 360 virtual)**, por escolha do usuário | ViGEmBus. 🖥️ Só funciona com o app aberto |

**Decisões do usuário (2026-11-13):**
1. O modo estendido do DS4 **só é ativado quando o usuário ligar "Recursos avançados"**. Desligado, o app não envia nada ao controle e lê apenas o relatório básico.
2. **Seguir sem HidHide.** No modo XInput, o app avisa que alguns jogos podem ver dois controles e detecta o HidHide se ele for instalado depois.

## Fora do escopo (v1.0)
- Atualização de firmware.
- Suporte a controles além do GameSir T4 Nova Lite e do DualShock 4.
- Bluetooth.

## Critério de sucesso
O usuário abre o app, vê em < 2 s se o controle está conectado, vê a bateria **ou** uma mensagem clara de indisponibilidade, e consegue testar todos os botões/eixos em tempo real.
