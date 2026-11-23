# ElevenLabs Sound Effects — referência verificada em 04/11/2026

POST https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_128
Autenticação: cabeçalho xi-api-key, obtido SOMENTE de ELEVENLABS_API_KEY no ambiente do processo de geração. Nenhuma credencial ou SDK no jogo.
Corpo: text (prompt exato), duration_seconds, loop, prompt_influence (0..1), model_id.
Modelo padrão: eleven_text_to_sound_v3. A visão geral recomenda v3; v2 está depreciado, com remoção prevista para 01/03/2027.

## Duração e formato
A referência do endpoint informa 0,5–30s, enquanto a visão geral informa 0,1–30s. Usamos o limite mais restrito na chamada. Os sete cliques de 0,2/0,4s recebem 0,5s na API e recorte local; duracao, api_duracao e ajuste registram essa decisão, sem reescrever os prompts.
MP3 44,1k/128 no Free/Starter; 192 no Creator ou superior; WAV 48k nos planos Pro ou superiores. O arquivo real é decodificado e verificado antes de substituir o processado anterior.
Concorrência conservadora 2 no Free e no máximo 3 nos demais planos; 429/5xx têm backoff e no máximo quatro tentativas. Autenticação/quota interrompem. Timeout de resposta mantém reserva de custo e NÃO repete automaticamente.

## Preço: fontes divergentes, reserva conservadora
A visão geral documenta 40 créditos/s com duração fixa; a FAQ de preços cita cerca de 200 créditos por geração. A página API também apresenta preço em USD/min, sem conversão confiável para créditos. Por isso, reservamos max(40 × duração API, 200) por chamada. O cabeçalho character-cost substitui a reserva pelo custo real informado pela API. Nenhuma conversão dólar/crédito foi inventada.
Nos três testes reais do v3, o cabeçalho informou 10 + 300 + 300 = 610 créditos. Isso não garante que o mesmo preço se aplique a toda conta/modelo no futuro. O custo ausente permanece estimado; nunca inferir custo zero de um contador ainda não atualizado.
Teto local padrão 30.000; parada antes de atingir 90% (27.000), incluindo reservas simultâneas. Saldo lido de GET /v1/user/subscription (character_limit - character_count). Lote completo acima do saldo não é iniciado. --budget-fit, autorizado pelo usuário depois do alerta de custo, seleciona um subconjunto com todas as variantes de cada item e no máximo 90% do saldo verificado. Não cria contas, não compra nem altera plano. O manifesto permite retomar manualmente.

## Licença
**Conta consultada: FREE. Efeitos não são CC0. O plano gratuito exige atribuição e tem restrição de uso comercial. Revisar a licença antes de distribuir comercialmente.** Não alteramos assinatura ou nome do jogo. Quatro apitos anteriores permanecem com sua proveniência própria.

## Fontes primárias
- https://elevenlabs.io/docs/api-reference/text-to-sound-effects/convert
- https://elevenlabs.io/docs/overview/capabilities/sound-effects
- https://elevenlabs.io/docs/api-reference/user/subscription/get
- https://elevenlabs.io/docs/help-center/technical/api-error-code-429
- https://elevenlabs.io/docs/overview/administration/billing
- https://elevenlabs.io/pricing/api
- https://elevenlabs.io/pricing

## Contador de saldo atrasado — prova real
Durante o lote, a API manteve used=8773 em consultas consecutivas enquanto novos cabeçalhos character-cost elevavam o ledger para9567. O script usa o menor saldo entre a consulta e a base de créditos verificada menos recibos/reservas posteriores. Esse guardrail impede reaproveitar créditos cujo débito ainda não apareceu. credit_baseline preserva o ponto inicial, sem identificador pessoal. Uma troca/renovação de conta precisa de nova base financeira explicitamente verificada, sem apagar o histórico de custos.
