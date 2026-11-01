# Nexus

Emulador de Super Nintendo com interface escura e minimalista, baseado no
código-fonte do **Snes9x 1.60** (veja `LICENSE`, uso não comercial; os créditos
originais foram mantidos).

## O que mudou em relação ao Snes9x GTK
- Nome, executável (`nexus`), ícone, atalho (.desktop) e textos agora são "Nexus".
- Tema escuro minimalista aplicado em toda a interface (menu, janelas, botões,
  abas, listas, sliders), com destaque violeta. Código em `gtk/src/gtk_s9x.cpp`
  (`nexus_css`) — dá pra mudar as cores no bloco `@define-color` no topo.
- Novo ícone (`gtk/data/nexus*.png`, `nexus.svg`) e nova tela inicial.
- Menus e opções continuam exatamente nos mesmos lugares.
- Pasta de configuração: `~/.config/nexus`.
- Correção de compatibilidade com GCC 11+ em `conffile.h/.cpp`.

## Compilar (Linux, Ubuntu/Debian)
    sudo apt install build-essential meson ninja-build pkg-config \
      libgtk-3-dev libsdl2-dev libxrandr-dev libepoxy-dev libx11-dev \
      libpng-dev zlib1g-dev libminizip-dev libpulse-dev libasound2-dev
    cd gtk
    meson setup build --buildtype=release
    ninja -C build
    ./build/nexus caminho/do/jogo.sfc
    sudo ninja -C build install      # opcional: instala atalho e ícone

Se algum pacote opcional (xv, portaudio, wayland...) faltar, o meson só
desativa o recurso. Se faltar GTK3, SDL2 ou xrandr, ele para e avisa.

## Windows
Instale com `Nexus-Setup.exe` (ou use o .zip portatil). Abra o Nexus: a pagina inicial
tem o botao "Adicionar jogos" (ou arraste os arquivos .sfc/.smc/.zip para a janela).
As capas dos jogos sao baixadas automaticamente (precisa de internet) e ficam salvas
na pasta `covers`. Clique com o botao direito num jogo para trocar a capa ou remover.
Dica: quanto mais parecido com o nome oficial for o nome do arquivo
(ex.: "Super Mario World (USA).sfc"), mais facil e achar a capa.
`NexusCore.exe` e o emulador em si (baseado no Snes9x), com todos os menus e opcoes.

## ROMs
Você precisa fornecer ROMs (.sfc/.smc) de jogos que você possui.
