# Nexus Nightly — configuração única (uns 5 minutos)

Depois disso, o link de download do Nexus portátil fica sempre disponível e se
atualiza sozinho.

1. Crie conta em https://github.com e clique em **New repository**.
   Nome: `nexus`. Marque **Public** (releases públicos permitem compartilhar o link).
2. Na página do repositório, clique em **uploading an existing file** e arraste
   para lá **tudo que está dentro desta pasta**: a pasta `overlay`, a pasta
   `.github` e os outros arquivos. Clique em **Commit changes**.
   - Se a pasta `.github` não aparecer pro arraste (ela é oculta), faça assim:
     **Add file > Create new file**, digite o nome `.github/workflows/nightly.yml`,
     cole o conteúdo do arquivo `nightly.yml` e faça o commit.
3. Vá em **Settings > Actions > General > Workflow permissions** e escolha
   **Read and write permissions**. Salve.
4. Aba **Actions > Nexus Nightly > Run workflow**. Espere ~10 a 25 min.
5. Seu download fica em:
   `https://github.com/SEU_USUARIO/nexus/releases/tag/nightly`
   Mande esse link pra quem quiser: baixa o .zip, extrai e abre `Nexus.exe`.

Se der erro (bolinha vermelha), abra o passo que falhou e me mande o texto.
