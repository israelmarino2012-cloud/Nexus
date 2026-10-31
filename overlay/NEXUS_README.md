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
O port GTK depende de X11 e só roda em Linux. Para Windows existe o port nativo
(`win32/`), também renomeado para Nexus (nome, ícone e textos). A interface dele
é a nativa do Windows (sem o tema escuro do GTK). Para gerar o `Nexus.exe`
portátil, veja `COMO_GERAR_EXE.md`.

## ROMs
Você precisa fornecer ROMs (.sfc/.smc) de jogos que você possui.
