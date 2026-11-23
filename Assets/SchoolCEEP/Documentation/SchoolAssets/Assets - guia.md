# Assets aprovados da escola

Os seis assets CC0 aprovados foram baixados diretamente do Poly Haven e integrados à cena `Assets/SchoolCEEP/Scenes/SchoolCEEP.unity`. São quatro modelos glTF e duas texturas PBR, todos com mapas 2K. Os 26 arquivos originais somam 31.885.095 bytes; tamanho, MD5 publicado e SHA256 local foram conferidos. Nome, autor, fonte e licença constam de `CREDITS.md` na raiz do workspace e em `Assets/CREDITS.md` do projeto Unity.

## O que mudou

- 457 conjuntos escolares receberam cadeira e carteira prontas; a cadeira do evento de percepção também usa o novo modelo, totalizando 458 cadeiras e 457 carteiras.
- 28 estantes dos arquivos/depósitos receberam o modelo metálico. As 84 caixas antigas, que atravessavam prateleiras, foram retiradas visualmente; há uma caixa pronta apoiada na prateleira inferior de cada estante.
- 56 agrupamentos da decoração de abandono receberam os materiais PBR de reboco deteriorado e ferrugem. As bordas do reboco têm transparência suave, sem placas retangulares sobre a parede.
- A biblioteca conserva seus livros e as estantes que os sustentam. O mapa, portas, luzes e narrativa existentes foram preservados.

Não foram baixados packs, HDRIs, monstros ou código de terceiros. O sedã próprio, menu, prólogo e prefab compartilhado mantêm seus hashes anteriores. Nenhum Capítulo 4 foi desenvolvido.

## Organização e conversão

Originais no workspace: `assets/models/mobiliario`, `assets/models/decoracao` e `assets/textures`. O projeto Unity usa prefabs e meshes nativos em `Assets/models`, com mapas em `Assets/textures`; não precisa de novo pacote para carregar glTF em runtime.

`download_assets.py` conserva os originais e registra a procedência. `prepare_assets.py` transforma coordenadas, winding e UVs para Unity, centraliza o pivot na base, prepara um LOD distante e converte ARM em metallic/smoothness e occlusion do URP. A escala do glTF da estante é corrigida para as dimensões em metros publicadas, sem alterar o arquivo original.

`SchoolReadyAssetPass` cria quatro prefabs compartilhados, seis materiais e instala apenas a parte visual. `SchoolBatchVisualReplacement` identifica todos os três vértices de cada face original, com tolerância de 0,1 mm, antes de remover qualquer índice. A prova encontrou 586.828 de 586.828 triângulos em 8.991 peças. Seis agrupamentos receberam clones dos meshes; os assets originais ficam intactos e suas referências constam do relatório. Nenhuma parede foi cortada por aproximação de bounds.

Os 457 conjuntos novos cabem nos mesmos volumes físicos antigos. Nenhum collider novo foi acrescentado. A cadeira móvel mantém Transform, posição, ID persistente e mecanismo de restauração; o modelo é somente seu filho visual. A assinatura física da arquitetura é idêntica antes e depois.

## Renderização e limites da prova

Meshes e materiais compartilhados, GPU instancing habilitado, mapas comprimidos com mipmaps, dois níveis de LOD e descarte distante. Os modelos somam 4.821.410 triângulos se todas as instâncias usarem LOD0 simultaneamente; em LOD1 somam 2.671.008. Essas são contagens da cena inteira, não medição de FPS. Não foi feito build de PC ou teste de desempenho em hardware médio.

Prévias nativas revisadas: `classroom.png`, `classroom-lod1.png`, `storage.png`, `event-chair.png` e `plaster.png`. A última usa temporariamente luz mais fraca para inspecionar o reboco; a lanterna de gameplay não mudou. Câmera, iluminação e objetos temporários são restaurados após as capturas.

## Como testar

Validação concluída: compilação nativa sem erros, seis materiais com mapas válidos e sem erros de shader, meshes/LODs válidos, movimento e restauração da cadeira preservados. O teste PlayMode integrado `SchoolOpeningScene` passou em 52,99 s. Save e preferências foram restaurados, o guard foi encerrado e a escola voltou salva/limpa, fora de Play. A instalação repetida confirmou 971 instâncias sem duplicação.

1. Abra `SchoolCEEP` no projeto Unity e inicie Play.
2. Siga a abertura pela entrada, Secretaria, Sala 02 e Sala 03. Confira documentos, fotografia, quadro, criança, desenho e apagão.
3. Observe cadeiras/carteiras de perto e à distância; circule pelas mesmas passagens. Nos depósitos, confira apoio das caixas e prateleiras.
4. Após a fotografia, a cadeira da memória pode mudar quando João estiver olhando para outro lado. A retomada do save conserva sua pose.

Para repetir testes automatizados, execute `SchoolOpeningTestGuard.Begin()` antes de Play, rode a categoria `SchoolOpeningScene` e encerre com `SchoolOpeningTestGuard.End()` fora de Play. O guard protege arquivos de save e preferências existentes. A sequência integrada usa posições apoiadas com Warps; não prova uma caminhada contínua de todo o mapa.

`SchoolMapBuilder` reaplica os assets preparados em uma futura reconstrução explicitamente solicitada. Não reconstrua o mapa para testar esta entrega. A instalação na cena atual já está salva; uma chamada repetida a `SchoolReadyAssetPass.Install()` apenas audita o grupo existente. Cada cena reconstruída recebe novos clones reversíveis de meshes.

Provas: `provenance.json`, `conversion.json`, `native-import.json`, `batch-replacement.json`, `installation.json`, `details.json`, `files-proof.json`, `opening-test.json` e `final-scene.json`.
