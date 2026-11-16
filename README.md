<img src="tools/icon/NovaControlCenter-256.png" width="96" align="right" alt="Ícone do Nova Control Center" />

# Nova Control Center

Aplicativo para Windows que monitora e personaliza o **GameSir T4 Nova Lite** (receptor USB 2.4 GHz) e o **DualShock 4** (PS4), sem depender de software oficial.

> Versão **1.1.0**. Testes manuais com os controles seguem em [docs/TEST_CHECKLIST.md](docs/TEST_CHECKLIST.md).

## Requisitos
- Windows 10 (1809 ou mais recente) ou Windows 11, 64 bits.
- **Não precisa instalar o .NET**: o executável já inclui tudo.
- Opcional: [ViGEmBus](https://github.com/nefarius/ViGEmBus) para o modo Xbox do PS4.

## Como usar
1. Baixe o `NovaControlCenter.exe` em [Releases](https://github.com/FilipeGmartins/NovaControlCenter/releases), coloque em qualquer pasta e abra.
2. Conecte o receptor do GameSir e/ou o controle de PS4 (cabo USB ou Bluetooth).
3. Use o menu lateral: **Dashboard**, **Monitoramento**, **Controle PS4**, **Configurações**, **Perfis** e **Sobre**.

## O que ele faz
| Recurso | GameSir T4 Nova Lite | DualShock 4 |
|---|---|---|
| Detecção e status da conexão | ✅ receptor ausente / controle desligado / conectado | ✅ não encontrado / desligado / conectado |
| Botões, analógicos e gatilhos ao vivo | ✅ | ✅ |
| Bateria | ❌ o receptor não informa (investigado e documentado) | ✅ aproximada (passos de 10%) |
| Vibração | ✅ teste por motor | ✅ |
| Luz | — | ✅ pelo cabo USB |
| Touchpad, giroscópio e acelerômetro | — | ✅ |
| Alto-falante e microfone | — | ✅ quando o controle expõe áudio ao Windows (DS4 original, pelo cabo) |
| Modo Xbox (XInput emulado) | — | ✅ com ViGEmBus; só funciona com o app aberto |
| Perfis (zona morta, curva, gatilhos, vibração) | pré-visualização | ✅ valem nos jogos no modo Xbox |

**Segundo plano:** fechar ou minimizar esconde o app na bandeja (ao lado do relógio), usando cerca de 15–20 MB e quase nenhuma CPU; o PS4 no modo Xbox continua funcionando. Opções "Iniciar com o Windows" e "Abrir já minimizado" em Configurações. Para sair: botão direito no ícone → Sair.

Todas as escolhas (perfis, modo do PS4, cor da luz) ficam em `%AppData%\NovaControlCenter\settings.json`.

## Segurança e privacidade
- **Funciona offline.** Nenhum dado sai do computador.
- **Nada é gravado no firmware.** Os ajustes são aplicados por software, e a interface indica isso.
- **Nenhum comando desconhecido é enviado aos controles.** O DS4 só recebe comandos de protocolos públicos (driver Linux e SDL), e só com "Recursos avançados" ligado.
- Logs de diagnóstico: `%LocalAppData%\NovaControlCenter\logs` (botão na página Sobre).

## Para desenvolvedores
```
dotnet build
dotnet test
dotnet publish src/NovaControlCenter.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish
```
- .NET 8, WPF + MVVM (CommunityToolkit.Mvvm), Generic Host, Serilog, HidSharp, Nefarius.ViGEm.Client, NAudio.
- `tools/NovaControlCenter.Probe` (`nova-probe`): ferramenta de diagnóstico de hardware.
- Documentação: [produto](docs/PRODUCT.md) · [arquitetura](docs/ARCHITECTURE.md) · [diagnóstico de hardware](docs/HARDWARE_DIAGNOSTICS.md) · [backlog](docs/BACKLOG.md) · [changelog](docs/CHANGELOG.md).

## Limitações conhecidas
- A bateria do GameSir não é exibida porque o receptor não a informa ao Windows.
- Luz e vibração do PS4 pelo Bluetooth ainda não funcionam no controle testado (pelo cabo, sim).
- Sem o [HidHide](https://github.com/nefarius/HidHide), alguns jogos podem ver dois controles no modo Xbox.
- Os perfis não alteram o GameSir dentro dos jogos (o jogo lê o controle direto).

## Licença
A definir.
