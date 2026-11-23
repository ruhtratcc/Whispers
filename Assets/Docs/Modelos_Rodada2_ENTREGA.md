# Modelos — Rodada 2 concluída

Os dez pacotes fornecidos em `Downloads/modelos tcc` foram integrados nas cenas existentes. Nao houve download de novos modelos. Sao130 instancias de modelos novos:4 no escritorio,2 na estrada,124 na escola. Uma caixa Poly Haven ja local apoia a lanterna no carro.

|Modelo|Uso efetivo|
|---|---|
|Sedã Baird Silver Coronet|1 carro da estrada; sem placas de teto, taximetros, divisoria e pintura de taxi; cabine alinhada as ancoras originais|
|Telefone de disco|1 telefone do escritorio; interacao e audio preservados|
|Cadeira de escritorio|1 cadeira de Joao; altura1,12m e frente para a mesa|
|Arquivo metalico|2 unidades; uma variante fechada do pacote selecionada|
|Smartphone|1 celular na estrada, deitado no apoio; coleta/vibracao/controle preservados|
|Quadro de giz|55 quadros; textos de narrativa preservados na frente|
|Armario enferrujado|43 armarios da escola|
|Vaso sanitario|11 unidades; ceramica nao metalica|
|Pia antiga|14 unidades; espelhos e divisorias originais preservados|
|Bebedouro|1 no vestibulo junto da cantina; piso e espaco livre conferidos, frente acessivel, sem o piso auxiliar do exportador|

As malhas sao assets nativos, sem importador em runtime. Materiais URP/Lit compartilhados, mapas ate1K, emissao desligada. Telefone usa especular/gloss; normal DirectX do armario recebeu inversao apenas do verde. O mapa MS ambiguo do vaso foi preservado sem uso, com material ceramico explicito.

O carro foi adaptado mantendo camera, porta, radio, celular e coleta de lanterna. Camera original e FOV49 preservados; apenas o visual do sedan foi deslocado24cm para tras para ajustar a cabine. A luz existente da cabine foi reposicionada para dentro do novo teto. Pintura cinza remove letras de taxi sem editar a imagem original. As versoes originais dos modelos/ZIPs permanecem intactas.

Na escola,561 pecas cubicas antigas foram identificadas por suas faces exatas:6.732 triangulos removidos de7 lotes clonados. Nenhum recorte por caixa espacial arbitraria. Malhas originais e fontes permanecem preservadas. Salas bloqueadas continuam bloqueadas; o layout, paredes, portas e historia nao foram refeitos.

## Verificacao real

- Compilacao Unity sem erros.
- 25 testes unicos passaram:5 escritorio,11 estrada,6 escola,3 capturas em Play. Repeticoes de capturas nao somadas.
- 622 componentes de scripts e suas referencias, e5.200 colliders originais, conferidos sem alteracao. Foi adicionado apenas1 collider ao bebedouro.
- Nenhum MissingScript, material ausente ou shader de erro encontrado nas tres cenas.
- 10 ZIPs originais conferidos por SHA256. Save pessoal nao acessado; testes usam diretorios descartaveis antes do Awake e restauram preferencias.
- Pontos comparaveis fotografados em Play e20 vistas dos assets em cena de estudio. As fotos de estudo nao representam a iluminacao final do jogo.

## Como testar

1. Escritorio: telefone/cadeira/arquivos; E no PC, emails, gaveta e apito seguem funcionando.
2. Estrada: conferir novo sedan por dentro, radio e celular. Coletar lanterna e usar a porta separadamente para sair.
3. Escola: conferir quadros, armarios, sanitarios, pias e bebedouro; entrar na biblioteca e retomar checkpoint.
4. Seu save existente nao precisa ser apagado. Para conferir cenas anteriores, prefira um perfil de teste.

## Preservacao e limites

Backup: `Assets/_Substituidos/ModelRound2/Backup/`; provas e fotos: `outputs/ModelRound2/`. Os passes em `work/ModelRound2/` ja foram executados e possuem marcadores; nao reaplicar como se estivessem pendentes.

NAO VERIFICADO: todas as salas/angulos durante uma partida humana completa; build standalone e hardware de terceiros; convencao das normais nao declaradas pelos autores. Nao foi criado ZIP. Os modelos sao CC BY4.0; autores/fontes/licencas estao em CREDITS.md, ThirdParty/LICENCAS.txt e ThirdParty/Sketchfab_Round2/Licenses/.
