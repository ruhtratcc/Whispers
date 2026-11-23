# Gerar e importar os SFX

Python 3.10+ e ffmpeg com libvorbis e loudnorm. requirements.txt contém apenas biblioteca padrão; nenhum pip/SDK é necessário. No Windows, `winget install Gyan.FFmpeg` é uma opção; se já houver ffmpeg, reutilize-o. Confira `ffmpeg -version`. Alternativa: configure FFMPEG_BINARY com o caminho completo do executável.

Na raiz do projeto, em PowerShell:

```powershell
$env:ELEVENLABS_API_KEY = Read-Host 'Chave ElevenLabs (não compartilhar a janela)'
$env:FFMPEG_BINARY = 'C:/caminho/ffmpeg.exe'
python Tools/Audio/sfx_pipeline.py --dry-run
python Tools/Audio/sfx_pipeline.py --run --test
python Tools/Audio/sfx_pipeline.py --run --budget-fit
```

A chave é usada somente pelo processo offline. Não coloque a chave no código, Unity, manifesto, arquivo .env do projeto, captura ou relatório. Em versões recentes do PowerShell, prefira Read-Host -MaskInput. A conexão já autorizada nesta máquina é carregada pelo host a partir de seu cofre externo; não é preciso reconectar.

- Sem --run, nenhum POST pago ou consulta autenticada é feito. --dry-run prevalece até junto de --run.
- --test gera e verifica três sons (curto, loop longo e ambiente) e para; reexecutar depois dos três aprovados não os gera novamente.
- --run sem filtro tenta a lista inteira e para ANTES de gerar se estimativa não couber. --budget-fit gera somente um lote com margem de saldo. Para retomar, repita --run --budget-fit; o que está íntegro é ignorado.
- --only phone_notify ou --section 'PC retrô' força nova geração desses itens; cobra novamente. Primeiro conclua os três testes sem filtro. Todo o custo anterior permanece no ledger.
- --max-credits 30000 ou MAX_CREDITS define teto; atingir 90% para. Não exclua ledger/manifesto para esconder gastos. Limites da conta continuam valendo.
- Para outra lista, --list caminho. Nenhum prompt é reescrito. Nomes devem ser minúsculos e únicos. Cabeçalhos com e sem ## são aceitos; influência aceita vírgula.
- Pare ao ver bloqueio de saldo/autenticação; não use repetição infinita. O lock inclui PID; só remova um lock antigo após conferir que o processo terminou.

## Importação
Com Unity aberto, limpo e fora de Play, execute **Tools > Áudio > Importar SFX**. O script tenta também esse mesmo menu ao terminar uma geração isolada ou ao parar uma sequência de lotes por saldo se a Unity CLI estiver disponível. Se o Editor estiver jogando, compilando, com cena suja ou desconectado, nenhuma cena é alterada; use o menu depois.
O importador exige TODAS as variantes processadas do som antes de mudar seu SoundSet ou slot. Liga somente o mapa explícito sfx_bindings.json, preservando os GUIDs e outros campos de assets existentes. Faz backup em Assets/_Substituidos/SfxBatch20261104. O catálogo reutiliza SoundSet.Next e AudioManager/pool/mixers existentes. Um slot de AudioClip recebe somente a primeira variante, registrando isso.
Não há novos gatilhos de gameplay. Sons sem slot permanecem no catálogo e no relatório Sons_Sem_Slot.md. SFX não substituem vozes nem músicas. Fontes 3D são mono e usam rolloff logarítmico; ambiências/rádio/PC/UI usam configuração explícita. Configurável em Tools/Audio/sfx_config.json: volumes, pitch, LUFS, prioridades, categorias.

## Processamento e segurança
Brutos: Assets/Audio/Raw/Editor. A pasta Editor e a ausência de referências runtime os excluem do Player; .gitignore sozinho NÃO exclui arquivos de um build. Processados: Assets/Audio/SFX/<categoria> e Assets/Audio/Ambience.
Curtos WAV 48k/16bit, pico alvo -3 dBFS, fades de 7ms; silêncio longo só nas bordas, mantendo 10ms. Longos >8s/loops: OGG q6. Ambiência -30 LUFS, drones -32, respiração/batimentos -28. Loops não têm silêncio recortado; emenda compara salto, correlação e energia, com crossfade circular de 50ms se suspeita. Essa heurística NÃO substitui escuta. Um loop ainda suspeito não substitui o processado anterior.
O manifesto guarda hashes, duração, RMS, pico, emendas, parâmetros, recibos e estado por variante; o ledger registra custo real ou reserva conservadora. Um bruto recebido e com hash íntegro é reutilizado após falha de processamento, sem pagar outra chamada. --only/--section são regenerações intencionais.

## Testes locais
```powershell
python -m unittest discover -s Tools/Audio -p test_sfx_pipeline.py -v
```
Na Unity, testes EditMode SfxCatalogTests e verificação nativa do importador. Nada toca o save pessoal. Escuta, timbre, balanço final e campanha inteira: NÃO VERIFICADO automaticamente.

Regerar uma única variante: --run --only flashlight_on_02. Clipe bruto rejeitado por QA fica marcado requires_review, sem repetição paga automática; após conferir o motivo, --only autoriza uma nova chamada. Os demais pendentes continuam normalmente. Os contadores da API podem demorar: o saldo efetivo também desconta todos os recibos/reservas desde a base verificada. Não apagar essa base ou ledger para continuar gerando.
