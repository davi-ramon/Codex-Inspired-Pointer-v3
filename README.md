# Codex-inspired Pointer v3

Ponteiros para Windows 10 e 11 com contorno branco, brilho difuso, animações de espera e movimento opcional de clique e arraste. O conjunto tem 17 funções do Windows e 21 combinações prontas de tamanho e espessura da borda. O símbolo **Indisponível** usa brilho vermelho.

Este é um projeto independente, inspirado no visual do ponteiro do Codex. **Não é um produto oficial da OpenAI.** O download e o uso são gratuitos.

![Prévia dos ponteiros](Preview.png)

## Baixar e instalar

1. Na página da [versão v3.0.0](https://github.com/davi-ramon/Codex-Inspired-Pointer-v3/releases/tag/v3.0.0), baixe **Codex-Inspired-Pointer-Windows-v3.zip** em **Assets**. Use esse arquivo para instalar: o botão **Code → Download ZIP** baixa apenas o código-fonte, sem os ponteiros gerados.
2. Extraia o ZIP inteiro para uma pasta.
3. Abra **Configure.exe** com dois cliques. Escolha o tamanho, a borda e se deseja o movimento suave; clique em **Aplicar ao Windows**. Para instalar diretamente o ajuste padrão, abra **Install.cmd**.
4. Para trocar o esquema nas configurações do Windows, abra **Propriedades do Mouse → Ponteiros** e escolha **Codex-inspired Blue Glow 2**. Se essa janela já estava aberta, feche e abra novamente.

O ajuste padrão é **85%** de tamanho e **1,5 px** de borda. Há tamanhos de 70%, 75%, 85%, 95%, 110%, 125% e 150%, com bordas de 1,3, 1,5 ou 1,7 px. A instalação vale para a conta atual do Windows, não exige administrador e persiste após reiniciar. Quando ativado, o auxiliar de movimento inicia com a sessão.

Para voltar aos ponteiros anteriores, clique em **Restaurar anteriores** no configurador ou abra **Uninstall.cmd**. A primeira instalação guarda uma cópia das configurações de ponteiro da conta atual.

## O que vem no pacote

- Cursores para seta, ajuda, trabalho, ocupado, precisão, texto, manuscrito, indisponível, redimensionamentos, mover, seleção alternativa, link, localização e pessoa.
- Animações `.ani` para trabalho e ocupado. O movimento opcional suaviza clique e inclinação com retorno da seta e da mão.
- Arquivos `.cur` com imagens de 128 × 128 e 256 × 256 pixels. Cada ajuste é desenhado novamente em alta resolução para reduzir serrilhado; a ampliação global do Windows além dessas resoluções ainda pode reamostrar a imagem.
- Instalador, configurador, desinstalador e código-fonte. O uso do ZIP pronto não requer Python, Pillow nem internet.

O instalador usa `%LOCALAPPDATA%\Codex Pointer` para os arquivos ativos. O executável e os scripts não têm assinatura digital; o Windows pode pedir confirmação para executá-los.

## Código-fonte

O gerador visual está em [build_package.py](build_package.py) e precisa de Python 3 com Pillow para criar os presets. [Configurator.cs](Configurator.cs) e [CursorMotionV2.cs](CursorMotionV2.cs) contêm o configurador e o auxiliar de movimento. Os scripts PowerShell cuidam da instalação e restauração. Para editar o projeto, gere novamente as 21 combinações e compile o configurador antes de montar um novo ZIP. Os arquivos gerados ficam na Release para manter o repositório leve.

## Integridade do download

SHA-256 de `Codex-Inspired-Pointer-Windows-v3.zip`:

```text
0547DE14844C54F03358FAB475E9CD5317F8C696417D389969605C955985BEA6
```

No PowerShell, confira com `Get-FileHash .\Codex-Inspired-Pointer-Windows-v3.zip -Algorithm SHA256`.
