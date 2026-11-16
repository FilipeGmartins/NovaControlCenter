# Checklist de testes manuais — v1.0.0

> A 1.0.0 foi publicada em 2026-11-16 por decisão do usuário antes da execução deste checklist; os resultados entram numa versão 1.0.x.

Itens que dependem do hardware real, da configuração do Windows ou da percepção do usuário.
Marque `[x]` e anote o resultado. O que já foi validado por automação está no fim.

## 1. GameSir T4 Nova Lite
- [ ] **Vibração:** em Monitoramento → Teste de vibração, "Pulso esquerdo" e "Pulso direito". **Qual lado vibra?** (já se sabe que só um motor responde) → ________
- [ ] **D-pad:** as 4 direções acendem no desenho? → ________
- [ ] **Home:** o botão Home acende? (pode ser reservado pelo receptor) → ________
- [ ] **Teste de botões:** chega a "15 / 15"? Se não, quais faltam → ________
- [ ] **Conexão:** desligar o controle → "Controle desligado"; tirar o receptor → "Receptor não encontrado"; recolocar → volta sozinho.

## 2. DualShock 4
- [ ] **Cabo USB:** status "PS4 conectado", bateria ≈ X% e "carregando".
- [ ] **Bluetooth:** com "Recursos avançados" ligado, a bateria aparece.
- [ ] **Luz (cabo):** as 7 cores e "Apagar luz" mudam a luz do controle.
- [ ] **Cor salva:** escolher uma cor, fechar e reabrir o app → a luz volta para ela.
- [ ] **Touchpad:** 1 e 2 dedos aparecem nas posições certas; o clique acende a borda.
- [ ] **Movimento:** inclinar o controle mexe o desenho; "Zerar" nivela.
- [ ] **Modo Xbox:** "O que o jogo vê" mostra botões e analógicos; num jogo XInput, o controle funciona.

## 3. Perfis (S5-07)
- [ ] Com o PS4 no modo Xbox, jogar com **FPS** e depois **Corrida**: a diferença na mira (curva) e nos gatilhos é perceptível?
- [ ] Criar, renomear, duplicar e excluir um perfil; fechar e reabrir → tudo continua lá.

## 4. Acessibilidade e Windows (S6-02)
- [ ] **Teclado:** com o app em foco, `Tab` percorre menu → conteúdo; o **contorno azul de foco** aparece; `Espaço`/`Enter` ativam botões; setas trocam itens do menu.
- [ ] **Escala de tela 125% e 150%** (Configurações do Windows → Tela): nada cortado, textos legíveis.
- [ ] **Leitor de tela (Narrador, `Ctrl+Win+Enter`):** lê nomes do menu, das abas, dos botões e das cores.
- [ ] **Maximizar/restaurar** e arrastar a janela pela barra de título; encaixar com `Win+←/→`.

## 5. Distribuição (S6-03)
- [ ] Copiar `publish/NovaControlCenter.exe` para outra pasta (ou outro PC sem .NET) e abrir.
- [ ] O Windows SmartScreen pode avisar (executável sem assinatura digital): "Mais informações" → "Executar assim mesmo".

## Já validado por automação (2026-11-16)
- ✅ 174 testes automatizados.
- ✅ Todas as páginas no **tamanho mínimo (960×620)** e maximizadas; corrigidos 2 cortes de texto.
- ✅ **Contraste WCAG AA:** texto ≥ 5,0:1 em todos os pares; texto branco sobre destaque passou de 3,2:1 para 5,0–5,8:1.
- ✅ **Leitor de tela:** todos os botões, interruptores e cores com nome; o conteúdo das abas passou a ser exposto (antes era invisível para leitores de tela).
- ✅ **Executável único** (75,6 MB, sem .NET instalado): abre, mostra a versão 1.0.0-rc.1, aplica as preferências e fecha sem erro.
- ⚠️ **Não verificável por automação:** ordem do `Tab` (o Windows impede que um processo em segundo plano pegue o foco) e escala de DPI (exige mudar a configuração de tela).
