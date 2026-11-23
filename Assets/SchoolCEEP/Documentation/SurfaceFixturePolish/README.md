# Acabamentos e luminárias — aplicação de 11/10 e revisão de 12/10/2026

Aplicação local em `Assets/SchoolCEEP/Scenes/SchoolCEEP.unity`: 256 modelos de luminárias, sendo 243 fluorescentes e 13 arandelas. Duas luminárias antigas sem suporte adequado foram removidas. O pendente industrial foi preparado e importado, mas nenhuma instância foi colocada: as condições reais de teto e altura livre selecionaram fluorescentes como alternativa.

Os acabamentos abrangem 90 renderers distintos, 15.516 triângulos existentes e 22 variantes de materiais, conforme `surface-applied.json` atualizado. Foram aplicadas seis famílias: reboco pintado, pintura azul, azulejos, forro interno, madeira e cobertura metálica. A revisão incluiu dois painéis existentes, acrescentando 18 triângulos: pintura azul em 25 renderers/1.248 triângulos e azulejos em 16 renderers/104 triângulos. `red_plaster_weathered` está apenas preparado. Um renderer pode usar mais de uma família; as contagens por família não devem ser somadas como renderers únicos.

Validação concluída em 12/10/2026: `fixture-final-audit.json` aprova os suportes das 256 luminárias. Após reaplicar os acabamentos dos dois painéis, o passe noturno e reabrir a cena, `combined-final-audit.json` aprova a auditoria conjunta: zero faces antigas restantes, geometria e estado protegido preservados, UVs métricas conferidas e zero novas luzes, colliders ou scripts. Reaplicar os três passes apenas auditou os journals, sem duplicar objetos. O teste integrado da abertura passou, 1/1, em aproximadamente 31,2 s; checkpoint e preferências foram restaurados. A cena final fica limpa, fora de Play, com uma câmera, um jogador e uma sessão; zero scripts, meshes ou materiais ausentes, erros de shader/narrativa, IDs vazios/duplicados ou inimigos.

## Fontes e licença

Todos os assets são [CC0 1.0 do Poly Haven](https://polyhaven.com/license), obtidos sem cadastro. Autores, fontes e adaptações também constam no `CREDITS.md` do workspace e na cópia `Assets/CREDITS.md`.

| Asset aplicado | Autor | Fonte |
|---|---|---|
| Industrial Wall Lamp | Kuutti Siitonen | [Poly Haven](https://polyhaven.com/a/industrial_wall_lamp) |
| Mounted Fluorescent Lights | Ulan Cabanilla | [Poly Haven](https://polyhaven.com/a/mounted_fluorescent_lights) |
| Painted Plaster Wall | Amal Kumar | [Poly Haven](https://polyhaven.com/a/painted_plaster_wall) |
| Blue Plaster Wall | Dimitrios Savva | [Poly Haven](https://polyhaven.com/a/blue_plaster_wall) |
| Long White Tiles | Jenelle van Heerden, Sergej Majboroda | [Poly Haven](https://polyhaven.com/a/long_white_tiles) |
| Ceiling Interior | Dimitrios Savva | [Poly Haven](https://polyhaven.com/a/ceiling_interior) |
| Wooden Planks | Charlotte Baglioni, Dario Barresi | [Poly Haven](https://polyhaven.com/a/wooden_planks) |
| Corrugated Iron | Dimitrios Savva, Jenelle van Heerden | [Poly Haven](https://polyhaven.com/a/corrugated_iron) |

Apenas preparados: [Hanging Industrial Lamp](https://polyhaven.com/a/hanging_industrial_lamp), de Kuutti Siitonen, e [Red Plaster Weathered](https://polyhaven.com/a/red_plaster_weathered), de Amal Kumar.

As três fontes glTF 1K somam 22 arquivos/8.886.246 bytes; os sete conjuntos PBR 1K somam 21 arquivos/13.937.518 bytes. Tamanho e MD5 publicado foram conferidos, com SHA256 local registrado em `work/SurfaceFixturePolish/ModelsResearch/provenance.json` e `work/SurfaceFixturePolish/Textures/provenance.json`. Originais em `assets/models/ambiente/<id>/source` e `assets/textures/<id>`; metadados da API e preparações permanecem no workspace.

## Integração e recuperação

Meshes e materiais são nativos do Unity, com LODs e mapas ARM convertidos para URP. Fluorescentes compartilham mapas e materiais. Vidro usa transparência alpha; emissão e reflexo ambiente ficam desligados. A geometria arquitetônica recebe UVs métricas nas faces selecionadas, sem superfícies sobrepostas. Os passes registram as referências originais e verificam componentes físicos, luzes e narrativa; a preservação do estado combinado passou na auditoria após reabrir a cena e foi novamente conferida após o teste em runtime.

Reaplicar um passe já aplicado chama sua auditoria, sem duplicar modelos. Para desfazer os acabamentos, a API exige primeiro `SchoolNightMaterialPass.Restore`, depois `SchoolSurfaceFinishPass.Restore`. O passe de luminárias possui `Apply`, `Preview` e `Audit`, sem método público `Restore`: a reversão desse estágio deve usar a cópia anterior da cena e seus journals correspondentes. Não apresentar uma chamada de restauração inexistente como procedimento disponível.

Backup anterior: `work/SurfaceFixturePolish/Backups/SchoolCEEP-before-fixtures.unity`. Os journals noturnos anterior/restaurado estão na mesma pasta. O ciclo anterior à revisão dos painéis foi preservado em `surface-journal-before-panels.json`, `surface-journal-restored-before-panels.json`, `night-journal-before-panels.json` e `night-journal-restored-before-panels.json`. As referências de batches originais removidos ficam em `Assets/SchoolCEEP/Models/SurfaceFixturePolish/FixtureReplacement-20261011-151546-646/SchoolBatchVisualReplacement.json`; o manifesto principal é `Assets/SchoolCEEP/Models/SurfaceFixturePolish/fixture-replacement-manifest.json`.

## Evidências disponíveis

- `native-import.json`: nove prefabs preparados, seis materiais compartilhados, 19 texturas e ausência de emissão/importador em runtime.
- `fixture-applied.json`: seleção, substituições, remoções, suportes e preservação registrada no momento da aplicação.
- `fixture-final-audit.json`: auditoria específica das 256 luminárias e comparação das assinaturas protegidas.
- `combined-final-audit.json`: auditoria conjunta persistida após reaplicar os três passes e reabrir a cena.
- `tests-opening-status.json`: teste `RealSchoolOpeningSupportsGateCluesSightlinesAndBlackout` aprovado, 1/1.
- `test-guard-restored.json`: checkpoint e preferências restaurados, escola reaberta.
- `final-SchoolCEEP.json`: inspeção final da cena limpa e fora de Play.
- `surface-applied.json` e `surface-journal.json`: faces, materiais, referências originais e contagens de acabamento.
- `night-rebased.json`: aplicação noturna sobre a nova base.
- `texture-copy-verification.json`, preparações e validações em `work/SurfaceFixturePolish`: integridade das fontes e cópias.
- Capturas `classroom-hall-before-*`, `classroom-hall-fixtures-only-*` e `classroom-hall-after-*`: comparação da câmera no Editor.
- `classroom-room-after-on.png`: inspeção do interior da sala com forro texturizado e fluorescentes fixadas; esta captura usa a lanterna acesa.

As evidências são de preparação, aplicação, inspeção no Editor e um teste integrado da abertura. Esse teste usa deslocamentos seguros entre locais; não comprova caminhada contínua por todo o mapa. Não há build PC de distribuição, medição de FPS ou playthrough humano desta revisão. História, campanha e qualidade final do jogo não são declaradas concluídas por essas contagens.
