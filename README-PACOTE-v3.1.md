# Codex-inspired Pointer v3.1 — Windows 10/11

Conjunto independente de ponteiros inspirado no visual do Codex. **Não é um produto oficial da OpenAI.** O download e o uso são gratuitos.

## Instalar ou atualizar

1. Extraia o ZIP inteiro para uma pasta.
2. Abra `Configure.exe` com dois cliques.
3. Escolha o tamanho, a borda branca e marque **Movimento e clique ao iniciar o Windows**.
4. Clique em **Aplicar ao Windows**. Se já tinha a versão anterior, este passo atualiza os arquivos da conta atual.

O ajuste inicial é **85%** de tamanho e **1,5 px** de borda. `Install.cmd` instala esse ajuste diretamente. A instalação vale somente para a conta atual do Windows e não requer administrador.

Para conferir cada ponteiro, abra `CursorTestPanel.exe` ou clique em **Testar ponteiros** no configurador. O painel inclui texto, carregamento, redimensionamentos, mover, links e um mapa de teste com mão aberta e fechada.

## O que mudou na versão 3.1

- O auxiliar aplica a mesma escala às **17 funções do Windows**, inclusive texto, ajuda, ocupado e redimensionamentos. O retorno suave acompanha a função ativa durante movimentos rápidos e cliques.
- O carregamento de **Trabalhando** e **Ocupado** continua animado, com 30 quadros por segundo.
- Um iniciador acompanha o auxiliar e o reabre se ele parar inesperadamente. Há inicialização pela conta do Windows e uma tarefa de login com atraso de 20 segundos como segunda entrada.
- O painel mostra se o auxiliar de movimento está ativo e permite testar todos os ponteiros em tamanho real.
- O painel inclui ponteiros próprios de **mão aberta** e **mão fechada** para o mapa de teste. Aplicativos de terceiros que desenham cursores próprios, como alguns mapas e páginas web, não obedecem ao esquema de ponteiros do Windows; seus gestos de arrastar podem continuar usando a arte do próprio aplicativo.

## Ajustes e restauração

Há tamanhos de 70%, 75%, 85%, 95%, 110%, 125% e 150%, com bordas de 1,3, 1,5 e 1,7 px. Os arquivos `.cur` trazem imagens de 128 × 128 e 256 × 256 pixels, geradas separadamente para cada ajuste. O brilho do cursor **Indisponível** é vermelho; os demais usam azul.

Para escolher o esquema pelo Windows, abra **Propriedades do Mouse → Ponteiros** e selecione **Codex-inspired Blue Glow 2**. Se a janela já estava aberta, feche e abra novamente.

Para restaurar os ponteiros anteriores, clique em **Restaurar anteriores** no configurador ou abra `Uninstall.cmd`. A instalação inicial guarda uma cópia das configurações de ponteiro da conta. A desinstalação também remove o iniciador e a tarefa de login.

## Arquivos

`Configure.exe` ajusta o pacote; `CursorTestPanel.exe` testa as funções; `presets/` contém os 21 ajustes prontos; `Install.cmd` e `Uninstall.cmd` são atalhos. Os scripts PowerShell e arquivos C# são o código-fonte. O pacote pronto não exige Python, Pillow nem acesso à internet. O executável e os scripts não são assinados digitalmente; o Windows pode pedir confirmação para executá-los.

Os arquivos instalados ficam em `%LOCALAPPDATA%\Codex Pointer`. Os registros de diagnóstico `motion.log` e `motion-launcher.log` nessa pasta ajudam a identificar falhas de inicialização. Guarde o ZIP para alterar os ajustes ou reinstalar.
