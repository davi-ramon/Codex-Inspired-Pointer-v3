# Codex-inspired Pointer v3.2 — Windows 10/11

Conjunto independente de ponteiros inspirado no visual do Codex. **Não é um produto oficial da OpenAI.** O download e o uso são gratuitos.

## Instalar ou atualizar

1. Extraia o ZIP inteiro para uma pasta.
2. Abra `Configure.exe` com dois cliques.
3. Escolha o tamanho, a borda branca, a cor, o raio e a intensidade do brilho. Marque **Movimento e clique ao iniciar o Windows** se quiser o efeito de retorno.
4. Clique em **Aplicar ao Windows**. Se já tinha a versão anterior, este passo atualiza os arquivos da conta atual.

O ajuste inicial é **85%** de tamanho, **1,5 px** de borda e brilho azul com raio de 120% e intensidade de 130%. `Install.cmd` instala esse ajuste diretamente. A instalação vale somente para a conta atual do Windows e não requer administrador.

Para conferir cada ponteiro, abra `CursorTestPanel.exe` ou clique em **Testar ponteiros** no configurador. O painel inclui texto, carregamento, redimensionamentos, mover, links e um mapa de teste com mão aberta e fechada.

## O que mudou na versão 3.2

- Os `.cur` e `.ani` incluem imagens próprias de **32, 48, 64, 96, 128 e 256 px**. O Windows não precisa reduzir a folha inteira de 128 px para mostrar um cursor de 32 px, o que deixava a seta minúscula e serrilhada quando o auxiliar não estava ativo.
- O auxiliar atualiza somente a função visível, no máximo 30 vezes por segundo. Antes ele substituía várias funções a cada ciclo, causando piscadas entre versões de tamanhos diferentes.
- **Trabalhando** e **Ocupado** usam a animação `.ani` nativa de 30 quadros, sem substituição a cada ciclo do auxiliar.
- Cor azul, ciano, roxa ou branca, raio e intensidade do brilho podem ser ajustados no configurador. O símbolo **Indisponível** permanece vermelho; seu raio e intensidade acompanham os ajustes.
- O painel mostra se o auxiliar de movimento está ativo e permite testar os ponteiros em tamanho real.
- O painel inclui ponteiros próprios de **mão aberta** e **mão fechada** para o mapa de teste. Aplicativos de terceiros que desenham cursores próprios, como alguns mapas e páginas web, não obedecem ao esquema de ponteiros do Windows; seus gestos de arrastar podem continuar usando a arte do próprio aplicativo.

## Ajustes e restauração

Há tamanhos de 70%, 75%, 85%, 95%, 110%, 125% e 150%, com bordas de 1,3, 1,5 e 1,7 px. Cada combinação foi desenhada em alta resolução; o instalador cria as cores de brilho escolhidas na cópia instalada. A prévia no configurador mostra o desenho azul padrão do tamanho e da borda selecionados; a cor personalizada aparece depois de aplicar.

Para escolher o esquema pelo Windows, abra **Propriedades do Mouse → Ponteiros** e selecione **Codex-inspired Blue Glow 2**. Se a janela já estava aberta, feche e abra novamente.

Para restaurar os ponteiros anteriores, clique em **Restaurar anteriores** no configurador ou abra `Uninstall.cmd`. A instalação inicial guarda uma cópia das configurações de ponteiro da conta. A desinstalação também remove o iniciador e a tarefa de login.

## Arquivos

`Configure.exe` ajusta o pacote; `CursorTestPanel.exe` testa as funções; `CursorTheme.exe` gera o brilho escolhido nos arquivos instalados; `presets/` contém os 21 ajustes prontos; `Install.cmd` e `Uninstall.cmd` são atalhos. Os scripts PowerShell e arquivos C# são o código-fonte. O pacote pronto não exige Python, Pillow nem acesso à internet. Os executáveis e scripts não são assinados digitalmente; o Windows pode pedir confirmação para executá-los.

Os arquivos instalados ficam em `%LOCALAPPDATA%\Codex Pointer`. Os registros de diagnóstico `motion.log` e `motion-launcher.log` nessa pasta ajudam a identificar falhas de inicialização. Guarde o ZIP para alterar os ajustes ou reinstalar.
