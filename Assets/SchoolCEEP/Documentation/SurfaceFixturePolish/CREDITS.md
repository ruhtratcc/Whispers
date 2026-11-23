# Créditos — assets gratuitos da escola

Seleção inicial aprovada pelo usuário. Todos os assets desta primeira tabela são CC0 1.0, obtidos diretamente do Poly Haven sem login.

| Asset | Autor | Fonte | Licença | Finalidade |
|---|---|---|---|---|
| School Chair 01 | Ethan Place | https://polyhaven.com/a/SchoolChair_01 | CC0 1.0 | Cadeiras escolares |
| School Desk 01 | Ethan Place | https://polyhaven.com/a/SchoolDesk_01 | CC0 1.0 | Carteiras escolares |
| Steel Frame Shelves 01 | James Ray Cock | https://polyhaven.com/a/steel_frame_shelves_01 | CC0 1.0 | Estantes dos arquivos e depósitos |
| Cardboard Box 01 | Rahul Chaudhary | https://polyhaven.com/a/cardboard_box_01 | CC0 1.0 | Caixas antigas nos arquivos e depósitos |
| Damaged Plaster | Amal Kumar | https://polyhaven.com/a/damaged_plaster | CC0 1.0 | Reboco deteriorado |
| Rusty Metal 03 | Amal Kumar | https://polyhaven.com/a/rusty_metal_03 | CC0 1.0 | Detalhes de ferrugem |

Licença: https://polyhaven.com/license

Os arquivos originais permanecem em assets/models e assets/textures. A integração cria meshes e materiais URP nativos a partir do glTF, sem executar código dos downloads. As conversões de mapas PBR e ajustes locais são específicos deste projeto.

Os créditos anteriores de áudio, lanterna e demais elementos continuam nos arquivos CREDITS.md dos respectivos capítulos em outputs. Nenhum desses materiais foi substituído nesta etapa.

## Props aprovados para o refinamento

Seleção adicional confirmada pelo usuário: glTF e texturas 1K, com tamanho, MD5 publicado e SHA256 local conferidos.

| Asset | Autor | Fonte | Licença | Finalidade |
|---|---|---|---|---|
| Office Notepads | Ulan Cabanilla | https://polyhaven.com/a/office_notepads | CC0 1.0 | Cadernos e papéis esquecidos nas salas |
| Book Encyclopedia Set 01 | John Malcolm | https://polyhaven.com/a/book_encyclopedia_set_01 | CC0 1.0 | Livros antigos espalhados nas salas e biblioteca |

Originais preservados em assets/models/decoracao. Meshes estáticos e materiais URP nativos preparados localmente; variantes individuais e LODs derivados mantêm o crédito dos modelos originais. Nenhum código dos downloads é executado.

## Abandono e presenças — seleção aprovada em09/10/2026

Somente seis mapas JPG1K e dois GLBs,4.903.004bytes, com procedência/tamanhos/SHA256 registrados em work/AbandonmentPolish/SourceEvidence/provenance.json.

| Asset | Autor | Fonte | Licença | Finalidade |
|---|---|---|---|---|
| Worn Cracked Plaster | Amal Kumar | https://polyhaven.com/a/worn_cracked_plaster | CC0 1.0 | Reboco deteriorado e superfícies úmidas |
| Worn Mossy Plasterwall | Amal Kumar | https://polyhaven.com/a/worn_mossy_plasterwall | CC0 1.0 | Reboco deteriorado e superfícies úmidas |
| Ceiling Fan | CreativeTrio | https://poly.pizza/m/pRMxaQQXha | CC0 1.0 | Ventilador parado |
| Little girl | Jair Trejo | https://poly.pizza/m/5ihpeSlkOkt | CC-BY 3.0 | Presença infantil imóvel e imaterial |

Little girl: ©Jair Trejo, modelo original em https://poly.pizza/m/5ihpeSlkOkt, licenciado sob Creative Commons Attribution3.0 (https://creativecommons.org/licenses/by/3.0/). Adaptações previstas: conversão para meshUnity estático com pivot inferior, escala infantil e materiais escuros/desaturados para aparição. Não é personagem rigado/animado; nunca recebe perseguição, dano ou colisão física. As adaptações não implicam apoio do autor ao jogo. Registrar o estado efetivamente integrado no relatório do passe.

Originais preservados em assets/models e assets/textures; mapas ARM convertidos para metallic/smoothness e occlusion do URP. Não executar código dos assets. O modelo Little girl foi rejeitado visualmente pelo usuário em10/10/2026 e não foi integrado: as aparições anteriores continuam. O Ceiling Fan da Poly Pizza foi substituído na seleção pelo modelo Poly Haven abaixo.

## Prioridade Poly Haven — autorização aberta em10/10/2026

O usuário dispensou novas confirmações para downloads e pediu a substituição dos props mais simples. Quatro modelos glTF1K,27 arquivos/9.942.447bytes, com tamanho/MD5 publicado/SHA256 conferidos em work/AbandonmentPolish/PolyHavenProps/provenance.json.

| Asset | Autor | Fonte | Licença | Finalidade |
|---|---|---|---|---|
| Ceiling Fan | Ulan Cabanilla | https://polyhaven.com/a/ceiling_fan | CC0 1.0 | Ventiladores estáticos sem emissão elétrica |
| Wall Clock | PierreB3D | https://polyhaven.com/a/wall_clock | CC0 1.0 | Relógios parados com volume, vidro e mostrador |
| Metal Trash Can | GurJas Studios | https://polyhaven.com/a/metal_trash_can | CC0 1.0 | Variante enferrujada nas salas |
| Plastic Broom | L | https://polyhaven.com/a/plastic_broom | CC0 1.0 | Vassouras esquecidas em cantos livres |

Adaptações locais: conversão de handedness/UVs para Unity, pivot inferior, ARM para URP, cerdas com alpha mask do próprio material, LODs derivados e emissão desativada. Originais preservados; meshes e materiais nativos compartilhados, sem importador em runtime. Uso real e contagens ficam registrados no relatório de integração.

Integração final: 11 relógios, 27 ventiladores, 5 lixeiras enferrujadas e 3 vassouras. Mais 21 caixas Cardboard Box 01, já creditadas acima, nas bordas das circulações; uma caixa foi retirada para liberar a passagem baixa da cantina. Ventiladores permanecem parados, coerentes com a escola sem energia. Os relógios substituem os mostradores planos anteriores, mantendo a posição na parede.

Paredes amplas usam o conjunto PBR Worn Mossy Plasterwall com cores antigas preservadas, repetição de aproximadamente 2,77 m e relevo discreto. Worn Cracked Plaster permanece como fonte preservada; não é o albedo das paredes amplas na versão final. Vidro transmissivo do relógio foi adaptado ao URP Lit por transparência alpha suave, sem refração física. Reflexo do céu foi desativado nos materiais selecionados da escola; metais mantêm resposta à luz direta da lanterna.

## Acabamentos e luminárias Poly Haven — 11/10/2026

Todos os assets abaixo são CC0 1.0 (https://polyhaven.com/license), obtidos sem cadastro. Originais, metadados da API, tamanhos, MD5 publicado e SHA256 local estão preservados em work/SurfaceFixturePolish. Sete conjuntos PBR 1K: 21 arquivos originais/13.937.518 bytes. Três fontes glTF de luminárias: 22 arquivos originais/8.886.246 bytes.

| Asset | Autor | Fonte | Uso |
|---|---|---|---|
| Painted Plaster Wall | Amal Kumar | https://polyhaven.com/a/painted_plaster_wall | Reboco pintado e pilares |
| Blue Plaster Wall | Dimitrios Savva | https://polyhaven.com/a/blue_plaster_wall | Pintura azul de alvenaria |
| Red Plaster Weathered | Amal Kumar | https://polyhaven.com/a/red_plaster_weathered | Pintura terracota deteriorada |
| Long White Tiles | Jenelle van Heerden, Sergej Majboroda | https://polyhaven.com/a/long_white_tiles | Faixas de azulejos, mantendo as cores da escola |
| Ceiling Interior | Dimitrios Savva | https://polyhaven.com/a/ceiling_interior | Forros e lajes internas |
| Wooden Planks | Charlotte Baglioni, Dario Barresi | https://polyhaven.com/a/wooden_planks | Forro de madeira |
| Corrugated Iron | Dimitrios Savva, Jenelle van Heerden | https://polyhaven.com/a/corrugated_iron | Coberturas metálicas |
| Industrial Wall Lamp | Kuutti Siitonen | https://polyhaven.com/a/industrial_wall_lamp | Arandelas fixadas em paredes |
| Mounted Fluorescent Lights | Ulan Cabanilla | https://polyhaven.com/a/mounted_fluorescent_lights | Sete variantes de luminárias, incluindo tubos ausentes |
| Hanging Industrial Lamp | Kuutti Siitonen | https://polyhaven.com/a/hanging_industrial_lamp | Pendentes com corrente e cabo completos |

Adaptações: albedo neutralizado para preservar a pintura existente, UVs em escala métrica, ARM convertido para metallic/smoothness e AO do URP, normais OpenGL importadas nativamente; meshes das luminárias convertidos para Unity com pivôs de montagem e LOD distante. Materiais sem emissão e reflexo ambiente, vidro por alpha suave sem refração física. A integração conserva os objetos Light, colliders, pistas e progressão existentes. Relatórios de uso efetivo: outputs/SurfaceFixturePolish; assets apenas preparados não devem ser apresentados como colocados na cena.

Uso efetivamente registrado em 11/10, revisado e validado em 12/10/2026: 256 luminárias na SchoolCEEP, sendo 243 variantes de Mounted Fluorescent Lights e 13 Industrial Wall Lamp. Duas luminárias procedurais sem suporte adequado foram removidas. Hanging Industrial Lamp foi preparado/importado, com zero instâncias colocadas; a alternativa fluorescente foi usada onde o pendente não atendia ao teto e à altura livre. Seis famílias PBR foram aplicadas em 90 renderers distintos/15.516 triângulos existentes, com 22 variantes de materiais; os dois painéis incluídos na revisão acrescentaram 18 triângulos. Red Plaster Weathered permanece apenas preparado, sem faces atribuídas neste passe. Os relatórios estão em outputs/SurfaceFixturePolish: as auditorias específica das luminárias e conjunta após reaplicar os três passes e reabrir a cena passaram, com geometria e estado protegido preservados e zero novas luzes, colliders ou scripts. O teste integrado da abertura passou, 1/1, com checkpoint e preferências restaurados; a auditoria conjunta foi reconferida depois dele. Inspeção final: cena limpa, fora de Play, uma câmera/jogador/sessão e zero componentes visuais ausentes, erros de shader/narrativa, IDs vazios/duplicados ou inimigos. O teste usa deslocamentos seguros e não comprova caminhada contínua de todo o mapa; sem build PC, medição de FPS ou playthrough humano desta revisão.
