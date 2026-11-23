# Whispers of Past — revisão de áudio e objetos

O pacote desta revisão melhora o áudio dos sistemas existentes e o acabamento do escritório. O telefone continua visual; ligações, vozes narrativas e outros capítulos ficam para uma etapa posterior. Nenhum modelo Meshy foi usado.

## Áudio

São 66 clipes organizados em 27 SoundSets. Os passos variam entre madeira, concreto, terra, grama, metal, piso cerâmico e tapete. As gravações têm variação discreta de volume e pitch, sem repetir imediatamente o mesmo clipe quando existem alternativas.

Portas fazem rangido ao mover e batida ao chegar ao batente. Uma porta bloqueada pelo jogador permanece silenciosa até conseguir se mover. A gaveta usa gravações próprias; o deslizamento e a batida final vêm de trechos distintos da mesma gravação, sem duplicação. A lanterna tem sons diferentes para ligar e desligar. Papel, chaves e impactos usam efeitos adequados ao objeto.

O áudio 3D desaparece gradualmente com a distância. Impactos consideram a velocidade contra a superfície, evitando pancadas fortes causadas apenas por deslizamento. Aterrissar não reproduz um passo extra ao mesmo tempo.

As duas faixas de música mantêm estéreo e são carregadas por streaming. As preferências de volume do menu continuam valendo. A respiração provisória, o chiado contínuo e as vozes artificiais anteriores permanecem desligados. Os sons de seleção e confirmação do menu também são substituídos por efeitos orgânicos discretos.

Fontes e licenças estão no arquivo `work/AudioPolish/CREDITS.txt`, no manifesto e nos avisos originais. O importador copia os créditos também para `Assets/StreamingAssets/WhispersOfPast-Audio-Credits.txt`, disponível junto de uma futura build.

- [Various Sound Effects, laleksic](https://opengameart.org/node/143670): gravações domésticas, CC0.
- [UI SFX, Kenney](https://opengameart.org/content/51-ui-sound-effects-buttons-switches-and-clicks): efeitos orgânicos, CC0.
- [Different steps, TinyWorlds](https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud): gravações de passos editadas, CC0.
- [Impact Sounds, Kenney](https://kenney.nl/assets/impact-sounds): impactos e passos, CC0; o método de gravação não é especificado.
- [Footsteps on different surfaces, congusbongus](https://opengameart.org/content/footsteps-on-different-surfaces): metal e piso cerâmico, CC BY 3.0. Os créditos incluem os autores originais Eelke, swuing e ceberation, links e alterações.
- [Lost in a bad place, congusbongus](https://opengameart.org/content/lost-in-a-bad-place-horror-ambience-loop) e [Post Apocalyptic Wastelands, Juhani Junkala/SubspaceAudio](https://opengameart.org/node/79273): composições para atmosfera, CC0.

## Objetos

O telefone tem carcaça inclinada com bordas arredondadas, receptor curvo com auriculares, encaixes, disco com dez furos reais e fio helicoidal contínuo. A gaveta tem interior aberto, frente, puxador, trilhos e papéis que acompanham o movimento. Os gaveteiros ficam ocos na região da gaveta.

A caneca tem interior oco e a caneta recebe melhor acabamento. Mesa, computador, cadeiras, relógio, arquivos, janela e lixeira mantêm as correções da revisão anterior. Mural de casos e calendário permanecem simples.

## Aplicar no projeto

Com `Chapter1Office` aberta e fora do Play, execute no PowerShell:

```powershell
unity command eval_file "C:\Users\ruhtra\Documents\Codex\2026-09-29\oi\work\ApplyGamePolish.cs" --project-path "C:\Users\ruhtra\My project" --caller plugin --skill unity-cli --timeout 120 --format json
```

O importador faz backup dos arquivos existentes em `work/AudioPolish/Backups`, mantém os arquivos meta/GUIDs e importa as gravações e scripts. Um pedido explícito aplica as mudanças após compilar, salva o escritório, o laboratório e o menu, gera prévias e executa os testes no Editor. O Unity entra em Play durante os testes e depois volta a Chapter1Office. Reloads normais não reaplicam a revisão.

As gravações ficam em `Assets/HorrorGameplay/Audio/Recorded/Clips`; os conjuntos em `Recorded/Sets`. Os conjuntos antigos recebem as novas referências para preservar conexões existentes. Porta, gaveta e documentos mantêm IDs persistentes. A lanterna mantém intensidade 90 e começa desligada no escritório.

## Verificação

A preparação foi compilada contra a versão instalada do Unity, sem erros ou avisos. Os novos modelos tiveram 10.756 triângulos verificados, sem faces invertidas ou degeneradas; as folgas e a direção da gaveta foram conferidas. Os clipes passaram nas verificações de hash, referências, formato WAV PCM16, clipping, DC e bordas.

Depois da aplicação, os relatórios ficam em `Assets/ChapterOneOffice/Documentation`:

- `Office-polish-apply.json`: aplicação e agendamento dos testes.
- `Audio-polish-report.json`: referências, superfícies, lanterna e preservação dos sons desligados.
- `Office-detail-polish-report.json`: modelos, geometria, folgas e prévias.
- `Office-polish-tests.json` e XMLs: resultados EditMode e PlayMode, incluindo porta bloqueada silenciosa, fecho no batente e restauração sem reproduzir áudio.

As imagens previstas são `Office-polished-phone.png`, `Office-polished-drawer-open.png` e `Office-polished-desk.png`. A gravação `outputs/Audio-foley-preview.wav` demonstra apenas os Foley; o volume e a distância no jogo dependem da mixagem.

**Estado em 01/10/2026:** pacote aplicado e cenas salvas. Validações de áudio e modelos passaram. A fixture de movimento foi corrigida durante a revisão de apresentação; repetição concluída às 09:52 UTC com **5/5 EditMode e 14/14 PlayMode**, sem falhas. Não foi feita build Windows nesta revisão. O usuário depois dispensou novos testes da porta e salinha; os ajustes adicionais de interruptor e poste não repetem esses testes.
