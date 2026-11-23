# Whispers of the Past — Antes da Demolição

Jogo de terror psicológico em primeira pessoa. João de Claus volta a uma escola abandonada para investigar o desaparecimento de quatro alunos antes da demolição do prédio.

## Abrir na Unity

1. Instale o Git e o Git LFS. Execute `git lfs install` antes de clonar.
2. Clone este repositório privado com uma conta que tenha acesso. Execute `git lfs pull` na pasta clonada para confirmar o download dos arquivos grandes.
3. No Unity Hub, adicione a pasta com `Assets`, `Packages` e `ProjectSettings`. O projeto usa **Unity 6000.6.3f1** e URP.
4. Abra `Assets/HorrorMenu/Scenes/MainMenu.unity` e pressione Play. Escolha **Novo jogo** para iniciar a campanha.

## Organização

- `Assets/HorrorGameplay/Scripts` contém os sistemas de movimento, interação, áudio, narrativa, inspeção e salvamento.
- `Assets/HorrorMenu` contém o menu, a introdução e o ícone. O escritório, a estrada e a escola têm cenas nos seus próprios diretórios.
- `Assets/HorrorGameplay/Tests` contém os testes que aparecem no Unity Test Runner.
- `Assets/CREDITS.md` e `Assets/ThirdParty/LICENCAS.txt` registram os créditos e as licenças dos recursos.

Mantenha os arquivos `.meta` junto dos assets. A Unity usa esses identificadores para ligar scripts, modelos e materiais às cenas. A primeira importação recria a pasta `Library`.

